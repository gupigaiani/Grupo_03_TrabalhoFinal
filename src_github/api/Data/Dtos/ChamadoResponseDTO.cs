namespace TechHelpSolutions.DTOs
{
    public class ChamadoResponseDTO
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public int UsuarioId { get; set; }
        public int? TecnicoId { get; set; }
        public int CategoriaId { get; set; }
        public int PrioridadeId { get; set; }
        public int StatusId { get; set; }
        public DateTime DataAbertura { get; set; }
        public DateTime? DataFechamento { get; set; }
        public UsuarioResumoDTO Usuario { get; set; }
        public UsuarioResumoDTO? Tecnico { get; set; }
        public ItemCatalogoDTO Categoria { get; set; }
        public ItemCatalogoDTO Prioridade { get; set; }
        public ItemCatalogoDTO Status { get; set; }
        public List<ComentarioResponseDTO>? Comentarios { get; set; }
    }
}
