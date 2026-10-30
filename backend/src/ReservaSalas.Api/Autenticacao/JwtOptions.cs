using System.ComponentModel.DataAnnotations;

namespace ReservaSalas.Api.Autenticacao;

/// <summary>
/// Validadas na inicialização: a API não sobe sem uma chave de assinatura adequada.
/// A chave vem de user-secrets (desenvolvimento) ou da variável de ambiente Jwt__ChaveAssinatura.
/// </summary>
public class JwtOptions
{
    public const string Secao = "Jwt";

    [Required]
    public string Emissor { get; init; } = "";

    [Required]
    public string Audiencia { get; init; } = "";

    // HS256 exige uma chave de pelo menos 256 bits (32 bytes).
    [Required, MinLength(32)]
    public string ChaveAssinatura { get; init; } = "";

    [Range(1, 1440)]
    public int ExpiracaoMinutos { get; init; } = 60;
}
