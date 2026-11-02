using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TechHelpSolutions.Data;
using TechHelpSolutions.Models;
using TechHelpSolutions.Services;

// Testes reais, sem pacotes adicionais. Nao executar contra banco de producao.
// IDs negativos explicitos nao avancam os contadores IDENTITY existentes.
var connectionString = Environment.GetEnvironmentVariable("THS_TEST_CONNECTION")
    ?? "Server=.\\SQLEXPRESS;Database=TechHelpSolutions;Integrated Security=True;TrustServerCertificate=True";
using var http = new HttpClient { BaseAddress = new Uri(args.FirstOrDefault() ?? "http://127.0.0.1:5278"), Timeout = TimeSpan.FromSeconds(20) };
if (args.Contains("--senhas")) { await PasswordTests.Run(connectionString, http); return; }
if (args.Contains("--acesso"))
{
    await ControleAcessoTests.Run(connectionString, http);
    return;
}
if (args.Contains("--correcoes"))
{
    await ApiCorrecoesTests.Run(connectionString, http);
    return;
}
if (args.Contains("--indices"))
{
    var fase = args.Single(a => a.StartsWith("--fase=")).Substring("--fase=".Length);
    await IndexTests.Run(connectionString, fase);
    return;
}
if (args.Contains("--consultas"))
{
    await ConsultasTests.Run(connectionString, http);
    return;
}
if (args.Contains("--trigger"))
{
    await TriggerTests.Run(connectionString, http);
    return;
}
var id = -Random.Shared.Next(100_000_000, 1_000_000_000);
var tecnicoA = id;
var tecnicoB = id - 1;
var marker = "NP2_TEST_" + Guid.NewGuid().ToString("N");
var fixturePasswordHash = new PasswordHashService().Hash("SenhaTesteNP2!");
var original = "";
var created = false;
var passed = 0;
using var db = new SqlConnection(connectionString);
await db.OpenAsync();

void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
void Pass(string message) { passed++; Console.WriteLine($"OK {passed}: {message}"); }

SqlCommand Command(SqlConnection connection, string text, SqlTransaction? transaction = null)
{
    var command = new SqlCommand(text, connection, transaction) { CommandTimeout = 15 };
    command.Parameters.AddWithValue("@Id", id);
    command.Parameters.AddWithValue("@A", tecnicoA);
    command.Parameters.AddWithValue("@B", tecnicoB);
    command.Parameters.AddWithValue("@Marker", marker);
    command.Parameters.AddWithValue("@PasswordHash", fixturePasswordHash);
    return command;
}
async Task Execute(string text, SqlTransaction? transaction = null)
{
    using var command = Command(db, text, transaction);
    await command.ExecuteNonQueryAsync();
}
async Task<string> Snapshot()
{
    var values = new List<string>();
    foreach (var table in new[] { "Usuarios", "Categorias", "Prioridades", "Status", "Chamados", "Comentarios", "__EFMigrationsHistory" })
    {
        var key = table == "__EFMigrationsHistory" ? "MigrationId" : "Id";
        using var command = new SqlCommand($"SELECT (SELECT * FROM dbo.[{table}] ORDER BY [{key}] FOR JSON PATH, INCLUDE_NULL_VALUES)", db);
        values.Add((string)(await command.ExecuteScalarAsync())!);
    }
    return string.Join("\n", values);
}
async Task<Estado> ReadState()
{
    using var command = Command(db, "SELECT TecnicoId, StatusId, DataFechamento FROM dbo.Chamados WHERE Id=@Id");
    using var reader = await command.ExecuteReaderAsync();
    Check(await reader.ReadAsync(), "Chamado temporario ausente.");
    return new Estado(reader.IsDBNull(0) ? null : reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetDateTime(2));
}
SqlCommand Procedure(SqlConnection connection, bool finalizar, int tecnico, Estado expected, SqlTransaction? transaction = null, int? chamado = null)
{
    var command = new SqlCommand(finalizar ? "dbo.usp_FinalizarChamado" : "dbo.usp_AssumirChamado", connection, transaction)
    { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
    command.Parameters.Add("@ChamadoId", SqlDbType.Int).Value = chamado ?? id;
    command.Parameters.Add("@TecnicoId", SqlDbType.Int).Value = tecnico;
    command.Parameters.Add("@TecnicoIdEsperado", SqlDbType.Int).Value = (object?)expected.Tecnico ?? DBNull.Value;
    command.Parameters.Add("@StatusIdEsperado", SqlDbType.Int).Value = expected.Status;
    command.Parameters.Add("@DataFechamentoEsperada", SqlDbType.DateTime2).Value = (object?)expected.Fechamento ?? DBNull.Value;
    command.Parameters["@DataFechamentoEsperada"].Scale = 7;
    if (finalizar)
    {
        command.Parameters.Add("@DataFechamento", SqlDbType.DateTime2).Value = DateTime.Now;
        command.Parameters["@DataFechamento"].Scale = 7;
    }
    return command;
}
async Task Error(int number, Func<Task> operation)
{
    try { await operation(); }
    catch (SqlException ex) when (ex.Number == number) { return; }
    throw new InvalidOperationException($"Era esperado o erro SQL {number}.");
}
async Task Reset()
    => await Execute("UPDATE dbo.Chamados SET TecnicoId=NULL, StatusId=1, DataFechamento=NULL WHERE Id=@Id AND Titulo=@Marker");

// Espera evidencia no servidor, sem supor que um atraso fixo garante concorrencia.
async Task WaitBlocked(int blocker)
{
    for (var attempt = 0; attempt < 100; attempt++)
    {
        using var command = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Blocker", db);
        command.Parameters.AddWithValue("@Blocker", blocker);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0) return;
        await Task.Delay(50);
    }
    throw new InvalidOperationException("Segunda sessao nao chegou ao bloqueio esperado (requer visibilidade das DMVs).");
}
async Task<int> SessionId(SqlConnection connection, SqlTransaction? transaction = null)
{
    using var command = new SqlCommand("SELECT @@SPID", connection, transaction);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}
