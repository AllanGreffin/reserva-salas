using System.Net;
using System.Net.Http.Json;

namespace ReservaSalas.Tests.Integration;

/// <summary>
/// D3: filtro por sala e período, com paginação no servidor. Cada teste cria as próprias reservas num dia
/// exclusivo e filtra por esse dia, então não depende do que os outros testes gravaram.
/// </summary>
[Trait("Category", "Integration")]
public class ListagemTests(AmbienteDeTestes ambiente)
{
    private const int SalaA = 1;
    private const int SalaB = 2;

    [Fact]
    public async Task Filtra_por_sala_e_por_periodo()
    {
        var (cliente, dia) = await PrepararDiaAsync();

        var doDia = await ListarAsync(cliente, $"de={Iso(dia)}&ate={Iso(dia.AddDays(1))}");
        var daSalaA = await ListarAsync(cliente, $"salaId={SalaA}&de={Iso(dia)}&ate={Iso(dia.AddDays(1))}");

        Assert.Equal(6, doDia.TotalItens);
        Assert.Equal(5, daSalaA.TotalItens);
        Assert.All(daSalaA.Itens, r => Assert.Equal(SalaA, r.Sala.Id));
    }

    [Fact]
    public async Task Periodo_traz_as_reservas_que_se_sobrepoem_com_borda_semiaberta()
    {
        var (cliente, dia) = await PrepararDiaAsync();

        // 09:30–10:30 sobrepõe 09–10 e 10–11.
        var parcial = await ListarAsync(cliente,
            $"salaId={SalaA}&de={Iso(dia.AddHours(9.5))}&ate={Iso(dia.AddHours(10.5))}");
        // 10:00–11:00: 09–10 termina exatamente no início e 11–12 começa exatamente no fim; nenhuma das duas entra.
        var exato = await ListarAsync(cliente,
            $"salaId={SalaA}&de={Iso(dia.AddHours(10))}&ate={Iso(dia.AddHours(11))}");

        Assert.Equal(["09h", "10h"], parcial.Itens.Select(r => r.Titulo));
        Assert.Equal(["10h"], exato.Itens.Select(r => r.Titulo));
    }

    [Fact]
    public async Task Pagina_em_ordem_sem_repetir_nem_perder_reservas()
    {
        var (cliente, dia) = await PrepararDiaAsync();
        var filtro = $"salaId={SalaA}&de={Iso(dia)}&ate={Iso(dia.AddDays(1))}&tamanhoPagina=2";

        var paginas = new List<PaginaDeReservas>();
        for (var numero = 1; numero <= 4; numero++)
            paginas.Add(await ListarAsync(cliente, $"{filtro}&pagina={numero}"));

        Assert.All(paginas, p => Assert.Equal((5, 3), (p.TotalItens, p.TotalPaginas)));
        Assert.Equal([2, 2, 1, 0], paginas.Select(p => p.Itens.Count));

        var titulos = paginas.SelectMany(p => p.Itens).Select(r => r.Titulo).ToList();
        Assert.Equal(["08h", "09h", "10h", "11h", "12h"], titulos);
    }

    [Theory]
    [InlineData("tamanhoPagina=101")]
    [InlineData("pagina=0")]
    [InlineData("de=2030-01-15T10:00:00")]                                // sem fuso
    [InlineData("de=2030-01-15T12:00:00Z&ate=2030-01-15T10:00:00Z")]      // fim antes do início
    public async Task Parametros_invalidos_retornam_400(string query)
    {
        var cliente = await ambiente.ClienteAsync("ana");

        var resposta = await cliente.GetAsync($"/api/reservas?{query}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    /// <summary>
    /// Num dia exclusivo: cinco reservas de 1h na sala A (08h às 13h) e uma na sala B (10h).
    /// Os títulos indicam o horário de início.
    /// </summary>
    private async Task<(HttpClient Cliente, DateTimeOffset Dia)> PrepararDiaAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var cliente = await ambiente.ClienteAsync("ana");
        var dia = Horarios.NovoDia().Inicio.AddHours(-10);

        // Criadas fora de ordem, para que a ordenação seja de fato testada.
        foreach (var hora in new[] { 12, 8, 10, 9, 11 })
            await CriarAsync(cliente, SalaA, $"{hora:00}h", dia.AddHours(hora), ct);
        await CriarAsync(cliente, SalaB, "10h sala B", dia.AddHours(10), ct);

        return (cliente, dia);
    }

    private static async Task CriarAsync(HttpClient cliente, int salaId, string titulo, DateTimeOffset inicio, CancellationToken ct)
    {
        var resposta = await cliente.PostAsJsonAsync(
            "/api/reservas", new { salaId, titulo, inicio, fim = inicio.AddHours(1) }, ct);
        resposta.EnsureSuccessStatusCode();
    }

    private static async Task<PaginaDeReservas> ListarAsync(HttpClient cliente, string query) =>
        (await cliente.GetFromJsonAsync<PaginaDeReservas>($"/api/reservas?{query}", TestContext.Current.CancellationToken))!;

    // "+" na query string seria lido como espaço; EscapeDataString o codifica.
    private static string Iso(DateTimeOffset valor) => Uri.EscapeDataString(valor.ToString("o"));

    private record PaginaDeReservas(List<Item> Itens, int Pagina, int TamanhoPagina, int TotalItens, int TotalPaginas);

    private record Item(int Id, string Titulo, Resumo Sala);

    private record Resumo(int Id, string Nome);
}
