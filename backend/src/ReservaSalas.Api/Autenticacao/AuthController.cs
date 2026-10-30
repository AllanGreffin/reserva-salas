using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ReservaSalas.Api.Data;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Autenticacao;

public record LoginRequest(
    [Required, MaxLength(50)] string Login,
    [Required, MaxLength(100)] string Senha);

public record UsuarioResponse(int Id, string Nome, PerfilUsuario Perfil);

public record LoginResponse(string Token, DateTimeOffset ExpiraEm, UsuarioResponse Usuario);

[ApiController]
[Route("api/auth")]
public class AuthController(ReservasDbContext db, TokenService tokens, IPasswordHasher<Usuario> hasher)
    : ControllerBase
{
    public const string PoliticaLogin = "login";

    // Usado quando o login não existe, para que a resposta leve o mesmo tempo de uma senha errada
    // e não revele quais logins existem.
    private static readonly string HashFalso = new PasswordHasher<Usuario>().HashPassword(null!, "senha-inexistente");

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaLogin)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Login == request.Login, ct);

        var resultado = hasher.VerifyHashedPassword(usuario!, usuario?.SenhaHash ?? HashFalso, request.Senha);
        if (usuario is null || resultado == PasswordVerificationResult.Failed)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login ou senha inválidos.");

        var (token, expiraEm) = tokens.Gerar(usuario);
        return new LoginResponse(token, expiraEm, new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Perfil));
    }

    [HttpGet("eu")]
    public UsuarioResponse Eu()
    {
        var usuario = User.ObterUsuarioAtual();
        return new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Perfil);
    }
}
