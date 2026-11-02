namespace TechHelpSolutions.Models
{
    public class Prioridade
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public ICollection<Chamado> Chamados { get; set; }
    }
}
