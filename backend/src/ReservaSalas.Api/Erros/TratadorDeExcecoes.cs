using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Erros;

/// <summary>
/// Traduz exceções em respostas Problem Details (RFC 9457). Exceções de regra de negócio são
/// comportamento esperado: viram 4xx sem log de erro. Qualquer outra vira 500 com mensagem genérica,
/// é registrada via ILogger e só expõe detalhes no ambiente de desenvolvimento.
/// </summary>
public class TratadorDeExcecoes(
    IProblemDetailsService problemDetails, ILogger<TratadorDeExcecoes> logger, IHostEnvironment ambiente)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
    {
        var (status, titulo) = excecao switch
        {
            RegraNegocioException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
            ConflitoDeHorarioException => (StatusCodes.Status409Conflict, "Conflito de horário"),
            AcessoNegadoException => (StatusCodes.Status403Forbidden, "Acesso negado"),
            RecursoNaoEncontradoException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };

        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = excecao.Message };

        if (excecao is RegraNegocioException regra)
            problema.Extensions["codigo"] = regra.Codigo;

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(excecao, "Erro não tratado em {Metodo} {Caminho}", http.Request.Method, http.Request.Path);

            problema.Detail = "Ocorreu um erro inesperado. Informe o traceId ao suporte.";
            if (ambiente.IsDevelopment())
                problema.Extensions["excecao"] = excecao.ToString();
        }

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problema,
            Exception = excecao
        });
    }
}