async Task<string> Login(string email)
{
    using var response = await http.PostAsJsonAsync("/api/auth/login", new { email, senha = "SenhaTesteNP2!" });
    Check(response.StatusCode == HttpStatusCode.OK, "Login de teste falhou.");
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return json.RootElement.GetProperty("token").GetString()!;
}
async Task<HttpResponseMessage> Put(string action, string? token, int? chamado = null)
{
    using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/chamados/{chamado ?? id}/{action}");
    if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await http.SendAsync(request);
}
async Task Status(string action, string? token, HttpStatusCode expected, int? chamado = null)
{
    using var response = await Put(action, token, chamado);
    var body = await response.Content.ReadAsStringAsync();
    Check(response.StatusCode == expected, $"{action}: esperado {(int)expected}, recebido {(int)response.StatusCode}: {body}");
    if (expected == HttpStatusCode.NoContent) Check(body.Length == 0, "204 deve continuar sem corpo.");
    if (expected == HttpStatusCode.BadRequest)
        Check(body.Contains("O chamado precisa ser assumido antes de ser finalizado."), "Mensagem 400 alterada.");
}

try
{
    original = await Snapshot();
    using (var command = new SqlCommand("SELECT COUNT(*) FROM sys.procedures WHERE schema_id=SCHEMA_ID('dbo') AND name IN ('usp_AssumirChamado','usp_FinalizarChamado')", db))
        Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 2, "Procedures ausentes.");
    Pass("duas procedures presentes");
    // Uma unica transacao para criar o conjunto de teste, com colisao tratada por PK.
    using (var transaction = db.BeginTransaction())
    {
        await Execute("""
            SET IDENTITY_INSERT dbo.Usuarios ON;
            INSERT dbo.Usuarios (Id,Nome,Email,Senha,Tipo,DataCriacao)
            VALUES (@A,@Marker,@Marker+'a@test.invalid',@PasswordHash,N'Tecnico',SYSDATETIME()),
                   (@B,@Marker,@Marker+'b@test.invalid',@PasswordHash,N'Tecnico',SYSDATETIME());
            SET IDENTITY_INSERT dbo.Usuarios OFF;
            SET IDENTITY_INSERT dbo.Chamados ON;
            INSERT dbo.Chamados (Id,Titulo,Descricao,UsuarioId,TecnicoId,CategoriaId,PrioridadeId,StatusId,DataAbertura,DataFechamento)
            VALUES (@Id,@Marker,@Marker,1,NULL,1,1,1,SYSDATETIME(),NULL);
            SET IDENTITY_INSERT dbo.Chamados OFF;
            """, transaction);
        transaction.Commit(); created = true;
    }
    var initial = await ReadState();
    using (var command = Procedure(db, false, tecnicoA, initial)) await command.ExecuteNonQueryAsync();
    Check((await ReadState()) == new Estado(tecnicoA, 2, null), "Atribuicao atomica falhou.");
    Pass("atribuicao valida");
    using (var command = Procedure(db, false, tecnicoB, await ReadState())) await command.ExecuteNonQueryAsync();
    Check((await ReadState()).Tecnico == tecnicoB, "Reatribuicao sequencial foi proibida.");
    Pass("reatribuicao preservada");
    foreach (var invalid in new[] { 1, int.MinValue })
    {
        var state = await ReadState();
        using var command = Procedure(db, false, invalid, state);
        await Error(51102, async () => { await command.ExecuteNonQueryAsync(); });
        Check(await ReadState() == state, "Validacao alterou o chamado.");
    }
    using (var command = Procedure(db, false, tecnicoA, initial, chamado: int.MinValue))
        await Error(51101, async () => { await command.ExecuteNonQueryAsync(); });
    Pass("tecnico inexistente, inelegivel e chamado ausente rejeitados");
    using (var command = Procedure(db, true, tecnicoA, await ReadState()))
        await Error(51104, async () => { await command.ExecuteNonQueryAsync(); });
    Pass("finalizacao por outro tecnico rejeitada");
    using (var command = Procedure(db, true, tecnicoB, await ReadState())) await command.ExecuteNonQueryAsync();
    var closed = await ReadState();
    Check(closed.Status == 3 && closed.Fechamento != null && closed.Tecnico == tecnicoB, "Finalizacao falhou.");
    using (var command = Procedure(db, false, tecnicoA, closed)) await command.ExecuteNonQueryAsync();
    Check((await ReadState()).Fechamento == closed.Fechamento, "Assumir alterou DataFechamento da NP1.");
    Pass("finalizacao valida e fechamento preservado ao reassumir");
    await Reset();
    using (var command = Procedure(db, true, tecnicoA, await ReadState()))
        await Error(51103, async () => { await command.ExecuteNonQueryAsync(); });
    Pass("finalizacao sem responsavel rejeitada");

    // Erro apos escrita previa na transacao externa: XACT_ABORT + CATCH desfaz tudo.
    using (var transaction = db.BeginTransaction())
    {
        await Execute("UPDATE dbo.Chamados SET Descricao=N'rollback probe' WHERE Id=@Id", transaction);
        using var command = Procedure(db, false, 1, await ReadStateInTransaction(transaction), transaction);
        await Error(51102, async () => { await command.ExecuteNonQueryAsync(); });
    }
    using (var command = Command(db, "SELECT Descricao FROM dbo.Chamados WHERE Id=@Id"))
        Check((string)(await command.ExecuteScalarAsync())! == marker, "Rollback nao restaurou a escrita anterior.");
    using (var command = new SqlCommand("SELECT @@TRANCOUNT", db))
        Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 0, "Transacao ficou aberta apos erro.");
    Pass("rollback integral em erro e nenhuma transacao pendente");

    // A comita enquanto B aguarda o mesmo registro: B deve rejeitar o snapshot antigo.
    async Task Race(bool firstFinishes, bool secondFinishes)
    {
        await Reset();
        if (firstFinishes) { using var setup = Procedure(db, false, tecnicoA, await ReadState()); await setup.ExecuteNonQueryAsync(); }
        var expected = await ReadState();
        using var a = new SqlConnection(connectionString); await a.OpenAsync();
        using var b = new SqlConnection(connectionString); await b.OpenAsync();
        using var transaction = a.BeginTransaction();
        using var first = Procedure(a, firstFinishes, tecnicoA, expected, transaction);
        await first.ExecuteNonQueryAsync();
        using var second = Procedure(b, secondFinishes, secondFinishes ? tecnicoA : tecnicoB, expected);
        var pending = second.ExecuteNonQueryAsync();
        await WaitBlocked(await SessionId(a, transaction));
        transaction.Commit();
        await Error(51105, async () => { await pending; });
        var actual = await ReadState();
        Check(actual.Tecnico == tecnicoA && actual.Status == (firstFinishes ? 3 : 2), "Operacao concorrente sobrescreveu o vencedor.");
    }
    await Race(false, false); Pass("duas atribuicoes concorrentes: uma vence e outra recebe 51105");
    await Race(true, true); Pass("duas finalizacoes concorrentes: segunda recebe 51105");
    await Race(true, false); Pass("finalizacao concorrente com reatribuicao: estado vencedor preservado");

    // Verifica invalidacao do tracker no mesmo DbContext usado pelo servico.
    await Reset();
    using (var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options))
    {
        var tracked = (await context.Chamados.FindAsync(id))!;
        Check(await new ChamadoService(context).AssumirChamado(tracked, tecnicoA) == ResultadoOperacaoChamado.Sucesso, "Servico nao assumiu.");
        Check(context.Entry(tracked).State == EntityState.Detached, "Tracker reteve entidade antiga.");
        Check((await context.Chamados.FindAsync(id))!.TecnicoId == tecnicoA, "FindAsync retornou estado antigo.");
        // Estado propositalmente obsoleto deve ser convertido para Conflito.
        Check(await new ChamadoService(context).AssumirChamado(tracked, tecnicoB) == ResultadoOperacaoChamado.Conflito, "Servico nao converteu conflito.");
    }
    Pass("servico parametrizado, mapeamento de conflito e tracker atualizados");

    var tokenA = await Login(marker + "a@test.invalid");
    var tokenB = await Login(marker + "b@test.invalid");
    using var userLogin = await http.PostAsJsonAsync("/api/auth/login", new { email = "usuario@gmail.com", senha = "Senha123" });
    userLogin.EnsureSuccessStatusCode();
    using var userJson = JsonDocument.Parse(await userLogin.Content.ReadAsStringAsync());
    var userToken = userJson.RootElement.GetProperty("token").GetString()!;
    await Reset();
    foreach (var action in new[] { "assumir", "finalizar" })
    {
        await Status(action, null, HttpStatusCode.Unauthorized);
        await Status(action, userToken, HttpStatusCode.Forbidden);
        await Status(action, tokenA, HttpStatusCode.NotFound, int.MinValue);
    }
    Pass("HTTP 401, 403 por perfil e 404 preservados nos dois endpoints");
    await Status("finalizar", tokenA, HttpStatusCode.BadRequest);
    await Status("assumir", tokenA, HttpStatusCode.NoContent);
    await Status("finalizar", tokenB, HttpStatusCode.Forbidden);
    await Status("finalizar", tokenA, HttpStatusCode.NoContent);
    await Status("finalizar", tokenA, HttpStatusCode.NoContent);
    var httpClosed = await ReadState();
    await Status("assumir", tokenB, HttpStatusCode.NoContent);
    Check((await ReadState()).Fechamento == httpClosed.Fechamento, "HTTP assumir alterou fechamento.");
    Pass("HTTP 400/403/204, finalizacao repetida e reatribuicao preservados");
    await Execute("UPDATE dbo.Usuarios SET Tipo=N'Usuario' WHERE Id=@A AND Nome=@Marker");
    await Status("assumir", tokenA, HttpStatusCode.Forbidden);
    await Execute("UPDATE dbo.Usuarios SET Tipo=N'Tecnico' WHERE Id=@A AND Nome=@Marker");
    Pass("HTTP rejeita tecnico que perdeu elegibilidade mesmo com JWT anterior");

    async Task HttpRace(bool finalizar)
    {
        await Reset();
        if (finalizar) await Status("assumir", tokenA, HttpStatusCode.NoContent);
        using var gate = new SqlConnection(connectionString); await gate.OpenAsync();
        using var transaction = gate.BeginTransaction();
        using (var command = Command(gate, "SELECT Id FROM dbo.Chamados WITH (UPDLOCK) WHERE Id=@Id", transaction))
            await command.ExecuteScalarAsync();
        var pending = Put(finalizar ? "finalizar" : "assumir", tokenA);
        await WaitBlocked(await SessionId(gate, transaction));
        using (var command = Command(gate, "UPDATE dbo.Chamados SET TecnicoId=@B,StatusId=2 WHERE Id=@Id", transaction))
            await command.ExecuteNonQueryAsync();
        transaction.Commit();
        using var response = await pending;
        Check(response.StatusCode == HttpStatusCode.Conflict, $"Conflito HTTP deveria retornar 409, recebeu {(int)response.StatusCode}.");
        Check((await ReadState()).Tecnico == tecnicoB, "HTTP sobrescreveu alteracao concorrente.");
    }
    await HttpRace(false); Pass("HTTP assumir: concorrencia real retorna 409 sem sobrescrita");
    await HttpRace(true); Pass("HTTP finalizar: mudanca concorrente de responsavel retorna 409");
}
finally
{
    if (created)
    {
        // Exclusao limitada aos IDs aleatorios e marcador deste teste. Nunca toca nos seeds.
        await Execute("""
            DELETE dbo.Chamados WHERE Id=@Id AND Titulo=@Marker;
            DELETE dbo.Usuarios WHERE Id IN (@A,@B) AND Nome=@Marker;
            """);
    }
    if (original.Length > 0)
    {
        Check(await Snapshot() == original, "Dados originais divergiram apos testes/limpeza.");
        Pass("todos os dados originais identicos antes/depois; temporarios removidos");
    }
}
Console.WriteLine($"Concluido: {passed} verificacoes aprovadas.");

async Task<Estado> ReadStateInTransaction(SqlTransaction transaction)
{
    using var command = Command(db, "SELECT TecnicoId,StatusId,DataFechamento FROM dbo.Chamados WHERE Id=@Id", transaction);
    using var reader = await command.ExecuteReaderAsync();
    Check(await reader.ReadAsync(), "Chamado de teste ausente.");
    return new Estado(reader.IsDBNull(0) ? null : reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetDateTime(2));
}
record Estado(int? Tecnico, int Status, DateTime? Fechamento);
