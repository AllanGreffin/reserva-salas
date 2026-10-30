using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReservaSalas.Domain;

namespace ReservaSalas.Tests.Integration;

/// <summary>
/// T2: atomicidade. A falha é forçada dentro do SQL Server, por uma CHECK constraint no histórico que
/// rejeita títulos com um marcador, depois de a reserva já ter sido gravada na transação. Se a reserva
/// não persiste, foi o rollback que a desfez. Só as linhas com o marcador falham, então os testes que
/// rodam em paralelo não são afetados. (Um trigger não serve: o SQL Server proíbe triggers em tabelas
/// gravadas com OUTPUT sem INTO, que é como o EF Core lê os Ids gerados.)
/// </summary>
[Trait("Category", "Integration")]
public class AtomicidadeTests(AmbienteDeTestes ambiente) : IAsyncLifetime
{
    private const string Marcador = "T2-FALHA";
    private const string Constraint = "CK_Testes_FalhaForcadaNoHistorico";

    public async ValueTask InitializeAsync()
    {
        await using var db = ambiente.NovoContexto();
        await db.Database.ExecuteSqlRawAsync($"""
            IF OBJECT_ID('{Constraint}') IS NULL
                ALTER TABLE ReservasHistorico WITH NOCHECK
                    ADD CONSTRAINT {Constraint} CHECK (Dados NOT LIKE '%{Marcador}%')
            """);
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = ambiente.NovoContexto();
        await db.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE ReservasHistorico DROP CONSTRAINT IF EXISTS {Constraint}");
    }

    [Fact]
    public async Task T2_falha_no_historico_desfaz_a_criacao_da_reserva()
    {
        var ct = TestContext.Current.CancellationToken;
        var (inicio, fim) = Horarios.NovoDia();
        var titulo = $"{Marcador} criação {Guid.NewGuid():N}";
        var cliente = await ambiente.ClienteAsync("ana");

        var resposta = await cliente.PostAsJsonAsync("/api/reservas", new { salaId = 1, titulo, inicio, fim }, ct);

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);

        await using var db = ambiente.NovoContexto();
        Assert.False(await db.Reservas.AnyAsync(r => r.Titulo == titulo, ct));
        Assert.False(await db.Reservas.AnyAsync(Reserva.ConflitaCom(1, inicio, fim), ct));
    }

    [Fact]
    public async Task T2_falha_no_historico_desfaz_a_edicao_da_reserva()
    {
        var ct = TestContext.Current.CancellationToken;
        var (inicio, fim) = Horarios.NovoDia();
        var cliente = await ambiente.ClienteAsync("ana");

        var criacao = await cliente.PostAsJsonAsync(
            "/api/reservas", new { salaId = 1, titulo = "T2 original", inicio, fim }, ct);
        criacao.EnsureSuccessStatusCode();
        var id = (await criacao.Content.ReadFromJsonAsync<ReservaCriada>(ct))!.Id;

        var edicao = await cliente.PutAsJsonAsync($"/api/reservas/{id}", new
        {
            salaId = 2,
            titulo = $"{Marcador} edição",
            inicio = inicio.AddHours(1),
            fim = fim.AddHours(1)
        }, ct);

        Assert.Equal(HttpStatusCode.InternalServerError, edicao.StatusCode);

        await using var db = ambiente.NovoContexto();
        var reserva = await db.Reservas.SingleAsync(r => r.Id == id, ct);
        Assert.Equal("T2 original", reserva.Titulo);
        Assert.Equal(1, reserva.SalaId);
        Assert.Equal(inicio, reserva.Inicio);
        Assert.Equal(fim, reserva.Fim);
        Assert.Equal(1, await db.ReservasHistorico.CountAsync(h => h.ReservaId == id, ct)); // só a criação
    }

    private record ReservaCriada(int Id);
}
