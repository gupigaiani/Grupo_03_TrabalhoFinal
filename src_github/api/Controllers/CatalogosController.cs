using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHelpSolutions.Data;
using TechHelpSolutions.DTOs;

[ApiController]
[Route("api/catalogos")]
[Authorize]
public class CatalogosController : ControllerBase
{
    private readonly AppDbContext _context;

    public CatalogosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias()
    {
        var categorias = await _context.Categorias
            .AsNoTracking()
            .OrderBy(c => c.Nome)
            .Select(c => new ItemCatalogoDTO
            {
                Id = c.Id,
                Nome = c.Nome
            })
            .ToListAsync();

        return Ok(categorias);
    }

    [HttpGet("prioridades")]
    public async Task<IActionResult> ListarPrioridades()
    {
        var prioridades = await _context.Prioridades
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new ItemCatalogoDTO
            {
                Id = p.Id,
                Nome = p.Nome
            })
            .ToListAsync();

        return Ok(prioridades);
    }

    [HttpGet("status")]
    public async Task<IActionResult> ListarStatus()
    {
        var status = await _context.Status
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new ItemCatalogoDTO
            {
                Id = s.Id,
                Nome = s.Nome
            })
            .ToListAsync();

        return Ok(status);
    }
}
