using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservaSalas.Api.Json;

/// <summary>Aplica <see cref="DatasComFuso"/> às datas do corpo JSON.</summary>
public class DateTimeOffsetComFusoConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && DatasComFuso.TentarLer(reader.GetString(), out var valor)
            ? valor
            : throw new JsonException(DatasComFuso.MensagemDeErro);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
