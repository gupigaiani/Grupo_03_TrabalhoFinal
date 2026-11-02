using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TechHelpSolutions.Services;

internal static class PasswordTests
{
    public static async Task Run(string connectionString, HttpClient http)
    {
        var h = new PasswordHashService(); var passed = 0;
        void Check(bool value) { if (!value) throw new InvalidOperationException("Verificacao de senha falhou (dados omitidos)."); }
        void Pass(string text) => Console.WriteLine($"OK senhas {++passed}: {text}");
        const string password = "TesteTemporarioNP2!";
        var hash = h.Hash(password); var other = h.Hash(password);
        Check(hash != other && h.Verify(password, hash) && !h.Verify(password + "x", hash));
        Check(!h.NeedsRehash(hash) && h.IdentifyFormat(hash) == "PBKDF2-SHA256-v1");
        Pass("salt distinto, senha correta/incorreta e metadados");
        foreach (var bad in new string?[] { null, "", password, "THS$1$PBKDF2-SHA256$999999999$AA$AA", hash + "$x", hash.Replace("600000", "-1"), "THS$1$PBKDF2-SHA256$600000$!!!$!!!" })
            Check(!h.Verify(password, bad));
        Check(h.NeedsRehash("invalido") && h.IdentifyFormat("THS$9$X") == "Invalid");
        var oldSalt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var oldKey = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(password, oldSalt, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        var oldHash = "THS$1$PBKDF2-SHA256$100000$" + Convert.ToBase64String(oldSalt) + "$" + Convert.ToBase64String(oldKey);
        Check(h.Verify(password, oldHash) && h.NeedsRehash(oldHash));
        Pass("legado, custos abusivos e formatos malformados rejeitados");
        using var db = new SqlConnection(connectionString); await db.OpenAsync();
        async Task<string> Snapshot()
        {
            var values = new List<string>();
            foreach (var table in new[] { "Usuarios", "Categorias", "Prioridades", "Status", "Chamados", "Comentarios", "__EFMigrationsHistory" })
            {
                var key = table == "__EFMigrationsHistory" ? "MigrationId" : "Id";
                using var c = new SqlCommand($"SELECT (SELECT * FROM dbo.[{table}] ORDER BY [{key}] FOR JSON PATH,INCLUDE_NULL_VALUES)", db);
                values.Add((string)(await c.ExecuteScalarAsync())!);
            }
            return string.Join("\n", values);
        }
        var before = await Snapshot(); var ids = new List<int>(); var marker = "NP2_HASH_" + Guid.NewGuid().ToString("N");
        async Task<JsonElement?> Request(HttpMethod method, string path, HttpStatusCode status, object? body = null, string? token = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body != null) request.Content = JsonContent.Create(body);
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request); Check(response.StatusCode == status);
            var text = await response.Content.ReadAsStringAsync();
            Check(!text.Contains("THS$", StringComparison.Ordinal));
            if (text.Length == 0) return null;
            try { using var doc = JsonDocument.Parse(text); return doc.RootElement.Clone(); } catch (JsonException) { return null; }
        }
        async Task<string> Stored(int id)
        {
            using var c = new SqlCommand("SELECT Senha FROM dbo.Usuarios WHERE Id=@Id", db); c.Parameters.AddWithValue("@Id", id);
            return (string)(await c.ExecuteScalarAsync())!;
        }
        async Task SetStored(int id, string value)
        {
            Check(ids.Contains(id));
            using var c = new SqlCommand("UPDATE dbo.Usuarios SET Senha=@Value WHERE Id=@Id", db);
            c.Parameters.AddWithValue("@Id", id); c.Parameters.AddWithValue("@Value", value); await c.ExecuteNonQueryAsync();
        }
        async Task<string> Login(string email, string pwd)
        {
            var result = (await Request(HttpMethod.Post, "/api/auth/login", HttpStatusCode.OK, new { email, senha = pwd }))!.Value;
            return result.GetProperty("token").GetString()!;
        }
        try
        {
            foreach (var role in new[] { ("usuario@gmail.com", "Usuario"), ("tecnico@gmail.com", "Tecnico"), ("gestor@gmail.com", "Admin") })
            {
                var result = (await Request(HttpMethod.Post, "/api/auth/login", HttpStatusCode.OK, new { email = role.Item1, senha = "Senha123" }))!.Value;
                Check(result.GetProperty("usuario").GetProperty("tipo").GetString() == role.Item2);
            }
            Pass("tres usuarios migrados autenticam com perfis originais");
            for (var n = 0; n < 2; n++)
            {
                var result = (await Request(HttpMethod.Post, "/api/usuarios", HttpStatusCode.OK, new { nome = marker, email = marker + n + "@test.invalid", senha = password, tipo = "Usuario" }))!.Value;
                ids.Add(result.GetProperty("id").GetInt32());
                Check(!result.TryGetProperty("senha", out _));
            }
            var first = await Stored(ids[0]); var second = await Stored(ids[1]);
            Check(first != second && h.Verify(password, first) && h.Verify(password, second));
            Pass("cadastros HTTP armazenam hashes distintos e nao retornam senha");
            var email = marker + "0@test.invalid"; var token = await Login(email, password);
            await Request(HttpMethod.Post, "/api/auth/login", HttpStatusCode.Unauthorized, new { email, senha = password + "incorreta" });
            Pass("login HTTP correto e rejeicao de senha incorreta");
            await Request(HttpMethod.Put, "/api/usuarios/" + ids[0], HttpStatusCode.OK, new { nome = marker + " atualizado", email, tipo = "Usuario" }, token);
            Check(await Stored(ids[0]) == first);
            Pass("atualizacao pessoal sem senha preserva hash byte a byte");
            await Request(HttpMethod.Put, "/api/usuarios/" + ids[0], HttpStatusCode.OK, new { nome = marker, email, tipo = "Usuario", senha = password + "Nova" }, token);
            Check(h.Verify(password + "Nova", await Stored(ids[0])) && !h.Verify(password, await Stored(ids[0])));
            token = await Login(email, password + "Nova");
            await Request(HttpMethod.Post, "/api/auth/login", HttpStatusCode.Unauthorized, new { email, senha = password });
            Pass("alteracao de senha preserva login e revoga senha antiga");
            foreach (var value in new[] { "THS$malformado", password })
            {
                await SetStored(ids[0], value);
                await Request(HttpMethod.Post, "/api/auth/login", HttpStatusCode.Unauthorized, new { email, senha = password });
            }
            await SetStored(ids[0], h.Hash(password + "Nova"));
            Pass("login HTTP rejeita hash malformado e senha legada sem fallback");
            foreach (var path in new[] { "/api/usuarios", "/api/usuarios/me", "/api/chamados", "/api/chamados/1", "/api/chamados/1/comentarios" })
            {
                // Usuario temporario nao possui o chamado original: autorizacao deve prevalecer.
                var status = path.Contains("/chamados/1") ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
                var result = await Request(HttpMethod.Get, path, status, token: token);
                if (result.HasValue) Check(!result.Value.ToString().Contains("\"senha\"", StringComparison.OrdinalIgnoreCase));
            }
            Pass("respostas publicas sem hash e autorizacao preservada");
        }
        finally
        {
            foreach (var id in ids)
            {
                using var c = new SqlCommand("DELETE dbo.Usuarios WHERE Id=@Id AND Nome LIKE @Marker", db);
                c.Parameters.AddWithValue("@Id", id); c.Parameters.AddWithValue("@Marker", marker + "%"); await c.ExecuteNonQueryAsync();
            }
        }
        Check(before == await Snapshot()); Pass("baseline pos-migracao preservado; fixtures removidas");
        Console.WriteLine($"Concluido: {passed} grupos de senhas aprovados.");
    }
}
