using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TechHelpSolutions.Data;
using TechHelpSolutions.DTOs;
using TechHelpSolutions.Models;
using TechHelpSolutions.Services;
using TechHelpSolutions.Filters;

[ApiController]
[Route("api/chamados")]
[TypeFilter(typeof(ChamadoDatasExceptionFilter))]
public class ChamadosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ChamadoService _service;

    public ChamadosController(AppDbContext context, ChamadoService service)
    {
        _context = context;
        _service = service;
    }

    [Authorize(Roles = "Usuario")]
    [HttpPost]
    public async Task<IActionResult> CriarChamado(CriarChamadoDTO dto)
    {
        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        dto.UsuarioId = userId.Value;

        var chamado = await _service.CriarChamado(dto);
        return Ok(chamado);
    }

    [Authorize(Roles = "Usuario,Tecnico,Gestor,Admin")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? status, [FromQuery] int? prioridade, [FromQuery] int? categoria)
    {
        var query = _service.ConsultarChamadosDetalhados();

        // A restricao integra o SQL da view, antes de filtros e materializacao.
        if (User.IsInRole("Usuario"))
        {
            var userId = ObterUsuarioIdLogado();
            if (userId == null)
                return Unauthorized();

            query = query.Where(c => c.UsuarioId == userId.Value);
        }

        if (status.HasValue)
            query = query.Where(c => c.StatusId == status.Value);

        if (prioridade.HasValue)
            query = query.Where(c => c.PrioridadeId == prioridade.Value);

        if (categoria.HasValue)
            query = query.Where(c => c.CategoriaId == categoria.Value);

        var chamados = await query
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return Ok(chamados);
    }

    [Authorize(Roles = "Usuario")]
    [HttpGet("meus")]
    public async Task<IActionResult> ListarMeusChamados()
    {
        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        var chamados = await _service.ConsultarChamadosDetalhados()
            .Where(c => c.UsuarioId == userId.Value)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return Ok(chamados);
    }

    [Authorize(Roles = "Tecnico")]
    [HttpGet("meus-atendimentos")]
    public async Task<IActionResult> ListarMeusAtendimentos()
    {
        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        var chamados = await _service.ConsultarChamadosDetalhados()
            .Where(c => c.TecnicoId == userId.Value)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return Ok(chamados);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var chamado = await CriarQueryBaseChamados(incluirComentarios: true)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chamado == null)
            return NotFound();

        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        if (User.IsInRole("Usuario") && chamado.UsuarioId != userId.Value)
            return Forbid();

        return Ok(ProjetarChamado(chamado, incluirComentarios: true));
    }

    [Authorize(Roles = "Tecnico")]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> AtualizarStatus(int id, AtualizarStatusDTO dto)
    {
        if (!await _service.AtualizarStatus(id, dto.StatusId))
            return NotFound();

        return NoContent();
    }

    [Authorize(Roles = "Tecnico")]
    [HttpPut("{id}/assumir")]
    public async Task<IActionResult> AssumirChamado(int id)
    {
        var chamado = await _context.Chamados.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

        if (chamado == null)
            return NotFound();

        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        var resultado = await _service.AssumirChamado(chamado, userId.Value);
        return ResponderOperacaoChamado(resultado);
    }

    [Authorize(Roles = "Tecnico")]
    [HttpPut("{id}/finalizar")]
    public async Task<IActionResult> FinalizarChamado(int id)
    {
        var chamado = await _context.Chamados.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

        if (chamado == null)
            return NotFound();

        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        if (chamado.TecnicoId == null)
            return BadRequest("O chamado precisa ser assumido antes de ser finalizado.");

        if (chamado.TecnicoId != userId.Value)
            return Forbid();

        var resultado = await _service.FinalizarChamado(chamado, userId.Value);
        return ResponderOperacaoChamado(resultado);
    }

    [Authorize(Roles = "Usuario,Tecnico")]
    [HttpPut("{id}")]
    public async Task<IActionResult> AtualizarChamado(int id, AtualizarChamadoDTO dto)
    {
        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        var chamado = await _context.Chamados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chamado == null)
            return NotFound();

        if (User.IsInRole("Usuario") && chamado.UsuarioId != userId.Value)
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Titulo) ||
            string.IsNullOrWhiteSpace(dto.Descricao) ||
            dto.CategoriaId <= 0 ||
            dto.PrioridadeId <= 0)
        {
            return BadRequest("Titulo, descricao, categoria e prioridade sao obrigatorios.");
        }

        await _service.AtualizarChamado(id, dto);

        var chamadoAtualizado = await CriarQueryBaseChamados()
            .FirstAsync(c => c.Id == id);

        return Ok(ProjetarChamado(chamadoAtualizado, incluirComentarios: false));
    }

    [Authorize]
    [HttpPost("{chamadoId}/comentarios")]
    public async Task<IActionResult> CriarComentario(int chamadoId, CriarComentarioDTO dto)
    {
        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        var chamado = await _context.Chamados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chamadoId);

        if (chamado == null)
            return NotFound();

        if (User.IsInRole("Usuario") && chamado.UsuarioId != userId.Value)
            return Forbid();

        try
        {
            var comentario = await _service.CriarComentario(chamadoId, userId.Value, dto);
            return Ok(comentario);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpGet("{id}/comentarios")]
    public async Task<IActionResult> ListarComentarios(int id)
    {
        var chamado = await _context.Chamados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chamado == null)
            return NotFound();

        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        if (User.IsInRole("Usuario") && chamado.UsuarioId != userId.Value)
            return Forbid();

        var comentarios = await _service.ConsultarComentariosDetalhados()
            .Where(c => c.ChamadoId == id)
            .OrderBy(c => c.DataEnvio)
            .ToListAsync();

        return Ok(comentarios);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Deletar(int id)
    {
        var chamado = await _context.Chamados
            .Include(c => c.Comentarios)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chamado == null)
            return NotFound();

        if (chamado.Comentarios.Any())
            _context.Comentarios.RemoveRange(chamado.Comentarios);

        _context.Chamados.Remove(chamado);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private IActionResult ResponderOperacaoChamado(ResultadoOperacaoChamado resultado)
    {
        return resultado switch
        {
            ResultadoOperacaoChamado.Sucesso => NoContent(),
            ResultadoOperacaoChamado.NaoEncontrado => NotFound(),
            ResultadoOperacaoChamado.TecnicoInvalido => Forbid(),
            ResultadoOperacaoChamado.SemResponsavel => BadRequest("O chamado precisa ser assumido antes de ser finalizado."),
            ResultadoOperacaoChamado.OutroResponsavel => Forbid(),
            ResultadoOperacaoChamado.Conflito => Conflict("O chamado foi alterado por outra operacao. Recarregue e tente novamente."),
            ResultadoOperacaoChamado.ParametroInvalido => BadRequest("Data de fechamento obrigatoria."),
            _ => throw new InvalidOperationException("Resultado de operacao de chamado desconhecido.")
        };
    }

    private IQueryable<Chamado> CriarQueryBaseChamados(bool incluirComentarios = false)
    {
        var query = _context.Chamados
            .AsNoTracking()
            .Include(c => c.Usuario)
            .Include(c => c.Tecnico)
            .Include(c => c.Categoria)
            .Include(c => c.Prioridade)
            .Include(c => c.Status)
            .AsQueryable();

        if (incluirComentarios)
        {
            query = query
                .Include(c => c.Comentarios)
                .ThenInclude(com => com.Usuario);
        }

        return query;
    }

    private int? ObterUsuarioIdLogado()
    {
        var userId = User.FindFirstValue("UserId");

        if (int.TryParse(userId, out var parsedUserId))
            return parsedUserId;

        return null;
    }

    private static ChamadoResponseDTO ProjetarChamado(Chamado chamado, bool incluirComentarios)
    {
        return new ChamadoResponseDTO
        {
            Id = chamado.Id,
            Titulo = chamado.Titulo,
            Descricao = chamado.Descricao,
            UsuarioId = chamado.UsuarioId,
            TecnicoId = chamado.TecnicoId,
            CategoriaId = chamado.CategoriaId,
            PrioridadeId = chamado.PrioridadeId,
            StatusId = chamado.StatusId,
            DataAbertura = chamado.DataAbertura,
            DataFechamento = chamado.DataFechamento,
            Usuario = new UsuarioResumoDTO
            {
                Id = chamado.Usuario.Id,
                Nome = chamado.Usuario.Nome,
                Email = chamado.Usuario.Email,
                Tipo = chamado.Usuario.Tipo
            },
            Tecnico = chamado.Tecnico == null
                ? null
                : new UsuarioResumoDTO
                {
                    Id = chamado.Tecnico.Id,
                    Nome = chamado.Tecnico.Nome,
                    Email = chamado.Tecnico.Email,
                    Tipo = chamado.Tecnico.Tipo
                },
            Categoria = new ItemCatalogoDTO
            {
                Id = chamado.Categoria.Id,
                Nome = chamado.Categoria.Nome
            },
            Prioridade = new ItemCatalogoDTO
            {
                Id = chamado.Prioridade.Id,
                Nome = chamado.Prioridade.Nome
            },
            Status = new ItemCatalogoDTO
            {
                Id = chamado.Status.Id,
                Nome = chamado.Status.Nome
            },
            Comentarios = incluirComentarios
                ? chamado.Comentarios
                    .OrderBy(com => com.DataEnvio)
                    .Select(com => new ComentarioResponseDTO
                    {
                        Id = com.Id,
                        ChamadoId = com.ChamadoId,
                        Mensagem = com.Mensagem,
                        DataEnvio = com.DataEnvio,
                        Usuario = new UsuarioResumoDTO
                        {
                            Id = com.Usuario.Id,
                            Nome = com.Usuario.Nome,
                            Email = com.Usuario.Email,
                            Tipo = com.Usuario.Tipo
                        }
                    })
                    .ToList()
                : null
        };
    }
}
