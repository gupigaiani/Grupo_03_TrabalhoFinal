using Microsoft.AspNetCore.Mvc;
using TechHelpSolutions.DTOs;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public IActionResult Login(LoginDTO dto)
    {
        AuthLoginResponseDTO? resultado = _authService.Login(dto);

        if (resultado == null)
            return Unauthorized("Email ou senha invalidos");

        return Ok(resultado);
    }
}
