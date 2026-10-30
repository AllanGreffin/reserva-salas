using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReservaSalas.Domain;

namespace ReservaSalas.Tests.Integration;

/// <summary>T1: reserva simultânea.</summary>
[Trait("Category", "Integration")]
public class ReservaSimultaneaTests(AmbienteDeTestes ambiente)
{
    private const int SalaId = 1;
    private const int Requisicoes = 20;

    [Fact]
    public async Task T1_requisicoes_concorrentes_em_duas_instancias_da_api_criam_exatamente_uma_reserva()
    {
        var (inicio, fim) = Horarios.NovoDia();

        // As requisições são divididas entre duas instâncias da API que compartilham o banco:
        // um lock em memória do processo não impediria o conflito entre elas.
        HttpClient[] clientes =
        [
            await ambiente.ClienteAsync("ana", ambiente.Instancia1),
            await ambiente.ClienteAsync("bruno", ambiente.Instancia2)
        ];

        var largada = new TaskCompletionSource();
        var tarefas = Enumerable.Range(0, Requisicoes).Select(i => Task.Run(async () =>
        {
            await largada.Task;
            // Todos os intervalos se sobrepõem a [inicio, fim): deslocamentos de 0 a 19 minutos.
            return await clientes[i % 2].PostAsJsonAsync("/api/reservas", new
            {
                salaId = SalaId,
                titulo = $"T1 concorrente {i}",
                inicio = inicio.AddMinutes(i),
                fim = fim.AddMinutes(i)
            }, TestContext.Current.CancellationToken);
        })).ToList();

        largada.SetResult();
        var respostas = await Task.WhenAll(tarefas);

        Assert.Equal(1, respostas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(Requisicoes - 1, respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        await using var db = ambiente.NovoContexto();
        var ativasNoIntervalo = await db.Reservas.CountAsync(
            Reserva.ConflitaCom(SalaId, inicio, fim.AddMinutes(Requisicoes)), TestContext.Current.CancellationToken);
        Assert.Equal(1, ativasNoIntervalo);
    }
}
