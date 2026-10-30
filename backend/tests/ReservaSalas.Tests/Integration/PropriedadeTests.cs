using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReservaSalas.Domain;

namespace ReservaSalas.Tests.Integration;

/// <summary>T3: propriedade. Um usuário Comum não altera a reserva de outro, e ela permanece inalterada.</summary>
[Trait("Category", "Integration")]
public class PropriedadeTests(AmbienteDeTestes ambiente)
{
    [Fact]
    public async Task T3_usuario_comum_recebe_403_ao_editar_ou_cancelar_reserva_de_outro_e_ela_permanece_inalterada()
    {
        var ct = TestContext.Current.CancellationToken;
        var (inicio, fim) = Horarios.NovoDia();
        var ana = await ambiente.ClienteAsync("ana");
        var bruno = await ambiente.ClienteAsync("bruno");

        var criada = await CriarReservaAsync(ana, inicio, fim, ct);
        var id = criada.Id;
        var antes = await LerAsync(id, ct);

        var edicao = await bruno.PutAsJsonAsync($"/api/reservas/{id}", new
        {
            salaId = 2,
            titulo = "Alterada por outro usuário",
            inicio = inicio.AddHours(2),
            fim = fim.AddHours(2),
            versao = criada.Versao
        }, ct);
        var cancelamento = await bruno.PostAsync($"/api/reservas/{id}/cancelamento", null, ct);

        Assert.Equal(HttpStatusCode.Forbidden, edicao.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancelamento.StatusCode);

        var depois = await LerAsync(id, ct);
        Assert.Equal(antes, depois);
        Assert.Equal(StatusReserva.Ativa, depois.Status);
    }

    [Fact]
    public async Task Administrador_pode_cancelar_reserva_de_outro_usuario()
    {
        var ct = TestContext.Current.CancellationToken;
        var (inicio, fim) = Horarios.NovoDia();
        var id = (await CriarReservaAsync(await ambiente.ClienteAsync("ana"), inicio, fim, ct)).Id;
        var admin = await ambiente.ClienteAsync("admin");

        var cancelamento = await admin.PostAsync($"/api/reservas/{id}/cancelamento", null, ct);

        Assert.Equal(HttpStatusCode.OK, cancelamento.StatusCode);
        Assert.Equal(StatusReserva.Cancelada, (await LerAsync(id, ct)).Status);
    }

    private static async Task<ReservaApi> CriarReservaAsync(
        HttpClient cliente, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct)
    {
        var resposta = await cliente.PostAsJsonAsync(
            "/api/reservas", new { salaId = 1, titulo = "T3 reserva da Ana", inicio, fim }, ct);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ReservaApi>(ct))!;
    }

    private async Task<EstadoReserva> LerAsync(int id, CancellationToken ct)
    {
        await using var db = ambiente.NovoContexto();
        var r = await db.Reservas.AsNoTracking().SingleAsync(r => r.Id == id, ct);
        var historicos = await db.ReservasHistorico.CountAsync(h => h.ReservaId == id, ct);
        return new EstadoReserva(r.SalaId, r.UsuarioId, r.Titulo, r.Inicio, r.Fim, r.Status, historicos);
    }

    private record EstadoReserva(
        int SalaId, int UsuarioId, string Titulo, DateTimeOffset Inicio, DateTimeOffset Fim, StatusReserva Status,
        int Historicos);
}
