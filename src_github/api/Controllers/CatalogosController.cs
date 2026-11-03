using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHelpSolutions.Data;
using TechHelpSolutions.DTOs;
using TechHelpSolutions.Services;

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

    [Authorize(Roles = "Admin"), HttpPost("categorias")]
    public Task<IActionResult> CriarCategoria(SalvarCatalogoDTO dto, [FromServices] AdministracaoService service) => Salvar(TipoCatalogo.Categoria, null, dto, service);

    [Authorize(Roles = "Admin"), HttpPut("categorias/{id:int}")]
    public Task<IActionResult> AtualizarCategoria(int id, SalvarCatalogoDTO dto, [FromServices] AdministracaoService service) => Salvar(TipoCatalogo.Categoria, id, dto, service);

    [Authorize(Roles = "Admin"), HttpDelete("categorias/{id:int}")]
    public Task<IActionResult> ExcluirCategoria(int id, [FromServices] AdministracaoService service) => Excluir(TipoCatalogo.Categoria, id, service);

    [Authorize(Roles = "Admin"), HttpPost("prioridades")]
    public Task<IActionResult> CriarPrioridade(SalvarCatalogoDTO dto, [FromServices] AdministracaoService service) => Salvar(TipoCatalogo.Prioridade, null, dto, service);

    [Authorize(Roles = "Admin"), HttpPut("prioridades/{id:int}")]
    public Task<IActionResult> AtualizarPrioridade(int id, SalvarCatalogoDTO dto, [FromServices] AdministracaoService service) => Salvar(TipoCatalogo.Prioridade, id, dto, service);

    [Authorize(Roles = "Admin"), HttpDelete("prioridades/{id:int}")]
    public Task<IActionResult> ExcluirPrioridade(int id, [FromServices] AdministracaoService service) => Excluir(TipoCatalogo.Prioridade, id, service);

    [Authorize(Roles = "Admin"), HttpPost("status")]
    public Task<IActionResult> CriarStatus(SalvarCatalogoDTO dto, [FromServices] AdministracaoService service) => Salvar(TipoCatalogo.Status, null, dto, service);

    [Authorize(Roles = "Admin"), HttpPut("status/{id:int}")]
    public Task<IActionResult> AtualizarStatus(int id, SalvarCatalogoDTO dto, [FromServices] AdministracaoService service) => Salvar(TipoCatalogo.Status, id, dto, service);

    [Authorize(Roles = "Admin"), HttpDelete("status/{id:int}")]
    public Task<IActionResult> ExcluirStatus(int id, [FromServices] AdministracaoService service) => Excluir(TipoCatalogo.Status, id, service);

    private async Task<IActionResult> Salvar(TipoCatalogo tipo, int? id, SalvarCatalogoDTO dto, AdministracaoService service)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome é obrigatório.");
        try
        {
            var item = await service.SalvarCatalogo(tipo, id, dto.Nome);
            return item is null ? NotFound() : Ok(item);
        }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException ex) when (AdministracaoService.ConflitoIntegridade(ex))
        { return Conflict("Não foi possível salvar o item devido a um conflito de integridade."); }
    }

    private async Task<IActionResult> Excluir(TipoCatalogo tipo, int id, AdministracaoService service)
    {
        try
        {
            return await service.ExcluirCatalogo(tipo, id) switch
            {
                ResultadoExclusao.Sucesso => NoContent(),
                ResultadoExclusao.NaoEncontrado => NotFound(),
                _ => Conflict("Item vinculado a chamados e não pode ser excluído.")
            };
        }
        catch (DbUpdateException ex) when (AdministracaoService.ConflitoIntegridade(ex))
        { return Conflict("Não foi possível excluir o item devido a vínculos existentes."); }
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
