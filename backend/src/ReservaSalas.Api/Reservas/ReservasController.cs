using Microsoft.AspNetCore.Mvc;
using ReservaSalas.Api.Autenticacao;

namespace ReservaSalas.Api.Reservas;

/// <summary>
/// Só traduz HTTP: regras ficam no domínio, orquestração no ReservaService e erros no tratador global.
/// </summary>
[ApiController]
[Route("api/reservas")]
public class ReservasController(ReservaService service, ReservaConsultas consultas) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ReservaResponse>> Listar(CancellationToken ct) =>
        consultas.ListarAsync(User.ObterUsuarioAtual(), ct);

    [HttpGet("{id:int}")]
    public Task<ReservaResponse> Obter(int id, CancellationToken ct) =>
        consultas.ObterAsync(id, User.ObterUsuarioAtual(), ct);

    [HttpPost]
    public async Task<ActionResult<ReservaResponse>> Criar(ReservaRequest request, CancellationToken ct)
    {
        var usuario = User.ObterUsuarioAtual();
        var reserva = await service.CriarAsync(
            request.SalaId!.Value, request.Titulo, request.Inicio!.Value, request.Fim!.Value, usuario, ct);

        var resposta = await consultas.ObterAsync(reserva.Id, usuario, ct);
        return CreatedAtAction(nameof(Obter), new { id = reserva.Id }, resposta);
    }

    [HttpPut("{id:int}")]
    public async Task<ReservaResponse> Editar(int id, ReservaRequest request, CancellationToken ct)
    {
        var usuario = User.ObterUsuarioAtual();
        await service.EditarAsync(
            id, request.SalaId!.Value, request.Titulo, request.Inicio!.Value, request.Fim!.Value, usuario, ct);

        return await consultas.ObterAsync(id, usuario, ct);
    }

    // Cancelar não exclui (RN04), por isso não é um DELETE: é a criação de um cancelamento.
    [HttpPost("{id:int}/cancelamento")]
    public async Task<ReservaResponse> Cancelar(int id, CancellationToken ct)
    {
        var usuario = User.ObterUsuarioAtual();
        await service.CancelarAsync(id, usuario, ct);

        return await consultas.ObterAsync(id, usuario, ct);
    }
}
