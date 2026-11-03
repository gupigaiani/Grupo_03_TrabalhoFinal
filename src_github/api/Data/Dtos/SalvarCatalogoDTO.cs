using System.ComponentModel.DataAnnotations;

namespace TechHelpSolutions.DTOs;

public sealed class SalvarCatalogoDTO
{
    [Required]
    public string Nome { get; set; } = "";
}
