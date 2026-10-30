namespace ReservaSalas.Domain;

public class Reserva
{
    public int Id { get; private set; }
    public int SalaId { get; private set; }
    public Sala Sala { get; private set; } = null!;
    public int UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public string Titulo { get; private set; } = null!;
    public DateTimeOffset Inicio { get; private set; }
    public DateTimeOffset Fim { get; private set; }
    public StatusReserva Status { get; private set; }

    private Reserva() { } // EF Core
}
