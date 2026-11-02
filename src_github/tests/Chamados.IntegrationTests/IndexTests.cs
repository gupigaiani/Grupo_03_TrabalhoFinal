using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TechHelpSolutions.Data;
using TechHelpSolutions.Services;

// Captura somente SELECTs reais do EF. Nao altera dados nem forca indices/planos.
internal static class IndexTests
{
    public static async Task Run(string connectionString, string phase)
    {
        if (phase is not ("antes" or "depois")) throw new ArgumentException("Use --fase=antes ou --fase=depois.");
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../evidencias/indices"));
        var directory = Path.Combine(root,phase);
        // Preserva a primeira captura como evidencia; nao sobrescreve a linha de base.
        if (Directory.Exists(directory)) throw new InvalidOperationException($"Pasta de captura ja existe: {directory}");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        async Task<string> Scalar(string sql)
        {
            using var command = new SqlCommand(sql,connection);
            return Convert.ToString(await command.ExecuteScalarAsync(),CultureInfo.InvariantCulture)!;
        }
        var count = int.Parse(await Scalar("SELECT COUNT(*) FROM sys.indexes WHERE name IN ('IX_Chamados_UsuarioId_DataAbertura','IX_Chamados_TecnicoId_DataAbertura','IX_Comentarios_ChamadoId_DataEnvio')"));
        if ((phase=="antes" && count!=0) || (phase=="depois" && count!=3))
            throw new InvalidOperationException("Indices nao correspondem a fase de captura informada.");
        async Task<string> DataHash()
        {
            var buffer = new StringBuilder();
            foreach(var table in new[]{"Usuarios","Categorias","Prioridades","Status","Chamados","Comentarios","__EFMigrationsHistory"})
            {
                var key=table=="__EFMigrationsHistory" ? "MigrationId" : "Id";
                buffer.Append(await Scalar($"SELECT (SELECT * FROM dbo.[{table}] ORDER BY [{key}] FOR JSON PATH,INCLUDE_NULL_VALUES)"));
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(buffer.ToString())));
        }
        var original=await DataHash();
        var interceptor=new Capture();
        using var context=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).AddInterceptors(interceptor).Options);
        var service=new ChamadoService(context);
        var queries=new List<Query>();
        async Task Record<T>(string name,IQueryable<T> query)
        {
            interceptor.Last=null;
            await query.ToListAsync();
            var captured=interceptor.Last ?? throw new InvalidOperationException("EF nao executou consulta.");
            queries.Add(captured with{Name=name,ReplaySql=query.ToQueryString()});
        }
        var userId=1; var technicianId=2; var statusId=2; var ticketId=1; var priorityId=2; var categoryId=2;
        await Record("01_meus",service.ConsultarChamadosDetalhados().Where(c=>c.UsuarioId==userId).OrderByDescending(c=>c.DataAbertura));
        await Record("02_atendimentos",service.ConsultarChamadosDetalhados().Where(c=>c.TecnicoId==technicianId).OrderByDescending(c=>c.DataAbertura));
        await Record("03_comentarios",service.ConsultarComentariosDetalhados().Where(c=>c.ChamadoId==ticketId).OrderBy(c=>c.DataEnvio));
        await Record("04_status",service.ConsultarChamadosDetalhados().Where(c=>c.StatusId==statusId).OrderByDescending(c=>c.DataAbertura));
        await Record("05_filtros_combinados",service.ConsultarChamadosDetalhados().Where(c=>c.StatusId==statusId && c.PrioridadeId==priorityId && c.CategoriaId==categoryId).OrderByDescending(c=>c.DataAbertura));
        await Record("06_todos",service.ConsultarChamadosDetalhados().OrderByDescending(c=>c.DataAbertura));
        await Record("07_detalhe",context.Chamados.AsNoTracking().Include(c=>c.Usuario).Include(c=>c.Tecnico)
            .Include(c=>c.Categoria).Include(c=>c.Prioridade).Include(c=>c.Status)
            .Include(c=>c.Comentarios).ThenInclude(c=>c.Usuario).Where(c=>c.Id==ticketId).Take(1));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"inventario.json"),await Scalar("""
            SELECT t.name AS Tabela,i.name AS Indice,i.type_desc,i.is_primary_key,i.is_unique,i.is_disabled,i.has_filter,
                   ic.key_ordinal,ic.is_descending_key,ic.is_included_column,c.name AS Coluna
            FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE t.is_ms_shipped=0 ORDER BY t.name,i.index_id,ic.index_column_id FOR JSON PATH;
            """));
        await File.WriteAllTextAsync(Path.Combine(directory,"ambiente.json"),await Scalar("""
            SELECT CONVERT(varchar(30),SERVERPROPERTY('ProductVersion')) AS Versao,
                   CONVERT(nvarchar(128),SERVERPROPERTY('Edition')) AS Edicao,
                   (SELECT compatibility_level FROM sys.databases WHERE name=DB_NAME()) AS Compatibilidade,
                   (SELECT COUNT(*) FROM dbo.Chamados) AS Chamados,
                   (SELECT COUNT(*) FROM dbo.Comentarios) AS Comentarios,
                   (SELECT COUNT(DISTINCT UsuarioId) FROM dbo.Chamados) AS SolicitantesDistintos,
                   (SELECT COUNT(DISTINCT TecnicoId) FROM dbo.Chamados) AS TecnicosDistintos,
                   (SELECT COUNT(DISTINCT StatusId) FROM dbo.Chamados) AS StatusDistintos
            FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
            """));
        var results=new List<object>();
        var messages=new List<string>();
        connection.InfoMessage+=(_,e)=>messages.Add(e.Message);
        XNamespace ns="http://schemas.microsoft.com/sqlserver/2004/07/showplan";
        foreach(var query in queries)
        {
            await File.WriteAllTextAsync(Path.Combine(directory,query.Name+".sql"),"-- SQL gerado por ToQueryString para reproducao manual.\n-- A captura usa o comando EF parametrizado, nao estes DECLAREs.\n"+query.ReplaySql);
            SqlCommand NewCommand(string prefix,string suffix)
            {
                var command=new SqlCommand(prefix+query.Sql+suffix,connection){CommandTimeout=30};
                foreach(var parameter in query.Parameters) command.Parameters.Add(((ICloneable)parameter).Clone());
                return command;
            }
            messages.Clear();
            var rows=0; var rowValues=new StringBuilder(); XDocument? plan=null;
            using(var command=NewCommand("SET STATISTICS XML ON;\n",";\nSET STATISTICS XML OFF;"))
            using(var reader=await command.ExecuteReaderAsync())
            {
                do
                {
                    while(await reader.ReadAsync())
                    {
                        if(reader.FieldCount==1 && reader.GetName(0).Contains("Showplan",StringComparison.OrdinalIgnoreCase))
                            plan=XDocument.Parse(reader.GetString(0));
                        else
                        {
                            rows++; var values=new object[reader.FieldCount]; reader.GetValues(values);
                            rowValues.Append(JsonSerializer.Serialize(values));
                        }
                    }
                }while(await reader.NextResultAsync());
            }
            if(plan==null || !plan.Descendants(ns+"RunTimeCountersPerThread").Any()) throw new InvalidOperationException("Plano real nao foi retornado.");
            plan.Save(Path.Combine(directory,query.Name+".sqlplan"));
            // Cinco execucoes instrumentadas apos aquecimento, sem limpeza de cache.
            messages.Clear();
            for(var repeat=1;repeat<=5;repeat++)
            {
                messages.Add($"EXECUCAO {repeat} (apos aquecimento; STATISTICS IO/TIME reais)");
                using var command=NewCommand("SET STATISTICS IO ON; SET STATISTICS TIME ON;\n",";\nSET STATISTICS IO OFF; SET STATISTICS TIME OFF;");
                using var reader=await command.ExecuteReaderAsync();
                do {while(await reader.ReadAsync()) { }}while(await reader.NextResultAsync());
            }
            await File.WriteAllTextAsync(Path.Combine(directory,query.Name+"_io_tempo.txt"),string.Join(Environment.NewLine,messages));
            var operators=plan.Descendants(ns+"RelOp").Select(node=>new
            {
                Node=node.Attribute("NodeId")?.Value,
                Physical=node.Attribute("PhysicalOp")?.Value,
                Logical=node.Attribute("LogicalOp")?.Value,
                Lookup=node.Elements(ns+"IndexScan").Any(e=>e.Attribute("Lookup")?.Value is "1" or "true"),
                Objects=node.Elements().SelectMany(e=>e.Elements(ns+"Object")).Select(e=>new{Table=e.Attribute("Table")?.Value,Index=e.Attribute("Index")?.Value}).ToArray(),
                ActualRows=node.Elements(ns+"RunTimeInformation").Elements(ns+"RunTimeCountersPerThread").Select(e=>e.Attribute("ActualRows")?.Value).ToArray()
            }).ToArray();
            var timing=plan.Descendants(ns+"QueryTimeStats").Select(e=>new{CPU=e.Attribute("CpuTime")?.Value,Elapsed=e.Attribute("ElapsedTime")?.Value}).ToArray();
            results.Add(new{Name=query.Name,Rows=rows,ResultHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rowValues.ToString()))),Operators=operators,ActualPlanTimeMs=timing});
            Console.WriteLine($"{phase}: {query.Name}, {rows} linha(s); "+string.Join(", ",operators.GroupBy(o=>o.Physical).Select(g=>$"{g.Key}={g.Count()}")));
        }
        if(await DataHash()!=original) throw new InvalidOperationException("Captura alterou dados.");
        await File.WriteAllTextAsync(Path.Combine(directory,"resumo.json"),JsonSerializer.Serialize(new{Phase=phase,Utc=DateTime.UtcNow,DataHash=original,Results=results},new JsonSerializerOptions{WriteIndented=true}));
        if(phase=="depois")
        {
            using var before=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"antes/resumo.json")));
            using var after=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory,"resumo.json")));
            if(before.RootElement.GetProperty("DataHash").GetString()!=original) throw new InvalidOperationException("Dados divergiram entre antes/depois.");
            var previous=before.RootElement.GetProperty("Results").EnumerateArray().ToArray();
            foreach(var result in after.RootElement.GetProperty("Results").EnumerateArray())
            {
                var match=previous.Single(p=>p.GetProperty("Name").GetString()==result.GetProperty("Name").GetString());
                if(match.GetProperty("ResultHash").GetString()!=result.GetProperty("ResultHash").GetString()) throw new InvalidOperationException("Indice alterou resultado de consulta.");
            }
        }
        Console.WriteLine("Captura concluida com dados preservados: "+directory);
    }

    private sealed record Query(string Name,string Sql,SqlParameter[] Parameters,string ReplaySql);
    private sealed class Capture : DbCommandInterceptor
    {
        public Query? Last {get;set;}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,
            InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
        {
            Last=new Query("",command.CommandText,command.Parameters.Cast<SqlParameter>().Select(p=>(SqlParameter)((ICloneable)p).Clone()).ToArray(),"");
            return new(result);
        }
    }
}
