namespace TechHelpSolutions.Models
{
    public class Comentario
    {
        public int Id { get; set; }
        public int ChamadoId { get; set; }
        public Chamado Chamado { get; set; }
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; }
        public string Mensagem { get; set; }
        public DateTime DataEnvio { get; set; } = DateTime.Now;
    }
}
