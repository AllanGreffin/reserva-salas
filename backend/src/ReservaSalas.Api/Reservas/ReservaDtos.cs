using System.ComponentModel.DataAnnotations;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Reservas;

// Sem campo de usuário: o responsável é sempre o usuário autenticado.
// Tipos anuláveis + [Required] distinguem "campo ausente" de um valor padrão (0, data mínima).
public record ReservaRequest(
    [Required] int? SalaId,
    [Required, MaxLength(Reserva.TamanhoMaximoTitulo)] string Titulo,
    [Required] DateTimeOffset? Inicio,
    [Required] DateTimeOffset? Fim);

public record ResumoResponse(int Id, string Nome);

public record ReservaResponse(
    int Id,
    string Titulo,
    ResumoResponse Sala,
    ResumoResponse Responsavel,
    DateTimeOffset Inicio,
    DateTimeOffset Fim,
    StatusReserva Status,
    bool PodeEditar,
    bool PodeCancelar);

public record SalaResponse(int Id, string Nome, int Capacidade);
