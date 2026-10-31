using Microsoft.AspNetCore.Mvc;

namespace ReservaSalas.Api.Salas;

[ApiController]
[Route("api/salas")]
public class SalasController(SalaConsultas consultas) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SalaResponse>> Listar(CancellationToken ct) => consultas.ListarAsync(ct);
}
