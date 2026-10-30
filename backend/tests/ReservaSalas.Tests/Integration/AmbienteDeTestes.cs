using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReservaSalas.Api.Data;
using ReservaSalas.Tests.Integration;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(AmbienteDeTestes))]

namespace ReservaSalas.Tests.Integration;

/// <summary>
/// Um SQL Server real para toda a execução: um container (Testcontainers) ou, se a variável
/// RESERVASALAS_TESTES_SQL estiver definida, a instância indicada por ela. As migrations (com o seed)
/// são aplicadas uma vez. Duas instâncias independentes da API compartilham o banco, como duas réplicas
/// atrás de um balanceador.
/// </summary>
public class AmbienteDeTestes : IAsyncLifetime
{
    public const string VariavelConexao = "RESERVASALAS_TESTES_SQL";

    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = "";
    public ApiDeTestes Instancia1 { get; private set; } = null!;
    public ApiDeTestes Instancia2 { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var conexaoExterna = Environment.GetEnvironmentVariable(VariavelConexao);
        if (string.IsNullOrWhiteSpace(conexaoExterna))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            conexaoExterna = _container.GetConnectionString();
        }

        ConnectionString = new SqlConnectionStringBuilder(conexaoExterna) { InitialCatalog = "ReservaSalasTestes" }
            .ConnectionString;

        // O banco de testes é sempre recriado: numa instância externa, dados de execuções anteriores
        // colidiriam com os horários usados pelos testes.
        await using (var db = NovoContexto())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }

        Instancia1 = new ApiDeTestes(ConnectionString);
        Instancia2 = new ApiDeTestes(ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await Instancia1.DisposeAsync();
        await Instancia2.DisposeAsync();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public ReservasDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<ReservasDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>Cliente HTTP autenticado pelo login real da API com um usuário do seed.</summary>
    public async Task<HttpClient> ClienteAsync(string login, ApiDeTestes? instancia = null)
    {
        var cliente = (instancia ?? Instancia1).CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new { login, senha = Senhas[login] });
        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaLogin>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", corpo!.Token);
        return cliente;
    }

    private static readonly Dictionary<string, string> Senhas = new()
    {
        ["ana"] = "Ana@2026",
        ["bruno"] = "Bruno@2026",
        ["admin"] = "Admin@2026"
    };

    private record RespostaLogin(string Token);
}

public class ApiDeTestes(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Ambiente próprio: não carrega appsettings.Development.json nem os user-secrets do desenvolvedor.
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReservaSalas"] = connectionString,
            ["Jwt:ChaveAssinatura"] = "chave-de-testes-com-pelo-menos-32-caracteres",
            ["Login:TentativasPorMinuto"] = "10000"
        }));
    }
}

/// <summary>Campos da resposta de reserva que os testes usam.</summary>
public record ReservaApi(int Id, string Titulo, byte[] Versao);

/// <summary>
/// Cada teste usa um dia futuro exclusivo, então os testes não interferem entre si
/// mesmo rodando em paralelo e sem limpar o banco.
/// </summary>
public static class Horarios
{
    private static int _proximoDia;

    public static (DateTimeOffset Inicio, DateTimeOffset Fim) NovoDia()
    {
        var dia = Interlocked.Increment(ref _proximoDia);
        var inicio = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(30 + dia).AddHours(10);
        return (inicio, inicio.AddHours(1));
    }
}
