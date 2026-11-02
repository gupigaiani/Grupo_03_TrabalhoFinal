using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TechHelpSolutions.Services;

var passwords = new PasswordHashService();
try
{
    if (args.Length != 2 || args[0] != "migrate") throw new ArgumentException("Use migrate <arquivo-evidencia.json>.");
    var connectionString = Environment.GetEnvironmentVariable("THS_PASSWORD_CONNECTION")
        ?? "Server=.\\SQLEXPRESS;Database=TechHelpSolutions;Integrated Security=True;TrustServerCertificate=True";
    await using var db = new SqlConnection(connectionString);
    await db.OpenAsync();
    async Task<string> Scalar(string query, SqlTransaction? tx = null)
    {
        using var c = new SqlCommand(query, db, tx) { CommandTimeout = 120 };
        return Convert.ToString(await c.ExecuteScalarAsync())!;
    }
    var tables = new[] { "Usuarios", "Categorias", "Prioridades", "Status", "Chamados", "Comentarios", "__EFMigrationsHistory" };
    async Task<string> Snapshot(bool includePasswords, SqlTransaction? tx = null)
    {
        var buffer = new StringBuilder();
        foreach (var table in tables)
        {
            var columns = table == "Usuarios" && !includePasswords ? "Id,Nome,Email,Tipo,DataCriacao" : "*";
            var key = table == "__EFMigrationsHistory" ? "MigrationId" : "Id";
            buffer.Append(await Scalar($"SELECT (SELECT {columns} FROM dbo.[{table}] ORDER BY [{key}] FOR JSON PATH,INCLUDE_NULL_VALUES)", tx));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(buffer.ToString())));
    }
    async Task<List<(int Id, string Stored)>> Read(SqlTransaction? tx = null)
    {
        using var c = new SqlCommand("SELECT Id,Senha FROM dbo.Usuarios ORDER BY Id", db, tx);
        using var r = await c.ExecuteReaderAsync();
        var rows = new List<(int, string)>();
        while (await r.ReadAsync()) rows.Add((r.GetInt32(0), r.GetString(1)));
        return rows;
    }
    var rows = await Read();
    if (rows.Any(r => passwords.IdentifyFormat(r.Stored) == "Invalid")) throw new InvalidOperationException("Formato reservado invalido; revisar antes da migracao.");
    var legacy = rows.Where(r => passwords.IdentifyFormat(r.Stored) == "Legacy").ToList();
    string? backup = null;
    if (legacy.Count > 0)
    {
        var dir = await Scalar("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))");
        if (string.IsNullOrWhiteSpace(dir)) throw new InvalidOperationException("Diretorio de backup indisponivel.");
        // Caminho do servidor, nao do cliente. Protegido pelas ACLs do SQL Server.
        backup = dir.TrimEnd('\\', '/') + "/THS_before_passwords_" + Guid.NewGuid().ToString("N") + ".bak";
        using var b = new SqlCommand("BACKUP DATABASE [" + db.Database.Replace("]", "]]") + "] TO DISK=@Path WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM;", db) { CommandTimeout = 120 };
        b.Parameters.AddWithValue("@Path", backup);
        await b.ExecuteNonQueryAsync();
    }
    var before = await Snapshot(true);
    var unchanged = await Snapshot(false);
    var definitions = await Scalar("SELECT (SELECT o.name,o.type,m.definition FROM sys.objects o LEFT JOIN sys.sql_modules m ON o.object_id=m.object_id WHERE o.is_ms_shipped=0 ORDER BY o.name FOR JSON PATH,INCLUDE_NULL_VALUES)");
    var changed = new List<int>();
    using (var tx = db.BeginTransaction(IsolationLevel.Serializable))
    {
        // Bloqueia alteracoes dos usuarios durante leitura/conversao/verificacao.
        await Scalar("SELECT COUNT(*) FROM dbo.Usuarios WITH (TABLOCKX,HOLDLOCK)", tx);
        rows = await Read(tx);
        foreach (var row in rows)
        {
            var format = passwords.IdentifyFormat(row.Stored);
            if (format == "Invalid") throw new InvalidOperationException("Formato invalido; migracao cancelada.");
            if (format != "Legacy") continue;
            if (backup is null) throw new InvalidOperationException("Novo legado concorrente sem backup; repita offline.");
            var hash = passwords.Hash(row.Stored);
            if (!passwords.Verify(row.Stored, hash)) throw new InvalidOperationException("Verificacao de conversao falhou.");
            using var c = new SqlCommand("UPDATE dbo.Usuarios SET Senha=@Hash WHERE Id=@Id", db, tx);
            c.Parameters.Add("@Hash", SqlDbType.NVarChar, -1).Value = hash;
            c.Parameters.AddWithValue("@Id", row.Id);
            await c.ExecuteNonQueryAsync(); changed.Add(row.Id);
        }
        if (unchanged != await Snapshot(false, tx)) throw new InvalidOperationException("Dados fora da senha divergiram; rollback.");
        tx.Commit();
    }
    if ((await Read()).Any(r => passwords.IdentifyFormat(r.Stored) != "PBKDF2-SHA256-v1"))
        throw new InvalidOperationException("Formato final invalido.");
    var after = await Snapshot(true);
    var counts = new Dictionary<string, int>();
    foreach (var table in tables) counts[table] = int.Parse(await Scalar($"SELECT COUNT(*) FROM dbo.[{table}]"));
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    // Arquivo agregado: nao inclui dados pessoais, senhas, hashes individuais ou tokens.
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { Utc = DateTime.UtcNow, Database = db.Database,
        Counts = counts, ChangedUserIds = changed, BeforeDataHash = before, AfterDataHash = after,
        NonPasswordDataHash = unchanged, ObjectDefinitionsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definitions))),
        Backup = backup, Algorithm = "PBKDF2-SHA256", Iterations = PasswordHashService.Iterations }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Senhas verificadas; usuarios convertidos: {changed.Count}. Evidencia agregada gravada.");
}
catch (Exception)
{
    // Nao imprimir excecoes que possam conter parametros ou credenciais.
    Console.Error.WriteLine("Manutencao de senhas falhou. Nenhuma credencial foi registrada. Verifique conexao, permissoes, backup e formato; transacao incompleta e revertida.");
    Environment.ExitCode = 1;
}
