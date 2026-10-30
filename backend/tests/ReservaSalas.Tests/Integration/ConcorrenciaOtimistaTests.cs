using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ReservaSalas.Tests.Integration;

/// <summary>D1: concorrência otimista na edição.</summary>
[Trait("Category", "Integration")]
public class ConcorrenciaOtimistaTests(AmbienteDeTestes ambiente)
{
    [Fact]
    public async Task D1_segunda_edicao_a_partir_da_mesma_versao_recebe_409_sem_sobrescrever_a_primeira()
    {
        var ct = TestContext.Current.CancellationToken;
        var (inicio, fim) = Horarios.NovoDia();
        var ana = await ambiente.ClienteAsync("ana");
        var admin = await ambiente.ClienteAsync("admin");

        var criacao = await ana.PostAsJsonAsync(
            "/api/reservas", new { salaId = 1, titulo = "D1 original", inicio, fim }, ct);
        criacao.EnsureSuccessStatusCode();
        var id = (await criacao.Content.ReadFromJsonAsync<ReservaApi>(ct))!.Id;

        // Os dois abrem a reserva e veem a mesma versão.
        var vistaPelaAna = (await ana.GetFromJsonAsync<ReservaApi>($"/api/reservas/{id}", ct))!;
        var vistaPeloAdmin = (await admin.GetFromJsonAsync<ReservaApi>($"/api/reservas/{id}", ct))!;
        Assert.Equal(vistaPelaAna.Versao, vistaPeloAdmin.Versao);

        var primeira = await admin.PutAsJsonAsync($"/api/reservas/{id}", new
        {
            salaId = 1, titulo = "D1 editada pelo admin", inicio, fim, versao = vistaPeloAdmin.Versao
        }, ct);
        var segunda = await ana.PutAsJsonAsync($"/api/reservas/{id}", new
        {
            salaId = 1, titulo = "D1 editada pela Ana", inicio, fim, versao = vistaPelaAna.Versao
        }, ct);

        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal("VERSAO_DESATUALIZADA", await CodigoDoErroAsync(segunda, ct));

        await using var db = ambiente.NovoContexto();
        var reserva = await db.Reservas.SingleAsync(r => r.Id == id, ct);
        Assert.Equal("D1 editada pelo admin", reserva.Titulo);
        Assert.NotEqual(vistaPelaAna.Versao, reserva.Versao);
        Assert.Equal(2, await db.ReservasHistorico.CountAsync(h => h.ReservaId == id, ct)); // criação + edição do admin
    }

    [Fact]
    public async Task Conflito_de_horario_e_de_versao_sao_distinguiveis_pelo_codigo()
    {
        var ct = TestContext.Current.CancellationToken;
        var (inicio, fim) = Horarios.NovoDia();
        var ana = await ambiente.ClienteAsync("ana");

        (await ana.PostAsJsonAsync("/api/reservas", new { salaId = 2, titulo = "Ocupando", inicio, fim }, ct))
            .EnsureSuccessStatusCode();
        var sobreposta = await ana.PostAsJsonAsync(
            "/api/reservas", new { salaId = 2, titulo = "Sobreposta", inicio, fim }, ct);

        Assert.Equal(HttpStatusCode.Conflict, sobreposta.StatusCode);
        Assert.Equal("CONFLITO_DE_HORARIO", await CodigoDoErroAsync(sobreposta, ct));
    }

    [Fact]
    public async Task Edicao_sem_versao_e_rejeitada_com_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (inicio, fim) = Horarios.NovoDia();
        var ana = await ambiente.ClienteAsync("ana");

        var criacao = await ana.PostAsJsonAsync(
            "/api/reservas", new { salaId = 1, titulo = "Sem versão", inicio, fim }, ct);
        var id = (await criacao.Content.ReadFromJsonAsync<ReservaApi>(ct))!.Id;

        var edicao = await ana.PutAsJsonAsync(
            $"/api/reservas/{id}", new { salaId = 1, titulo = "Editada", inicio, fim }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, edicao.StatusCode);
    }

    private static async Task<string?> CodigoDoErroAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
        return corpo.RootElement.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;
    }
}
