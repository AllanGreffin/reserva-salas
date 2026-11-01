using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace ReservaSalas.Api.Documentacao;

public static class DocumentacaoOpenApi
{
    public const string Secao = "OpenApi";
    private const string EsquemaJwt = "Bearer";

    // Habilitada em desenvolvimento ou por configuração (OpenApi:Habilitado), como no docker-compose.
    public static bool EstaHabilitada(WebApplication app) =>
        app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>($"{Secao}:Habilitado");

    // Descreve o JWT no documento para o botão "Authorize" do Swagger UI enviar o token. Só os endpoints
    // que exigem autenticação recebem o requisito; o login, que é anônimo, fica sem ele.
    public static IServiceCollection AdicionarDocumentacaoComAutenticacaoJwt(this IServiceCollection services) =>
        services.AddOpenApi(opcoes =>
        {
            opcoes.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Components ??= new OpenApiComponents();
                documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                documento.Components.SecuritySchemes[EsquemaJwt] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Token obtido em POST /api/auth/login."
                };
                return Task.CompletedTask;
            });

            opcoes.AddOperationTransformer((operacao, contexto, _) =>
            {
                var anonimo = contexto.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
                if (!anonimo)
                {
                    operacao.Security =
                    [
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(EsquemaJwt, contexto.Document)] = [] }
                    ];
                }
                return Task.CompletedTask;
            });
        });
}
