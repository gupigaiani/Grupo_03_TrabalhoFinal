using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TechHelpSolutions.Data;
using TechHelpSolutions.DTOs;
using TechHelpSolutions.Models;

namespace TechHelpSolutions.Services;

public enum TipoCatalogo { Categoria, Prioridade, Status }
public enum ResultadoExclusao { Sucesso, NaoEncontrado, Referenciado }

public sealed class AdministracaoService(AppDbContext context)
{
    public async Task<ItemCatalogoDTO?> SalvarCatalogo(TipoCatalogo tipo, int? id, string nome)
    {
        object? item;
        if (id.HasValue)
        {
            item = tipo switch
            {
                TipoCatalogo.Categoria => await context.Categorias.FindAsync(id.Value),
                TipoCatalogo.Prioridade => await context.Prioridades.FindAsync(id.Value),
                TipoCatalogo.Status => await context.Status.FindAsync(id.Value),
                _ => throw new ArgumentOutOfRangeException(nameof(tipo))
            };
            if (item is null) return null;
        }
        else
        {
            item = tipo switch
            {
                TipoCatalogo.Categoria => new Categoria(),
                TipoCatalogo.Prioridade => new Prioridade(),
                TipoCatalogo.Status => new Status(),
                _ => throw new ArgumentOutOfRangeException(nameof(tipo))
            };
            context.Add(item);
        }
        context.Entry(item).Property("Nome").CurrentValue = nome.Trim();
        await context.SaveChangesAsync();
        return new ItemCatalogoDTO { Id = (int)context.Entry(item).Property("Id").CurrentValue!, Nome = nome.Trim() };
    }

    public Task<ResultadoExclusao> ExcluirCatalogo(TipoCatalogo tipo, int id) => tipo switch
    {
        TipoCatalogo.Categoria => Excluir<Categoria>("Categorias", id, () => context.Chamados.AnyAsync(c => c.CategoriaId == id)),
        TipoCatalogo.Prioridade => Excluir<Prioridade>("Prioridades", id, () => context.Chamados.AnyAsync(c => c.PrioridadeId == id)),
        TipoCatalogo.Status => Excluir<Status>("Status", id, () => context.Chamados.AnyAsync(c => c.StatusId == id)),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };

    public Task<ResultadoExclusao> ExcluirUsuario(int id) => Excluir<Usuario>("Usuarios", id, async () =>
        await context.Chamados.AnyAsync(c => c.UsuarioId == id || c.TecnicoId == id)
        || await context.Comentarios.AnyAsync(c => c.UsuarioId == id));

    private async Task<ResultadoExclusao> Excluir<T>(string table, int id, Func<Task<bool>> possuiVinculos) where T : class
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        // Tabelas fixas e id parametrizado, sem concatenar entradas HTTP.
        // XLOCK na linha pai impede nova FK enquanto verificamos vinculos e excluimos.
        // Isso evita que uma corrida acione as cascatas existentes sobre chamados/comentarios.
        FormattableString query = table switch
        {
            "Categorias" => $"SELECT * FROM dbo.Categorias WITH (XLOCK, ROWLOCK) WHERE Id = {id}",
            "Prioridades" => $"SELECT * FROM dbo.Prioridades WITH (XLOCK, ROWLOCK) WHERE Id = {id}",
            "Status" => $"SELECT * FROM dbo.Status WITH (XLOCK, ROWLOCK) WHERE Id = {id}",
            "Usuarios" => $"SELECT * FROM dbo.Usuarios WITH (XLOCK, ROWLOCK) WHERE Id = {id}",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        var item = await context.Set<T>().FromSqlInterpolated(query)
            .SingleOrDefaultAsync();
        if (item is null) return ResultadoExclusao.NaoEncontrado;
        if (await possuiVinculos()) return ResultadoExclusao.Referenciado;
        context.Remove(item);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ResultadoExclusao.Sucesso;
    }

    public static bool ConflitoIntegridade(DbUpdateException exception) => exception.InnerException is SqlException sql
        && sql.Errors.Cast<SqlError>().Any(e => e.Number is 547 or 2601 or 2627);
}
