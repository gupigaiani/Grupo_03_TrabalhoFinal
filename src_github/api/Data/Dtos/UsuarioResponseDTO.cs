namespace TechHelpSolutions.DTOs
{
    public class UsuarioResponseDTO
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Email { get; set; }
        public string Tipo { get; set; }
        public DateTime DataCriacao { get; set; }
    }
}
