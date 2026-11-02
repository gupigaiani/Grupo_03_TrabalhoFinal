using TechHelpSolutions.Services;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class ControleAcessoTests
{
    public static async Task Run(string connectionString, HttpClient http)
    {
        var passed=0;
        void Check(bool condition,string message) {if(!condition) throw new InvalidOperationException(message);}
        void Pass(string message) => Console.WriteLine($"OK acesso {++passed}: {message}");
        using var db=new SqlConnection(connectionString); await db.OpenAsync();
        var marker="NP2_ACESSO_"+Guid.NewGuid().ToString("N");
        var gestorId=-Random.Shared.Next(100_000_000,900_000_000);
        var firstTicket=gestorId-10;
        var createdUsers=new List<int>();
        var fixtureCreated=false;
        const string password="SenhaTesteAcessoNP2!";

        async Task<string> Snapshot()
        {
            var rows=new List<string>();
            foreach(var table in new[]{"Usuarios","Categorias","Prioridades","Status","Chamados","Comentarios","__EFMigrationsHistory"})
            {
                var key=table=="__EFMigrationsHistory"?"MigrationId":"Id";
                using var command=new SqlCommand($"SELECT (SELECT * FROM dbo.[{table}] ORDER BY [{key}] FOR JSON PATH,INCLUDE_NULL_VALUES)",db);
                rows.Add((string)(await command.ExecuteScalarAsync())!);
            }
            return string.Join("\n",rows);
        }
        async Task<JsonElement?> Request(HttpMethod method,string path,string? token,HttpStatusCode expected,object? body=null)
        {
            using var request=new HttpRequestMessage(method,path);
            if(token!=null) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            if(body!=null) request.Content=JsonContent.Create(body);
            using var response=await http.SendAsync(request);
            var raw=await response.Content.ReadAsStringAsync();
            Check(response.StatusCode==expected,$"{method} {path}: esperado {(int)expected}, recebido {(int)response.StatusCode}.");
            if(raw.Length==0) return null;
            try {using var json=JsonDocument.Parse(raw);return json.RootElement.Clone();}
            catch(JsonException) {return null;}
        }
        async Task<(string Token,string Role)> Login(string email,string pass)
        {
            var result=(await Request(HttpMethod.Post,"/api/auth/login",null,HttpStatusCode.OK,new{email,senha=pass}))!.Value;
            return (result.GetProperty("token").GetString()!,result.GetProperty("usuario").GetProperty("tipo").GetString()!);
        }
        async Task<int> Register(string suffix,string type)
        {
            // Campos extras nao fazem parte do DTO: nao podem conceder privilegios.
            var result=(await Request(HttpMethod.Post,"/api/usuarios",null,HttpStatusCode.OK,
                new{nome=marker+suffix,email=marker+suffix+"@test.invalid",senha=password,tipo=type,roles=new[]{"Admin"},isAdmin=true}))!.Value;
            var id=result.GetProperty("id").GetInt32();createdUsers.Add(id);
            Check(result.GetProperty("tipo").GetString()=="Usuario","Cadastro concedeu perfil privilegiado.");
            return id;
        }
        async Task<int> CountMarkedUsers()
        {
            using var command=new SqlCommand("SELECT COUNT(*) FROM dbo.Usuarios WHERE Nome LIKE @Marker",db);
            command.Parameters.AddWithValue("@Marker",marker+"%");
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        var original=await Snapshot();
        try
        {
            var normal=await Login("usuario@gmail.com","Senha123");
            var technician=await Login("tecnico@gmail.com","Senha123");
            var administrator=await Login("gestor@gmail.com","Senha123");
            Check(normal.Role=="Usuario" && technician.Role=="Tecnico" && administrator.Role=="Admin","Perfis originais foram alterados.");
            var a=await Register("a","Usuario");
            var b=await Register("b"," uSuArIo ");
            var loginA=await Login(marker+"a@test.invalid",password);
            var loginB=await Login(marker+"b@test.invalid",password);
            Check(loginA.Role=="Usuario" && loginB.Role=="Usuario","JWT do cadastro ganhou privilegios por campos extras.");
            await Request(HttpMethod.Get,"/api/chamados/meus-atendimentos",loginA.Token,HttpStatusCode.Forbidden);
            Pass("cadastro publico Usuario, normalizacao de caixa/espacos e login sem privilegios extras");

            var usersBeforeDenied=await CountMarkedUsers();
            foreach(var role in new[]{"Tecnico","Gestor","Admin"," tecnico ","gEsToR"," aDmIn ","Outro"})
                await Request(HttpMethod.Post,"/api/usuarios",null,HttpStatusCode.BadRequest,
                    new{nome=marker+" rejeitado",email=marker+"rejeitado@test.invalid",senha=password,tipo=role});
            // O mesmo cadastro publico nao vira um mecanismo administrativo ao receber JWT.
            await Request(HttpMethod.Post,"/api/usuarios",administrator.Token,HttpStatusCode.BadRequest,
                new{nome=marker+" rejeitado",email=marker+"rejeitado@test.invalid",senha=password,tipo="Tecnico"});
            await Request(HttpMethod.Post,"/api/usuarios",null,HttpStatusCode.BadRequest,
                new Dictionary<string,object>{{"nome",marker+" rejeitado"},{"email",marker+"rejeitado@test.invalid"},{"senha",password},{"tipo","Usuario"},{"Tipo","Admin"}});
            await Request(HttpMethod.Post,"/api/usuarios",null,HttpStatusCode.BadRequest,
                new{nome=marker+" rejeitado",email=marker+"rejeitado@test.invalid",senha=password,tipo=new{nome="Admin"}});
            Check(await CountMarkedUsers()==usersBeforeDenied,"Cadastro rejeitado persistiu uma conta.");
            Pass("Tecnico/Gestor/Admin e manipulacoes do JSON rejeitados com 400, sem persistencia");

            foreach(var requestedRole in new[]{"Tecnico","Gestor","Admin"," aDmIn "})
                await Request(HttpMethod.Put,$"/api/usuarios/{a}",loginA.Token,HttpStatusCode.Forbidden,
                    new{nome=marker+" tentativa",email=marker+"tentativa@test.invalid",tipo=requestedRole});
            var unchanged=(await Request(HttpMethod.Get,"/api/usuarios/me",loginA.Token,HttpStatusCode.OK))!.Value;
            Check(unchanged.GetProperty("tipo").GetString()=="Usuario" && unchanged.GetProperty("nome").GetString()==marker+"a" && unchanged.GetProperty("email").GetString()==(marker+"a@test.invalid").ToLowerInvariant(),"PUT rejeitado alterou dados pessoais/perfil.");
            Pass("autoalteracao para perfil privilegiado retorna 403 e nao grava outros campos");
            var updatedName=marker+"a atualizado";
            var updatedEmail=marker+"a.atualizado@test.invalid";
            var updated=(await Request(HttpMethod.Put,$"/api/usuarios/{a}",loginA.Token,HttpStatusCode.OK,
                new{nome=updatedName,email=updatedEmail,tipo="Usuario"}))!.Value;
            Check(updated.GetProperty("nome").GetString()==updatedName && updated.GetProperty("email").GetString()==updatedEmail && updated.GetProperty("tipo").GetString()=="Usuario","Edicao pessoal legitima foi alterada.");
            loginA=await Login(updatedEmail,password);
            await Request(HttpMethod.Put,$"/api/usuarios/{b}",loginA.Token,HttpStatusCode.Forbidden,
                new{nome=marker+"b",email=marker+"b@test.invalid",tipo="Usuario"});
            await Request(HttpMethod.Put,$"/api/usuarios/{a}",administrator.Token,HttpStatusCode.OK,
                new{nome=updatedName,email=updatedEmail,tipo="Usuario"});
            Pass("atualizacao pessoal e login preservados; edicao administrativa existente continua autorizada");

            // Gestor literal so como fixture SQL: nao se cria um fluxo publico de concessao.
            using(var transaction=db.BeginTransaction())
            {
                using var command=new SqlCommand("""
                    SET IDENTITY_INSERT dbo.Usuarios ON;
                    INSERT dbo.Usuarios (Id,Nome,Email,Senha,Tipo,DataCriacao)
                    VALUES (@Gestor,@Marker,@Email,@Password,N'Gestor',SYSDATETIME());
                    SET IDENTITY_INSERT dbo.Usuarios OFF;
                    SET IDENTITY_INSERT dbo.Chamados ON;
                    INSERT dbo.Chamados (Id,Titulo,Descricao,UsuarioId,TecnicoId,CategoriaId,PrioridadeId,StatusId,DataAbertura,DataFechamento)
                    VALUES (@T1,@Marker,@Marker,@A,NULL,1,1,1,'2026-10-01T10:00:00',NULL),
                           (@T2,@Marker,@Marker,@A,2,2,2,2,'2026-10-02T10:00:00',NULL),
                           (@T3,@Marker,@Marker,@B,NULL,3,3,1,'2026-10-03T10:00:00',NULL),
                           (@T4,@Marker,@Marker,@B,2,1,1,3,'2026-10-04T10:00:00','2026-10-04T11:00:00');
                    SET IDENTITY_INSERT dbo.Chamados OFF;
                    """,db,transaction);
                command.Parameters.AddWithValue("@Gestor",gestorId);command.Parameters.AddWithValue("@Marker",marker);
                command.Parameters.AddWithValue("@Email",marker+"gestor@test.invalid");command.Parameters.AddWithValue("@Password",new PasswordHashService().Hash(password));
                command.Parameters.AddWithValue("@A",a);command.Parameters.AddWithValue("@B",b);
                for(var n=1;n<=4;n++) command.Parameters.AddWithValue("@T"+n,firstTicket-(n-1));
                await command.ExecuteNonQueryAsync();transaction.Commit();fixtureCreated=true;
            }
            var gestor=await Login(marker+"gestor@test.invalid",password);
            Check(gestor.Role=="Gestor","Fixture de Gestor nao autenticou com o perfil real.");
            var allTickets=new List<Ticket>();
            using(var command=new SqlCommand("SELECT Id,UsuarioId,StatusId,PrioridadeId,CategoriaId,DataAbertura,TecnicoId FROM dbo.Chamados ORDER BY DataAbertura DESC",db))
            using(var reader=await command.ExecuteReaderAsync())
                while(await reader.ReadAsync()) allTickets.Add(new Ticket(reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4),reader.GetDateTime(5),reader.IsDBNull(6)?null:reader.GetInt32(6)));

            async Task<int[]> List(string path,string token)
            {
                var response=(await Request(HttpMethod.Get,path,token,HttpStatusCode.OK))!.Value;
                return response.EnumerateArray().Select(c=>c.GetProperty("id").GetInt32()).ToArray();
            }
            Check((await List("/api/chamados",loginA.Token)).SequenceEqual(allTickets.Where(t=>t.Owner==a).Select(t=>t.Id)),"Usuario A recebeu chamados alheios.");
            Check((await List("/api/chamados",loginB.Token)).SequenceEqual(allTickets.Where(t=>t.Owner==b).Select(t=>t.Id)),"Usuario B recebeu chamados alheios.");
            Check((await List("/api/chamados",normal.Token)).SequenceEqual(allTickets.Where(t=>t.Owner==1).Select(t=>t.Id)),"Solicitante original recebeu chamados temporarios alheios.");
            Pass("listagem geral restringe tres solicitantes diferentes aos respectivos chamados");

            Check((await List("/api/chamados?categoria=3&prioridade=3&status=1",loginA.Token)).Length==0,"Filtro de chamado alheio rompeu isolamento.");
            Check((await List($"/api/chamados?usuarioId={b}",loginA.Token)).SequenceEqual(allTickets.Where(t=>t.Owner==a).Select(t=>t.Id)),"Parametro nao suportado permitiu trocar solicitante.");
            await Request(HttpMethod.Get,$"/api/chamados/{firstTicket-2}",loginA.Token,HttpStatusCode.Forbidden);
            await Request(HttpMethod.Get,$"/api/chamados/{firstTicket-2}/comentarios",loginA.Token,HttpStatusCode.Forbidden);
            await Request(HttpMethod.Get,$"/api/chamados/{firstTicket}",loginA.Token,HttpStatusCode.OK);
            Check((await List("/api/chamados/meus",loginA.Token)).SequenceEqual(allTickets.Where(t=>t.Owner==a).Select(t=>t.Id)),"Listagem /meus mudou.");
            Pass("filtros e manipulacao de query string nao burlam propriedade; detalhe proprio e /meus preservados");

            foreach(var token in new[]{technician.Token,gestor.Token,administrator.Token})
                Check((await List("/api/chamados",token)).SequenceEqual(allTickets.Select(t=>t.Id)),"Perfil privilegiado perdeu listagem legitimamente necessaria.");
            Check((await List("/api/chamados/meus-atendimentos",technician.Token)).SequenceEqual(allTickets.Where(t=>t.Technician==2).Select(t=>t.Id)),"Atendimentos do tecnico divergiram.");
            Pass("Tecnico, Gestor literal e Admin mantem consultas gerais; painel usa a mesma rota");

            var filters=new (int? Status,int? Priority,int? Category)[]
            {
                (null,null,null),(1,null,null),(2,null,null),(3,null,null),(null,1,null),
                (null,2,null),(null,3,null),(null,null,1),(null,null,2),(null,null,3),
                (null,null,4),(2,2,2),(1,3,3),(3,1,1),(int.MaxValue,null,null)
            };
            foreach(var (token,owner) in new (string,int?)[]{(loginA.Token,a),(loginB.Token,b),(normal.Token,1),(technician.Token,null),(gestor.Token,null),(administrator.Token,null)})
            foreach(var (status,priority,category) in filters)
            {
                var expected=allTickets.Where(t=>(!owner.HasValue || t.Owner==owner) && (!status.HasValue || t.Status==status) && (!priority.HasValue || t.Priority==priority) && (!category.HasValue || t.Category==category));
                var ids=await List($"/api/chamados?status={status}&prioridade={priority}&categoria={category}",token);
                Check(ids.SequenceEqual(expected.Select(t=>t.Id)),"Filtro/ordenacao mudou ou expande visibilidade.");
            }
            Pass("90 listagens HTTP validam filtros simples/combinados e ordenacao por abertura");
            await Request(HttpMethod.Get,"/api/chamados",null,HttpStatusCode.Unauthorized);
            await Request(HttpMethod.Get,"/api/chamados?status=1",null,HttpStatusCode.Unauthorized);
            Pass("sem autenticacao a listagem retorna 401, com ou sem filtros");
            await Request(HttpMethod.Put,$"/api/chamados/{firstTicket-2}/assumir",technician.Token,HttpStatusCode.NoContent);
            Check((await List("/api/chamados/meus-atendimentos",technician.Token)).Contains(firstTicket-2),"Tecnico nao consultou chamado assumido.");
            await Request(HttpMethod.Put,$"/api/chamados/{firstTicket-2}/finalizar",technician.Token,HttpStatusCode.NoContent);
            var finished=(await Request(HttpMethod.Get,$"/api/chamados/{firstTicket-2}",loginB.Token,HttpStatusCode.OK))!.Value;
            Check(finished.GetProperty("statusId").GetInt32()==3,"Finalizacao existente sofreu regressao.");
            Pass("tecnico continua assumindo/finalizando e o solicitante acompanha seu chamado");
        }
        finally
        {
            if(fixtureCreated)
            {
                using var cleanup=new SqlCommand("DELETE dbo.Chamados WHERE Id BETWEEN @Lowest AND @Highest AND Descricao=@Marker; DELETE dbo.Usuarios WHERE Id=@Gestor AND Nome=@Marker;",db);
                cleanup.Parameters.AddWithValue("@Lowest",firstTicket-3);cleanup.Parameters.AddWithValue("@Highest",firstTicket);
                cleanup.Parameters.AddWithValue("@Gestor",gestorId);cleanup.Parameters.AddWithValue("@Marker",marker);
                await cleanup.ExecuteNonQueryAsync();
            }
            foreach(var user in createdUsers)
            {
                using var cleanup=new SqlCommand("DELETE dbo.Usuarios WHERE Id=@Id AND Nome LIKE @Marker;",db);
                cleanup.Parameters.AddWithValue("@Id",user);cleanup.Parameters.AddWithValue("@Marker",marker+"%");
                await cleanup.ExecuteNonQueryAsync();
            }
            Check(await Snapshot()==original,"Dados originais divergiram apos teste/limpeza.");
            Pass("dados originais identicos e temporarios removidos");
        }
        Console.WriteLine($"Concluido: {passed} grupos de controle de acesso aprovados.");
    }

    private sealed record Ticket(int Id,int Owner,int Status,int Priority,int Category,DateTime Opened,int? Technician);
}
