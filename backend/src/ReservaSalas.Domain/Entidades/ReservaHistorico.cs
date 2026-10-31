using ReservaSalas.Domain.Enums;

namespace ReservaSalas.Domain.Entidades;

public class ReservaHistorico
{
    public int Id { get; private set; }
    public int ReservaId { get; private set; }
    public Reserva Reserva { get; private set; } = null!;
    public AcaoReserva Acao { get; private set; }
    public int UsuarioId { get; private set; }
    public DateTimeOffset DataHora { get; private set; }
    public string Dados { get; private set; } = null!;

    private ReservaHistorico() { } // EF Core

    // Recebe a entidade (e não o Id) para que uma reserva nova e o seu histórico
    // sejam gravados no mesmo SaveChanges, antes de o Id existir.
    public ReservaHistorico(Reserva reserva, AcaoReserva acao, int usuarioId, DateTimeOffset dataHora, string dados)
    {
        Reserva = reserva;
        Acao = acao;
        UsuarioId = usuarioId;
        DataHora = dataHora.ToUniversalTime();
        Dados = dados;
    }
}
