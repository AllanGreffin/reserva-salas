using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ReservaSalas.Api.Json;

/// <summary>
/// RN06 na entrada: rejeita datas sem fuso explícito. Sem isto, "2030-01-15T10:00:00" seria aceito e
/// interpretado no fuso do servidor, e a mesma requisição significaria instantes diferentes conforme a máquina.
/// </summary>
public partial class DateTimeOffsetComFusoConverter : JsonConverter<DateTimeOffset>
{
    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex TerminaComFuso();

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();

        if (texto is null || !TerminaComFuso().IsMatch(texto) || !reader.TryGetDateTimeOffset(out var valor))
            throw new JsonException(
                "Data/hora inválida: use ISO 8601 com fuso explícito, por exemplo 2030-01-15T13:00:00Z ou 2030-01-15T10:00:00-03:00.");

        return valor;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
