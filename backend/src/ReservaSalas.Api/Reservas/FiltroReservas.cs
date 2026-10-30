using System.ComponentModel.DataAnnotations;
using ReservaSalas.Api.Json;

namespace ReservaSalas.Api.Reservas;

/// <summary>
/// Filtros e paginação da listagem (D3), lidos da query string. O período [de, ate) traz as reservas que
/// se sobrepõem a ele, com a mesma semântica de intervalo semiaberto da RN03. Erros de validação → 400.
/// </summary>
public class FiltroReservas : IValidatableObject
{
    public const int TamanhoPaginaMaximo = 100;

    public int? SalaId { get; init; }

    /// <summary>ISO 8601 com fuso. Recebido como texto para aplicar a mesma exigência de fuso do corpo JSON.</summary>
    public string? De { get; init; }

    public string? Ate { get; init; }

    // Limite superior evita overflow em (Pagina - 1) * TamanhoPagina.
    [Range(1, 1_000_000)]
    public int Pagina { get; init; } = 1;

    // Sem limite, um cliente poderia pedir a tabela inteira de uma vez.
    [Range(1, TamanhoPaginaMaximo)]
    public int TamanhoPagina { get; init; } = 20;

    public DateTimeOffset? InicioDoPeriodo => Ler(De);
    public DateTimeOffset? FimDoPeriodo => Ler(Ate);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (De is not null && !DatasComFuso.TentarLer(De, out _))
            yield return new ValidationResult(DatasComFuso.MensagemDeErro, [nameof(De)]);

        if (Ate is not null && !DatasComFuso.TentarLer(Ate, out _))
            yield return new ValidationResult(DatasComFuso.MensagemDeErro, [nameof(Ate)]);

        if (InicioDoPeriodo is { } de && FimDoPeriodo is { } ate && ate <= de)
            yield return new ValidationResult("O fim do período deve ser posterior ao início.", [nameof(Ate)]);
    }

    private static DateTimeOffset? Ler(string? texto) =>
        DatasComFuso.TentarLer(texto, out var valor) ? valor : null;
}
