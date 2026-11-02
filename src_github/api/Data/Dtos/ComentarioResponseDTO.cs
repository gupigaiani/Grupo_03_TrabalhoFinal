namespace TechHelpSolutions.DTOs
{
    public class ComentarioResponseDTO
    {
        public int Id { get; set; }
        public int ChamadoId { get; set; }
        public string Mensagem { get; set; }
        public DateTime DataEnvio { get; set; }
        public UsuarioResumoDTO Usuario { get; set; }
    }
}
