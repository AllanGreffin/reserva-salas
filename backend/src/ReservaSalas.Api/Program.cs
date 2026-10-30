using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ReservaSalas.Api.Autenticacao;
using ReservaSalas.Api.Data;
using ReservaSalas.Api.Erros;
using ReservaSalas.Api.Json;
using ReservaSalas.Api.Reservas;
using ReservaSalas.Domain;

var builder = WebApplication.CreateBuilder(args);

// Dados e domínio
builder.Services.AddDbContext<ReservasDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ReservaSalas")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ReservaService>();
builder.Services.AddScoped<ReservaConsultas>();

// Erros: toda resposta de erro em Problem Details (RFC 9457)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorDeExcecoes>();

// Autenticação (JWT)
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, opcoes) =>
    {
        var jwt = opcoes.Value;

        // Mantém os nomes curtos das claims (sub, name, role) em vez de mapeá-los para URIs.
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emissor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = TokenService.ChaveDe(jwt),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = ClaimsUsuario.Perfil,
            // O padrão aceita tokens até 5 minutos após a expiração.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Seguro por padrão: todo endpoint exige autenticação, salvo os marcados com [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Limita tentativas de login por IP (proteção contra força bruta).
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.AddPolicy(AuthController.PoliticaLogin, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("Login:TentativasPorMinuto", 5),
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new DateTimeOffsetComFusoConverter());
    });
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
// Respostas de erro sem corpo (401 do JwtBearer, 429 do rate limiter, 404 de rota) também viram Problem Details.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

// Torna o Program acessível à WebApplicationFactory dos testes de integração.
public partial class Program;
