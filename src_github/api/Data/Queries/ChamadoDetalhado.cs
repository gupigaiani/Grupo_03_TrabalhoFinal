namespace TechHelpSolutions.Data.Queries;

// Modelo de leitura sem chave; nao substitui a entidade de escrita Chamado.
public sealed class ChamadoDetalhado
{
    public int Id { get; set; }
    public string Titulo { get; set; } = null!;
    public string Descricao { get; set; } = null!;
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = null!;
    public string UsuarioEmail { get; set; } = null!;
    public string UsuarioTipo { get; set; } = null!;
    public int? TecnicoId { get; set; }
    public string? TecnicoNome { get; set; }
    public string? TecnicoEmail { get; set; }
    public string? TecnicoTipo { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = null!;
    public int PrioridadeId { get; set; }
    public string PrioridadeNome { get; set; } = null!;
    public int StatusId { get; set; }
    public string StatusNome { get; set; } = null!;
    public DateTime DataAbertura { get; set; }
    public DateTime? DataFechamento { get; set; }
    public double? DuracaoMinutos { get; set; }
}
