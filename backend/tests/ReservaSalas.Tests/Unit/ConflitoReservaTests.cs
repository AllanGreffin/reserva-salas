using ReservaSalas.Domain;

namespace ReservaSalas.Tests.Unit;

/// <summary>
/// RN03 testada em memória com a mesma expression que o EF Core traduz para SQL.
/// </summary>
[Trait("Category", "Unit")]
public class ConflitoReservaTests
{
    private const int SalaA = 1;
    private const int SalaB = 2;

    private static readonly DateOnly Dia = new(2030, 1, 15);
    private static readonly DateTimeOffset Agora = Em("08:00");

    private static DateTimeOffset Em(string hora) =>
        new(Dia.ToDateTime(TimeOnly.Parse(hora)), TimeSpan.Zero);

    // Reserva ativa existente na Sala A, das 10h00 às 11h00.
    private static Reserva ReservaExistente() =>
        Reserva.Criar(SalaA, usuarioId: 1, "Existente", Em("10:00"), Em("11:00"), Agora);

    private static bool Conflita(Reserva existente, int salaId, string inicio, string fim, int? reservaIgnorada = null) =>
        Reserva.ConflitaCom(salaId, Em(inicio), Em(fim), reservaIgnorada).Compile()(existente);

    // Tabela da seção 3.3 do enunciado.
    [Theory]
    [InlineData(SalaA, "09:00", "10:00", false)] // termina exatamente quando a outra começa
    [InlineData(SalaA, "11:00", "12:00", false)] // começa exatamente quando a outra termina
    [InlineData(SalaA, "10:30", "11:30", true)]  // sobreposição parcial
    [InlineData(SalaA, "09:00", "12:00", true)]  // contém a reserva existente
    [InlineData(SalaA, "10:15", "10:45", true)]  // contida na reserva existente
    [InlineData(SalaB, "10:00", "11:00", false)] // outra sala
    public void RN03_tabela_da_secao_3_3(int salaId, string inicio, string fim, bool esperaConflito)
    {
        Assert.Equal(esperaConflito, Conflita(ReservaExistente(), salaId, inicio, fim));
    }

    [Fact]
    public void RN03_mesmo_intervalo_na_mesma_sala_conflita()
    {
        Assert.True(Conflita(ReservaExistente(), SalaA, "10:00", "11:00"));
    }

    [Fact]
    public void RN04_reserva_cancelada_libera_o_horario()
    {
        var existente = ReservaExistente();
        existente.Cancelar();

        Assert.False(Conflita(existente, SalaA, "10:00", "11:00"));
    }

    [Fact]
    public void Na_edicao_a_propria_reserva_e_ignorada()
    {
        var existente = ReservaExistente(); // ainda não persistida: Id = 0

        Assert.False(Conflita(existente, SalaA, "10:30", "11:30", reservaIgnorada: existente.Id));
    }
}
