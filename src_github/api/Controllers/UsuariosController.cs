using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TechHelpSolutions.Data;
using TechHelpSolutions.DTOs;
using TechHelpSolutions.Models;
using TechHelpSolutions.Services;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsuariosController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Criar(UsuarioDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Senha) ||
            string.IsNullOrWhiteSpace(dto.Tipo))
        {
            return BadRequest("Nome, e-mail, senha e tipo são obrigatórios.");
        }

        var emailNormalizado = dto.Email.Trim().ToLower();
        var tipoNormalizado = dto.Tipo.Trim();

        // Este endpoint e de cadastro publico, inclusive se receber um JWT.
        // Perfis privilegiados nao sao concedidos por esse fluxo.
        if (!tipoNormalizado.Equals("Usuario", StringComparison.OrdinalIgnoreCase))
            return BadRequest("O cadastro público permite apenas o perfil Usuario.");

        var emailJaExiste = await _context.Usuarios
            .AnyAsync(u => u.Email.ToLower() == emailNormalizado);

        if (emailJaExiste)
            return Conflict("Já existe um usuário cadastrado com esse e-mail.");

        var usuario = new Usuario
        {
            Nome = dto.Nome.Trim(),
            Email = emailNormalizado,
            Senha = new PasswordHashService().Hash(dto.Senha),
            Tipo = "Usuario"
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return Ok(ProjetarUsuario(usuario));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var usuarios = await _context.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioResponseDTO
            {
                Id = u.Id,
                Nome = u.Nome,
                Email = u.Email,
                Tipo = u.Tipo,
                DataCriacao = u.DataCriacao
            })
            .ToListAsync();

        return Ok(usuarios);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> ObterUsuarioLogado()
    {
        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (usuario == null)
            return NotFound();

        return Ok(ProjetarUsuario(usuario));
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(int id, AtualizarUsuarioDTO dto)
    {
        var userId = ObterUsuarioIdLogado();
        if (userId == null)
            return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && userId.Value != id)
            return Forbid();

        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound();

        // Nao administradores podem editar dados pessoais, mas nao trocar de perfil.
        if (!isAdmin && !string.Equals(dto.Tipo.Trim(), usuario.Tipo, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        usuario.Nome = dto.Nome;
        usuario.Email = dto.Email;
        usuario.Tipo = isAdmin ? dto.Tipo : usuario.Tipo;

        if (!string.IsNullOrWhiteSpace(dto.Senha))
            usuario.Senha = new PasswordHashService().Hash(dto.Senha);

        await _context.SaveChangesAsync();

        return Ok(ProjetarUsuario(usuario));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Excluir(int id, [FromServices] AdministracaoService service)
    {
        try
        {
            return await service.ExcluirUsuario(id) switch
            {
                ResultadoExclusao.Sucesso => NoContent(),
                ResultadoExclusao.NaoEncontrado => NotFound(),
                _ => Conflict("Usuário possui chamados ou comentários vinculados e não pode ser excluído.")
            };
        }
        catch (DbUpdateException ex) when (AdministracaoService.ConflitoIntegridade(ex))
        {
            return Conflict("Não foi possível excluir o usuário devido a vínculos existentes.");
        }
    }

    private int? ObterUsuarioIdLogado()
    {
        var userId = User.FindFirstValue("UserId");

        if (int.TryParse(userId, out var parsedUserId))
            return parsedUserId;

        return null;
    }

    private static UsuarioResponseDTO ProjetarUsuario(Usuario usuario)
    {
        return new UsuarioResponseDTO
        {
            Id = usuario.Id,
            Nome = usuario.Nome,
            Email = usuario.Email,
            Tipo = usuario.Tipo,
            DataCriacao = usuario.DataCriacao
        };
    }
}
