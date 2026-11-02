namespace TechHelpSolutions.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Email { get; set; }
        public string Senha { get; set; }
        public string Tipo { get; set; } 
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public ICollection<Chamado> ChamadosAbertos { get; set; }
        public ICollection<Chamado> ChamadosAtendidos { get; set; }
    }
}
