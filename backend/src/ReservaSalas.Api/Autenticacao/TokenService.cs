using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ReservaSalas.Domain.Entidades;

namespace ReservaSalas.Api.Autenticacao;

public class TokenService(IOptions<JwtOptions> opcoes, TimeProvider relogio)
{
    private readonly JsonWebTokenHandler _handler = new();

    public static SymmetricSecurityKey ChaveDe(JwtOptions opcoes) =>
        new(Encoding.UTF8.GetBytes(opcoes.ChaveAssinatura));

    public (string Token, DateTimeOffset ExpiraEm) Gerar(Usuario usuario)
    {
        var jwt = opcoes.Value;
        var agora = relogio.GetUtcNow();
        var expiraEm = agora.AddMinutes(jwt.ExpiracaoMinutos);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Emissor,
            Audience = jwt.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, usuario.Nome),
                new Claim(ClaimsUsuario.Perfil, usuario.Perfil.ToString())
            ]),
            SigningCredentials = new SigningCredentials(ChaveDe(jwt), SecurityAlgorithms.HmacSha256)
        });

        return (token, expiraEm);
    }
}
