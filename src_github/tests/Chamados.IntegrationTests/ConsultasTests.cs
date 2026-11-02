using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TechHelpSolutions.Data;
using TechHelpSolutions.Data.Queries;
using TechHelpSolutions.DTOs;
using TechHelpSolutions.Models;
using TechHelpSolutions.Services;

internal static class ConsultasTests
{
    public static async Task Run(string connectionString, HttpClient http)
    {
        var passed = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        void Pass(string message) { passed++; Console.WriteLine($"OK consultas {passed}: {message}"); }
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        void SameJson(object expected, string actual, string message)
        {
            Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, jsonOptions), JsonNode.Parse(actual)), message);
        }
        using var db = new SqlConnection(connectionString);
        await db.OpenAsync();
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
        var original = await Snapshot();
        try
        {
            using (var command = new SqlCommand("""
                SELECT COUNT(*) FROM sys.objects WHERE schema_id=SCHEMA_ID(N'dbo') AND
                  ((name=N'ufn_DuracaoMinutos' AND type='FN') OR
                   (name IN (N'vw_ChamadosDetalhados',N'vw_ComentariosDetalhados') AND type='V'));
                """, db))
                Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 3, "Function/Views ausentes.");
            using (var command = new SqlCommand("SELECT OBJECTPROPERTY(OBJECT_ID(N'dbo.ufn_DuracaoMinutos'), 'IsDeterministic')", db))
                Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "Function deve ser deterministica.");
            using (var command = new SqlCommand("""
                SELECT COUNT(*) FROM sys.columns
                WHERE object_id IN (OBJECT_ID(N'dbo.vw_ChamadosDetalhados'),OBJECT_ID(N'dbo.vw_ComentariosDetalhados'))
                AND (LOWER(name) LIKE '%senha%' OR LOWER(name) LIKE '%token%');
                """, db))
                Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 0, "View expoe segredo.");
            Pass("existencia, determinismo e ausencia de senhas/tokens nas views");

            var counter = new ReadCounter();
            using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString).AddInterceptors(counter).Options);
            // Consulta original independente: Includes e projecao usados antes das views.
            var originalTickets = await context.Chamados.AsNoTracking()
                .Include(c => c.Usuario).Include(c => c.Tecnico).Include(c => c.Categoria)
                .Include(c => c.Prioridade).Include(c => c.Status)
                .Include(c => c.Comentarios).ThenInclude(c => c.Usuario)
                .OrderByDescending(c => c.DataAbertura).ToListAsync();
            var service = new ChamadoService(context);
            counter.Commands.Clear();
            var tickets = await service.ConsultarChamadosDetalhados().OrderByDescending(c => c.DataAbertura).ToListAsync();
            SameJson(originalTickets.Select(c => Ticket(c, false)), JsonSerializer.Serialize(tickets, jsonOptions), "View alterou projecao de chamados.");
            Check(counter.Commands.Count == 1 && counter.Commands[0].Contains("vw_ChamadosDetalhados"), "Listagem deve consultar a view uma unica vez.");
            Check(!counter.Commands[0].Contains("Senha") && !counter.Commands[0].Contains("DuracaoMinutos"), "Listagem selecionou campos desnecessarios.");
            Check(tickets.Any(c => c.TecnicoId == null && c.Tecnico == null) && tickets.Any(c => c.Tecnico != null), "LEFT JOIN perdeu chamados com/sem tecnico.");
            Check(context.Model.FindEntityType(typeof(ChamadoDetalhado))!.FindPrimaryKey() == null, "View deve ser sem chave.");
            Pass("view de chamados equivalente a consulta original, com LEFT JOIN e um comando SQL");

            var originalComments = originalTickets.SelectMany(c => c.Comentarios).OrderBy(c => c.DataEnvio).Select(Comment).ToList();
            counter.Commands.Clear();
            var comments = await service.ConsultarComentariosDetalhados().OrderBy(c => c.DataEnvio).ToListAsync();
            SameJson(originalComments, JsonSerializer.Serialize(comments, jsonOptions), "View alterou projecao dos comentarios.");
            Check(counter.Commands.Count == 1 && counter.Commands[0].Contains("vw_ComentariosDetalhados"), "Comentarios devem consultar a view uma unica vez.");
            Check(originalTickets.Any(c => c.DataFechamento != null && c.Comentarios.Any(x => x.DataEnvio > c.DataFechamento)), "Falta seed de comentario posterior ao fechamento.");
            Check(context.Model.FindEntityType(typeof(ComentarioDetalhado))!.FindPrimaryKey() == null, "View de comentarios deve ser sem chave.");
            Check(!context.ChangeTracker.Entries().Any(), "Modelos de leitura nao devem ser rastreados.");
            Pass("comentarios equivalentes, incluindo comentario posterior ao fechamento, sem tracking");

            var start = new DateTime(2026, 10, 27, 9, 34, 13).AddTicks(3097614);
            var end = new DateTime(2026, 10, 27, 9, 38, 34).AddTicks(1561336);
            var cases = new (DateTime? Open, DateTime? Close)[]
            {
                (start,end), (start,null), (null,end), (null,null), (start,start), (end,start),
                (new DateTime(2026,1,1,0,0,59).AddTicks(9999999), new DateTime(2026,1,1,0,1,0)),
                (start,start.AddMilliseconds(1)), (start,start.AddTicks(1)),
                (start,start.AddDays(60)), (DateTime.MinValue,DateTime.MaxValue)
            };
            foreach (var (open, close) in cases)
            {
                using var command = new SqlCommand("SELECT dbo.ufn_DuracaoMinutos(@Open,@Close)", db);
                command.Parameters.Add("@Open", SqlDbType.DateTime2).Value = (object?)open ?? DBNull.Value;
                command.Parameters.Add("@Close", SqlDbType.DateTime2).Value = (object?)close ?? DBNull.Value;
                command.Parameters["@Open"].Scale = command.Parameters["@Close"].Scale = 7;
                var value = await command.ExecuteScalarAsync();
                var expected = Duration(open,close);
                Check(expected == null ? value is DBNull : value is double actual && actual == expected,
                    "Duracao SQL divergiu da precisao em milissegundos do painel.");
            }
            Pass("11 casos de duracao: fracao, NULL, negativa, submilissegundo, fronteira de minuto e intervalo longo");

            // Prova de traducao real do HasDbFunction; sem avaliacao alternativa no cliente.
            var durationsQuery = context.Chamados.Select(c => new { c.Id, Duracao = FuncoesSql.DuracaoMinutos(c.DataAbertura,c.DataFechamento) });
            Check(durationsQuery.ToQueryString().Contains("[dbo].[ufn_DuracaoMinutos]"), "EF nao traduziu a function.");
            var durations = await durationsQuery.ToListAsync();
            var viewDurations = await context.ChamadosDetalhados.Select(c => new { c.Id, c.DuracaoMinutos }).ToListAsync();
            foreach (var c in originalTickets)
            {
                var expected = Duration(c.DataAbertura,c.DataFechamento);
                Check(durations.Single(x => x.Id == c.Id).Duracao == expected && viewDurations.Single(x => x.Id == c.Id).DuracaoMinutos == expected, "Function/coluna da view divergiram do painel.");
            }
            // O painel usa somente resolvidos/finalizados com ambas as datas na media.
            var resolved = originalTickets.Where(c => c.DataFechamento != null &&
                (c.Status.Nome.Contains("final",StringComparison.OrdinalIgnoreCase) || c.Status.Nome.Contains("resolvido",StringComparison.OrdinalIgnoreCase))).ToList();
            var expectedAverage = resolved.Count > 0 ? resolved.Average(c => Duration(c.DataAbertura,c.DataFechamento)!.Value) : 0;
            var ids = resolved.Select(c => c.Id).ToArray();
            var average = await context.Chamados.Where(c => ids.Contains(c.Id))
                .Select(c => FuncoesSql.DuracaoMinutos(c.DataAbertura,c.DataFechamento)).AverageAsync() ?? 0;
            Check(average == expectedAverage, "Media via function alterou indicador.");
            Pass("mapeamento EF da function, coluna da view e media equivalentes ao painel");

            async Task<string> Login(string email)
            {
                using var response = await http.PostAsJsonAsync("/api/auth/login", new { email, senha = "Senha123" });
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return json.RootElement.GetProperty("token").GetString()!;
            }
            async Task<string> Get(string path, string? token, HttpStatusCode expected = HttpStatusCode.OK)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,path);
                if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
                using var response = await http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                Check(response.StatusCode == expected,$"GET {path}: esperado {(int)expected}, recebido {(int)response.StatusCode}: {body}");
                return body;
            }
            var user = await Login("usuario@gmail.com");
            var tech = await Login("tecnico@gmail.com");
            var admin = await Login("gestor@gmail.com");
            foreach (var token in new[] {user,tech,admin})
            {
                var filters = new (int? Status,int? Priority,int? Category)[]
                {
                    (null,null,null),(1,null,null),(2,null,null),(3,null,null),
                    (null,1,null),(null,2,null),(null,3,null),(null,null,1),(null,null,2),(null,null,3),(null,null,4),
                    (2,2,2),(1,3,3),(3,1,2),(int.MaxValue,null,null)
                };
                foreach (var (status,priority,category) in filters)
                {
                    var path = $"/api/chamados?status={status}&prioridade={priority}&categoria={category}";
                    var expected = originalTickets.Where(c => (token != user || c.UsuarioId == 1) && (!status.HasValue || c.StatusId == status) &&
                        (!priority.HasValue || c.PrioridadeId == priority) && (!category.HasValue || c.CategoriaId == category));
                    SameJson(expected.Select(c => Ticket(c,false)),await Get(path,token),"Filtro, ordenacao ou JSON de listagem alterado.");
                }
            }
            Pass("45 listagens HTTP equivalentes: tres perfis, filtros simples/combinados e resultado vazio");
            SameJson(originalTickets.Where(c => c.UsuarioId == 1).Select(c => Ticket(c,false)),await Get("/api/chamados/meus",user),"Meus chamados divergiram.");
            SameJson(originalTickets.Where(c => c.TecnicoId == 2).Select(c => Ticket(c,false)),await Get("/api/chamados/meus-atendimentos",tech),"Meus atendimentos divergiram.");
            foreach (var c in originalTickets)
            {
                SameJson(c.Comentarios.OrderBy(x => x.DataEnvio).Select(Comment),await Get($"/api/chamados/{c.Id}/comentarios",user),"JSON/ordenacao dos comentarios alterados.");
                SameJson(Ticket(c,true),await Get($"/api/chamados/{c.Id}",tech),"Detalhamento existente sofreu regressao.");
            }
            Pass("HTTP meus, meus-atendimentos, comentarios e detalhamento preservam todos os DTOs");
            await Get("/api/chamados",null,HttpStatusCode.Unauthorized);
            await Get("/api/chamados/1/comentarios",null,HttpStatusCode.Unauthorized);
            await Get("/api/chamados/meus",tech,HttpStatusCode.Forbidden);
            await Get("/api/chamados/meus-atendimentos",user,HttpStatusCode.Forbidden);
            await Get($"/api/chamados/{int.MinValue}/comentarios",user,HttpStatusCode.NotFound);
            Pass("autenticacao, perfis e 404 preservados nas consultas HTTP");
        }
        finally
        {
            Check(await Snapshot() == original,"Dados originais mudaram durante testes de leitura.");
            Pass("todas as tabelas identicas antes/depois; testes sem escrita");
        }
        Console.WriteLine($"Concluido: {passed} grupos de consultas aprovados.");
    }

    private static double? Duration(DateTime? open, DateTime? close)
        => open == null || close == null ? null : Math.Max(0, (close.Value.Ticks / TimeSpan.TicksPerMillisecond - open.Value.Ticks / TimeSpan.TicksPerMillisecond) / 60000.0);

    private static UsuarioResumoDTO User(Usuario u) => new() { Id=u.Id,Nome=u.Nome,Email=u.Email,Tipo=u.Tipo };
    private static ComentarioResponseDTO Comment(Comentario c) => new()
        { Id=c.Id,ChamadoId=c.ChamadoId,Mensagem=c.Mensagem,DataEnvio=c.DataEnvio,Usuario=User(c.Usuario) };
    private static ChamadoResponseDTO Ticket(Chamado c, bool comments) => new()
    {
        Id=c.Id,Titulo=c.Titulo,Descricao=c.Descricao,UsuarioId=c.UsuarioId,TecnicoId=c.TecnicoId,
        CategoriaId=c.CategoriaId,PrioridadeId=c.PrioridadeId,StatusId=c.StatusId,
        DataAbertura=c.DataAbertura,DataFechamento=c.DataFechamento,
        Usuario=User(c.Usuario),Tecnico=c.Tecnico == null ? null : User(c.Tecnico),
        Categoria=new ItemCatalogoDTO {Id=c.Categoria.Id,Nome=c.Categoria.Nome},
        Prioridade=new ItemCatalogoDTO {Id=c.Prioridade.Id,Nome=c.Prioridade.Nome},
        Status=new ItemCatalogoDTO {Id=c.Status.Id,Nome=c.Status.Nome},
        Comentarios=comments ? c.Comentarios.OrderBy(x => x.DataEnvio).Select(Comment).ToList() : null
    };

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public List<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }
    }
}
