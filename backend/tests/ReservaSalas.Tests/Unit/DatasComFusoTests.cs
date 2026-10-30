using ReservaSalas.Api.Json;

namespace ReservaSalas.Tests.Unit;

[Trait("Category", "Unit")]
public class DatasComFusoTests
{
    [Theory]
    [InlineData("2030-01-15T13:00:00Z", 13)]
    [InlineData("2030-01-15T13:00:00.000Z", 13)]
    [InlineData("2030-01-15T10:00:00-03:00", 13)]
    [InlineData("2030-01-15T13:00Z", 13)]
    public void Aceita_iso_com_fuso_explicito(string texto, int horaUtc)
    {
        Assert.True(DatasComFuso.TentarLer(texto, out var valor));
        Assert.Equal(horaUtc, valor.UtcDateTime.Hour);
    }

    [Theory]
    [InlineData("2030-01-15T10:00:00")]   // sem fuso: seria interpretado no fuso do servidor
    [InlineData("2030-01-15")]
    [InlineData("15/01/2030 10:00Z")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejeita_data_sem_fuso_ou_fora_do_formato(string? texto)
    {
        Assert.False(DatasComFuso.TentarLer(texto, out _));
    }
}
