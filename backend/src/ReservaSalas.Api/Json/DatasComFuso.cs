using System.Globalization;
using System.Text.RegularExpressions;

namespace ReservaSalas.Api.Json;

/// <summary>
/// RN06 na entrada: só aceita data/hora ISO 8601 com fuso explícito ("Z" ou "±hh:mm"). Sem fuso,
/// o valor seria interpretado no fuso do servidor e a mesma requisição significaria instantes
/// diferentes conforme a máquina. Usado no corpo JSON e na query string.
/// </summary>
public static partial class DatasComFuso
{
    public const string MensagemDeErro =
        "Data/hora inválida: use ISO 8601 com fuso explícito, por exemplo 2030-01-15T13:00:00Z ou 2030-01-15T10:00:00-03:00.";

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex IsoComFuso();

    public static bool TentarLer(string? texto, out DateTimeOffset valor)
    {
        valor = default;
        return texto is not null
            && IsoComFuso().IsMatch(texto)
            && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out valor);
    }
}
