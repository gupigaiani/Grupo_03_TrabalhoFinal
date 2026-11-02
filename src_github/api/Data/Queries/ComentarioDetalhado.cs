namespace TechHelpSolutions.Data.Queries;

public sealed class ComentarioDetalhado
{
    public int Id { get; set; }
    public int ChamadoId { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = null!;
    public string UsuarioEmail { get; set; } = null!;
    public string UsuarioTipo { get; set; } = null!;
    public string Mensagem { get; set; } = null!;
    public DateTime DataEnvio { get; set; }
}
