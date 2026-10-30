using Microsoft.AspNetCore.Mvc;

namespace ReservaSalas.Api.Reservas;

[ApiController]
[Route("api/salas")]
public class SalasController(ReservaConsultas consultas) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SalaResponse>> Listar(CancellationToken ct) => consultas.ListarSalasAsync(ct);
}
