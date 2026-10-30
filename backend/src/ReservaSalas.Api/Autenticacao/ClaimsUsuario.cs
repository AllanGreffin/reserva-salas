using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Autenticacao;

/// <summary>
/// Usuário autenticado, extraído das claims do token. É sempre a fonte do responsável pela reserva:
/// os DTOs de entrada não têm campo de usuário, então o cliente não consegue forjá-lo.
/// </summary>
public record UsuarioAtual(int Id, string Nome, PerfilUsuario Perfil);

public static class ClaimsUsuario
{
    public const string Perfil = "role";

    public static UsuarioAtual ObterUsuarioAtual(this ClaimsPrincipal principal) =>
        new(
            int.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!),
            principal.FindFirstValue(JwtRegisteredClaimNames.Name)!,
            Enum.Parse<PerfilUsuario>(principal.FindFirstValue(Perfil)!));
}
