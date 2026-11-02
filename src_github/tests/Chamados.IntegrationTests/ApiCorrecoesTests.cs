using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class ApiCorrecoesTests
{
    public static async Task Run(string connectionString, HttpClient http)
    {
        var passed = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Pass(string message) => Console.WriteLine($"OK correcoes {++passed}: {message}");
        using var db = new SqlConnection(connectionString);
        await db.OpenAsync();
        var id = -Random.Shared.Next(10_000_000,90_000_000);
        var marker = "NP2_CORRECOES_" + Guid.NewGuid().ToString("N");
        var created = false;
        int? apiCreatedId = null;
        SqlCommand Command(string text, SqlTransaction? transaction = null)
        {
            var command = new SqlCommand(text, db, transaction);
            command.Parameters.AddWithValue("@Id",id);
            command.Parameters.AddWithValue("@Other",id-1);
            command.Parameters.AddWithValue("@Marker",marker);
            return command;
        }
        async Task<string> Snapshot()
        {
            var values = new List<string>();
            foreach(var table in new[]{"Usuarios","Categorias","Prioridades","Status","Chamados","Comentarios","__EFMigrationsHistory"})
            {
                var key = table == "__EFMigrationsHistory" ? "MigrationId" : "Id";
                using var command = new SqlCommand($"SELECT (SELECT * FROM dbo.[{table}] ORDER BY [{key}] FOR JSON PATH,INCLUDE_NULL_VALUES)",db);
                values.Add((string)(await command.ExecuteScalarAsync())!);
            }
            return string.Join("\n",values);
        }
        async Task<string> Request(HttpMethod method, string path, string? token, HttpStatusCode expected, object? payload = null)
        {
            using var request = new HttpRequestMessage(method,path);
            if(token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
            if(payload != null) request.Content = JsonContent.Create(payload);
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            // Nao inclui corpo de respostas inesperadas na falha: pode conter segredo.
            Check(response.StatusCode == expected,$"{method} {path}: esperado {(int)expected}, recebido {(int)response.StatusCode}.");
            if(expected == HttpStatusCode.NoContent) Check(body.Length == 0,"204 deve permanecer sem corpo.");
            return body;
        }
        async Task<string> Login(string email)
        {
            var raw = await Request(HttpMethod.Post,"/api/auth/login",null,HttpStatusCode.OK,new{email,senha="Senha123"});
            using var json = JsonDocument.Parse(raw);
            return json.RootElement.GetProperty("token").GetString()!;
        }
        void NoCredentials(JsonElement element)
        {
            if(element.ValueKind == JsonValueKind.Object)
                foreach(var property in element.EnumerateObject())
                {
                    Check(!new[]{"senha","password","passwordhash","token","accesstoken","refreshtoken","secret","credentials"}
                        .Contains(property.Name,StringComparer.OrdinalIgnoreCase),"Propriedade de credencial encontrada no JSON.");
                    NoCredentials(property.Value);
                }
            else if(element.ValueKind == JsonValueKind.Array)
                foreach(var child in element.EnumerateArray()) NoCredentials(child);
        }
        JsonElement SafeJson(string raw)
        {
            using var json = JsonDocument.Parse(raw);
            NoCredentials(json.RootElement);
            Check(!raw.Contains("Senha123",StringComparison.Ordinal),"Valor de senha de teste encontrado no JSON.");
            return json.RootElement.Clone();
        }
        JsonElement Comment(string raw, int author, string text)
        {
            var json = SafeJson(raw);
            Check(json.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(new[]{"id","chamadoId","mensagem","dataEnvio","usuario"}.Order()),"Comentario nao corresponde ao DTO publico existente.");
            var user = json.GetProperty("usuario");
            Check(user.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(new[]{"id","nome","email","tipo"}.Order()),"Resumo publico do autor foi alterado.");
            Check(json.GetProperty("id").GetInt32()>0 && json.GetProperty("chamadoId").GetInt32()==id,"Identificacao do comentario incorreta.");
            Check(json.GetProperty("mensagem").GetString()==text && user.GetProperty("id").GetInt32()==author,"Mensagem/autor incorretos.");
            Check(!string.IsNullOrWhiteSpace(user.GetProperty("nome").GetString()) && !string.IsNullOrWhiteSpace(user.GetProperty("email").GetString()),"Dados publicos do autor ausentes.");
            Check(json.GetProperty("dataEnvio").GetDateTime()!=default,"Data de envio ausente.");
            return json;
        }
        async Task<(int Status, int Comments, DateTime? Closed)> State()
        {
            using var command = Command("SELECT StatusId,(SELECT COUNT(*) FROM dbo.Comentarios WHERE ChamadoId=@Id),DataFechamento FROM dbo.Chamados WHERE Id=@Id");
            using var reader = await command.ExecuteReaderAsync();
            Check(await reader.ReadAsync(),"Chamado temporario ausente.");
            return (reader.GetInt32(0),reader.GetInt32(1),reader.IsDBNull(2)?null:reader.GetDateTime(2));
        }
        var original = await Snapshot();
        try
        {
            using(var transaction = db.BeginTransaction())
            {
                using var command = Command("""
                    SET IDENTITY_INSERT dbo.Chamados ON;
                    INSERT dbo.Chamados (Id,Titulo,Descricao,UsuarioId,TecnicoId,CategoriaId,PrioridadeId,StatusId,DataAbertura,DataFechamento)
                    VALUES (@Id,@Marker,@Marker,1,NULL,1,1,1,DATEADD(minute,-5,SYSDATETIME()),NULL),
                           (@Other,@Marker,@Marker,3,NULL,1,1,1,DATEADD(minute,-5,SYSDATETIME()),NULL);
                    SET IDENTITY_INSERT dbo.Chamados OFF;
                    """,transaction);
                await command.ExecuteNonQueryAsync();
                transaction.Commit(); created = true;
            }
            var user = await Login("usuario@gmail.com");
            var tech = await Login("tecnico@gmail.com");
            var admin = await Login("gestor@gmail.com");
            var post = Comment(await Request(HttpMethod.Post,$"/api/chamados/{id}/comentarios",user,HttpStatusCode.OK,new{mensagem=marker}),1,marker);
            Check(post.GetProperty("usuario").GetProperty("tipo").GetString()=="Usuario","Tipo publico incorreto.");
            Pass("POST de comentario do solicitante: DTO completo e JSON sem credenciais em qualquer nivel");
            Comment(await Request(HttpMethod.Post,$"/api/chamados/{id}/comentarios",tech,HttpStatusCode.OK,new{mensagem=marker+" tecnico"}),2,marker+" tecnico");
            Pass("POST do tecnico preserva autor e campos publicos sem serializar entidades EF");
            var get = SafeJson(await Request(HttpMethod.Get,$"/api/chamados/{id}/comentarios",user,HttpStatusCode.OK));
            Check(get.GetArrayLength()==2,"GET nao recuperou os comentarios criados.");
            var saved = get.EnumerateArray().Single(c=>c.GetProperty("id").GetInt32()==post.GetProperty("id").GetInt32());
            Check(saved.GetProperty("mensagem").GetString()==marker && saved.GetProperty("usuario").GetProperty("nome").GetString()==post.GetProperty("usuario").GetProperty("nome").GetString(),"GET alterou mensagem/autor.");
            Check(saved.GetProperty("dataEnvio").GetDateTime().Ticks==post.GetProperty("dataEnvio").GetDateTime().Ticks,"POST alterou data persistida.");
            Pass("GET preserva identificacao, mensagem, data e resumo publico do POST");
            await Request(HttpMethod.Post,$"/api/chamados/{id}/comentarios",null,HttpStatusCode.Unauthorized,new{mensagem="nao deve gravar"});
            await Request(HttpMethod.Post,$"/api/chamados/{id-1}/comentarios",user,HttpStatusCode.Forbidden,new{mensagem="nao deve gravar"});
            Check((await State()).Comments==2,"Tentativa sem permissao gravou comentario.");
            Pass("comentario sem login retorna 401 e em chamado alheio retorna 403");

            var missing = await Request(HttpMethod.Put,$"/api/chamados/{int.MinValue}/status",tech,HttpStatusCode.NotFound,new{statusId=2});
            Check(!missing.Contains("Exception") && !missing.Contains("SqlClient"),"404 expoe detalhes internos.");
            Pass("PUT status de chamado inexistente retorna HTTP 404, sem excecao interna");
            await Request(HttpMethod.Put,$"/api/chamados/{id}/status",tech,HttpStatusCode.NoContent,new{statusId=2});
            Check((await State()).Status==2 && (await State()).Closed==null,"Atualizacao de status existente falhou.");
            await Request(HttpMethod.Put,$"/api/chamados/{id}/status",tech,HttpStatusCode.NoContent,new{statusId=3});
            var closed = await State();
            Check(closed.Status==3 && closed.Closed!=null,"Status 3 deixou de registrar fechamento.");
            await Request(HttpMethod.Put,$"/api/chamados/{id}/status",tech,HttpStatusCode.NoContent,new{statusId=2});
            Check((await State()).Closed==closed.Closed,"Retorno a outro status alterou regra preexistente do fechamento.");
            Pass("PUT existente continua 204, grava fechamento no status 3 e preserva demais regras");
            var beforeDenied = await State();
            foreach(var token in new string?[]{null,user,admin})
                await Request(HttpMethod.Put,$"/api/chamados/{id}/status",token,token==null?HttpStatusCode.Unauthorized:HttpStatusCode.Forbidden,new{statusId=1});
            await Request(HttpMethod.Put,$"/api/chamados/{int.MinValue}/status",null,HttpStatusCode.Unauthorized,new{statusId=2});
            Check(await State()==beforeDenied,"Requisicao sem perfil Tecnico alterou estado.");
            Pass("status exige Tecnico: 401/403 preservados e autorizacao precede verificacao de existencia");

            foreach(var path in new[]{"/api/usuarios","/api/usuarios/me","/api/chamados",$"/api/chamados/{id}",$"/api/chamados/{id}/comentarios"})
                SafeJson(await Request(HttpMethod.Get,path,user,HttpStatusCode.OK));
            Pass("GETs de usuarios, chamados e comentarios exercitados nao expoem credenciais");
            // Inspeciona o outro endpoint que ainda retorna entidade, sem altera-lo.
            var rawTicket = await Request(HttpMethod.Post,"/api/chamados",user,HttpStatusCode.OK,new{titulo=marker,descricao=marker,categoriaId=1,prioridadeId=1});
            using(var ticketDocument = JsonDocument.Parse(rawTicket)) apiCreatedId=ticketDocument.RootElement.GetProperty("id").GetInt32();
            SafeJson(rawTicket);
            Pass("POST de chamado inspecionado sem exposicao comprovada de credenciais neste fluxo");
        }
        finally
        {
            if(apiCreatedId.HasValue)
            {
                using var command = new SqlCommand("DELETE dbo.Chamados WHERE Id=@Id AND Descricao=@Marker",db);
                command.Parameters.AddWithValue("@Id",apiCreatedId.Value); command.Parameters.AddWithValue("@Marker",marker);
                await command.ExecuteNonQueryAsync();
            }
            if(created)
            {
                using var command = Command("DELETE dbo.Chamados WHERE Id IN (@Id,@Other) AND Descricao=@Marker");
                await command.ExecuteNonQueryAsync();
            }
            Check(await Snapshot()==original,"Dados originais divergiram apos testes/limpeza.");
            Pass("registros originais identicos; chamados e comentarios temporarios removidos");
        }
        Console.WriteLine($"Concluido: {passed} grupos de correcoes aprovados.");
    }
}
