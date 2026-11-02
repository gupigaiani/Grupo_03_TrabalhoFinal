using System.Net.NetworkInformation;

namespace TechHelpSolutions.Models
{
    public class Chamado
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; }

        public int? TecnicoId { get; set; }
        public Usuario Tecnico { get; set; }

        public int CategoriaId { get; set; }
        public Categoria Categoria { get; set; }

        public int PrioridadeId { get; set; }
        public Prioridade Prioridade { get; set; }

        public int StatusId { get; set; }
        public Status Status { get; set; }

        public DateTime DataAbertura { get; set; } = DateTime.Now;
        public DateTime? DataFechamento { get; set; }

        public ICollection<Comentario> Comentarios { get; set; }
    }
}
