using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TechHelpSolutions.Data;

internal static class TriggerTests
{
    public static async Task Run(string connectionString, HttpClient http)
    {
        var passed = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Pass(string message) { Console.WriteLine($"OK trigger {++passed}: {message}"); }
        using var db = new SqlConnection(connectionString);
        await db.OpenAsync();
        var id = -Random.Shared.Next(1_000_000_001, 2_000_000_000);
        var marker = "NP2_TRIGGER_" + Guid.NewGuid().ToString("N");
        var created = false;
        int? httpId = null;
        var opening = new DateTime(2026,1,1);
        SqlCommand Command(string sql, SqlTransaction? transaction = null)
        {
            var command = new SqlCommand(sql,db,transaction) {CommandTimeout=15};
            command.Parameters.AddWithValue("@One",id);
            command.Parameters.AddWithValue("@Two",id-1);
            command.Parameters.AddWithValue("@Three",id-2);
            command.Parameters.AddWithValue("@Four",id-3);
            command.Parameters.AddWithValue("@Marker",marker);
            command.Parameters.Add("@Open",SqlDbType.DateTime2).Value=opening;
            command.Parameters["@Open"].Scale=7;
            return command;
        }
        async Task Execute(string sql, SqlTransaction? transaction = null)
        { using var command=Command(sql,transaction); await command.ExecuteNonQueryAsync(); }
        async Task<int> Count(string sql)
        { using var command=Command(sql); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
        async Task Error(Func<Task> operation)
        {
            try {await operation();}
            catch (SqlException ex) when (ex.Errors.Cast<SqlError>().Any(e=>e.Number==51201)) {return;}
            throw new InvalidOperationException("Era esperado erro 51201 da trigger.");
        }
        async Task IdentityInsert(string sql)
        {
            // SET e INSERT no mesmo batch parametrizado (mesmo escopo sp_executesql).
            try {await Execute("SET IDENTITY_INSERT dbo.Chamados ON;\n"+sql+"\nSET IDENTITY_INSERT dbo.Chamados OFF;");}
            finally {await Execute("SET IDENTITY_INSERT dbo.Chamados OFF;");}
        }
        async Task<string> Snapshot()
        {
            var values=new List<string>();
            foreach (var table in new[]{"Usuarios","Categorias","Prioridades","Status","Chamados","Comentarios","__EFMigrationsHistory"})
            {
                var key=table=="__EFMigrationsHistory" ? "MigrationId" : "Id";
                using var command=new SqlCommand($"SELECT (SELECT * FROM dbo.[{table}] ORDER BY [{key}] FOR JSON PATH,INCLUDE_NULL_VALUES)",db);
                values.Add((string)(await command.ExecuteScalarAsync())!);
            }
            return string.Join("\n",values);
        }
        const string insert="INSERT dbo.Chamados (Id,Titulo,Descricao,UsuarioId,CategoriaId,PrioridadeId,StatusId,DataAbertura,DataFechamento) VALUES ";
        var original=await Snapshot();
        try
        {
            Check(await Count("""
                SELECT COUNT(*) FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.trg_Chamados_ValidarDatas')
                AND parent_id=OBJECT_ID(N'dbo.Chamados') AND is_disabled=0 AND is_instead_of_trigger=0
                AND (SELECT COUNT(*) FROM sys.trigger_events WHERE object_id=OBJECT_ID(N'dbo.trg_Chamados_ValidarDatas')
                     AND type_desc IN ('INSERT','UPDATE'))=2;
                """)==1,"Trigger ausente ou eventos/configuracao incorretos.");
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE DataFechamento < DataAbertura")==0,"Dados preexistentes invalidos.");
            Pass("trigger habilitada AFTER INSERT/UPDATE e base inicialmente integra");
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id IN (@One,@Two,@Three,@Four)")==0,"Colisao de IDs temporarios.");
            await IdentityInsert(insert+"(@One,@Marker,@Marker,1,1,1,1,@Open,DATEADD(day,1,@Open)),(@Two,@Marker,@Marker,1,1,1,1,@Open,NULL);");
            created=true;
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id IN (@One,@Two)")==2,"INSERT valido falhou.");
            Pass("INSERT valido e INSERT com fechamento NULL");
            await Error(()=>IdentityInsert(insert+"(@Three,@Marker,@Marker,1,1,1,1,@Open,DATEADD(nanosecond,-100,@Open));"));
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id=@Three")==0,"INSERT invalido foi persistido.");
            Pass("INSERT invalido por 100 ns rejeitado sem persistencia");
            await Execute("UPDATE dbo.Chamados SET DataFechamento=DataAbertura WHERE Id=@One;");
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id=@One AND DataFechamento=DataAbertura")==1,"Igualdade deve ser aceita.");
            await Execute("UPDATE dbo.Chamados SET DataFechamento=DATEADD(day,2,DataAbertura) WHERE Id=@One;");
            Pass("UPDATE valido, inclusive fechamento igual a abertura");
            await Error(()=>Execute("UPDATE dbo.Chamados SET DataFechamento=DATEADD(day,-1,DataAbertura),Descricao=N'nao deve persistir' WHERE Id=@One;"));
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id=@One AND DataFechamento=DATEADD(day,2,DataAbertura) AND Descricao=@Marker")==1,"UPDATE invalido deixou alteracao parcial.");
            Pass("UPDATE invalido rejeitado e todas as colunas restauradas");
            await Execute("UPDATE dbo.Chamados SET DataFechamento=NULL WHERE Id IN (@One,@Two);");
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id IN (@One,@Two) AND DataFechamento IS NULL")==2,"UPDATE NULL falhou.");
            Pass("UPDATE com fechamento NULL em multiplas linhas");
            await Error(()=>IdentityInsert(insert+"(@Three,@Marker,@Marker,1,1,1,1,@Open,@Open),(@Four,@Marker,@Marker,1,1,1,1,@Open,DATEADD(day,-1,@Open));"));
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id IN (@Three,@Four)")==0,"Lote INSERT foi parcialmente gravado.");
            await Error(()=>Execute("UPDATE dbo.Chamados SET DataFechamento=CASE WHEN Id=@One THEN DataAbertura ELSE DATEADD(day,-1,DataAbertura) END,Descricao=N'lote invalido' WHERE Id IN (@One,@Two);"));
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id IN (@One,@Two) AND DataFechamento IS NULL AND Descricao=@Marker")==2,"Lote UPDATE foi parcialmente gravado.");
            Pass("INSERT/UPDATE em lote com um invalido rejeitam o lote inteiro");

            using(var transaction=db.BeginTransaction())
            {
                await Execute("UPDATE dbo.Chamados SET Descricao=N'rollback externo' WHERE Id=@One",transaction);
                await Error(()=>Execute("UPDATE dbo.Chamados SET DataFechamento=DATEADD(day,-1,DataAbertura) WHERE Id=@Two",transaction));
                if(transaction.Connection!=null) transaction.Rollback();
            }
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id=@One AND Descricao=@Marker")==1,"Transacao externa deixou escrita parcial.");
            Check(await Count("SELECT @@TRANCOUNT")==0,"Transacao pendente.");
            Pass("rollback da transacao externa, incluindo escrita anterior ao erro");

            using(var context=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options))
            {
                var entities=await context.Chamados.Where(c=>c.Id==id || c.Id==id-1).OrderByDescending(c=>c.Id).ToListAsync();
                entities[0].Descricao="primeira escrita EF";
                entities[0].DataFechamento=opening;
                entities[1].DataFechamento=opening.AddTicks(-1);
                try {await context.SaveChangesAsync(); throw new InvalidOperationException("EF deveria rejeitar lote invalido.");}
                catch(DbUpdateException ex) when(ex.GetBaseException() is SqlException sql && sql.Errors.Cast<SqlError>().Any(e=>e.Number==51201)) {}
            }
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id IN (@One,@Two) AND Descricao=@Marker AND DataFechamento IS NULL")==2,"SaveChanges deixou escrita parcial.");
            Pass("SaveChanges com multiplas entidades sofre rollback sem erro de OUTPUT");

            await Execute("UPDATE dbo.Chamados SET TecnicoId=2,StatusId=2 WHERE Id=@One");
            await Error(()=>Execute("""
                EXEC dbo.usp_FinalizarChamado @ChamadoId=@One,@TecnicoId=2,@TecnicoIdEsperado=2,
                @StatusIdEsperado=2,@DataFechamentoEsperada=NULL,@DataFechamento='2025-12-31';
                """));
            Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id=@One AND TecnicoId=2 AND StatusId=2 AND DataFechamento IS NULL")==1,"Procedure deixou status parcial.");
            Pass("trigger na procedure invalida desfaz status e fechamento juntos");

            async Task<string> Login(string email)
            {
                using var response=await http.PostAsJsonAsync("/api/auth/login",new{email,senha="Senha123"});
                response.EnsureSuccessStatusCode();
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return json.RootElement.GetProperty("token").GetString()!;
            }
            var user=await Login("usuario@gmail.com");
            var tech=await Login("tecnico@gmail.com");
            var admin=await Login("gestor@gmail.com");
            async Task<string> Request(HttpMethod method,string path,string token,HttpStatusCode expected,object? payload=null)
            {
                using var request=new HttpRequestMessage(method,path);
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
                if(payload!=null) request.Content=JsonContent.Create(payload);
                using var response=await http.SendAsync(request);
                var body=await response.Content.ReadAsStringAsync();
                Check(response.StatusCode==expected,$"{method} {path}: esperado {(int)expected}, recebido {(int)response.StatusCode}: {body}");
                if(expected==HttpStatusCode.NoContent) Check(body.Length==0,"204 deveria continuar sem corpo.");
                return body;
            }
            var createdJson=await Request(HttpMethod.Post,"/api/chamados",user,HttpStatusCode.OK,new{titulo=marker,descricao=marker,categoriaId=1,prioridadeId=1});
            using(var json=JsonDocument.Parse(createdJson))
            {
                httpId=json.RootElement.GetProperty("id").GetInt32();
                Check(httpId>0 && json.RootElement.GetProperty("dataFechamento").ValueKind==JsonValueKind.Null,"Identidade/fechamento na criacao incorretos.");
            }
            Pass("HTTP cria chamado pelo EF e recupera IDENTITY com trigger habilitada");
            await Request(HttpMethod.Put,$"/api/chamados/{httpId}",user,HttpStatusCode.OK,new{titulo=marker+" editado",descricao=marker,categoriaId=2,prioridadeId=2});
            await Request(HttpMethod.Put,$"/api/chamados/{httpId}/assumir",tech,HttpStatusCode.NoContent);
            await Request(HttpMethod.Put,$"/api/chamados/{httpId}/finalizar",tech,HttpStatusCode.NoContent);
            using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.Chamados WHERE Id=@Id AND StatusId=3 AND TecnicoId=2 AND DataFechamento>=DataAbertura",db))
            {
                command.Parameters.AddWithValue("@Id",httpId.Value);
                Check(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"Estado HTTP apos finalizacao invalido.");
            }
            Pass("HTTP edita, assume e finaliza com EF/procedures e trigger ativos");
            // A API nao recebe datas no DTO. Fixture futura valida reproduz falha ao fechar agora.
            using(var command=Command("UPDATE dbo.Chamados SET DataAbertura=@Future,DataFechamento=NULL,TecnicoId=2,StatusId=2 WHERE Id=@Two"))
            { command.Parameters.Add("@Future",SqlDbType.DateTime2).Value=DateTime.Now.AddDays(2); await command.ExecuteNonQueryAsync(); }
            const string publicMessage="A data de fechamento não pode ser anterior à data de abertura.";
            foreach(var action in new[]{"finalizar","status"})
            {
                var body=await Request(HttpMethod.Put,$"/api/chamados/{id-1}/{action}",tech,HttpStatusCode.BadRequest,action=="status" ? new{statusId=3} : null);
                // Aceita texto puro ou string JSON conforme negociacao de conteudo do MVC.
                Check(body==publicMessage || (body.StartsWith('"') && JsonSerializer.Deserialize<string>(body)==publicMessage),"Resposta publica nao corresponde a mensagem fixa.");
                Check(!body.Contains("51201") && !body.Contains("SqlException") && !body.Contains("trg_Chamados"),"Detalhe SQL vazou na resposta.");
                Check(await Count("SELECT COUNT(*) FROM dbo.Chamados WHERE Id=@Two AND StatusId=2 AND TecnicoId=2 AND DataFechamento IS NULL")==1,"HTTP invalido deixou gravacao parcial.");
            }
            Pass("HTTP 400 seguro tanto para SqlException da procedure quanto DbUpdateException do EF");
            await Request(HttpMethod.Delete,$"/api/chamados/{httpId}",admin,HttpStatusCode.NoContent);
            using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.Chamados WHERE Id=@Id",db))
            {command.Parameters.AddWithValue("@Id",httpId.Value); Check(Convert.ToInt32(await command.ExecuteScalarAsync())==0,"DELETE HTTP nao removeu temporario.");}
            httpId=null;
            Pass("HTTP exclui chamado temporario com trigger habilitada");
        }
        finally
        {
            if(created) await Execute("DELETE dbo.Chamados WHERE Id IN (@One,@Two,@Three,@Four) AND Descricao=@Marker");
            if(httpId.HasValue)
            {
                using var command=new SqlCommand("DELETE dbo.Chamados WHERE Id=@Id AND Descricao=@Marker",db);
                command.Parameters.AddWithValue("@Id",httpId.Value); command.Parameters.AddWithValue("@Marker",marker);
                await command.ExecuteNonQueryAsync();
            }
            Check(await Snapshot()==original,"Dados originais divergiram apos os testes.");
            Pass("dados originais identicos e todos os registros temporarios removidos");
        }
        Console.WriteLine($"Concluido: {passed} grupos de trigger aprovados.");
    }
}
