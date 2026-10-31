using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservaSalas.Domain.Excecoes;

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
        // Os dois 409 têm causas diferentes; o "codigo" permite ao cliente distingui-los.
        var (status, titulo, codigo, detalhe) = excecao switch
        {
            RegraNegocioException regra =>
                (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", regra.Codigo, excecao.Message),
            ConflitoDeHorarioException =>
                (StatusCodes.Status409Conflict, "Conflito de horário", "CONFLITO_DE_HORARIO", excecao.Message),
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "Versão desatualizada", "VERSAO_DESATUALIZADA",
                    "A reserva foi alterada por outra pessoa depois que você a abriu. Recarregue e tente novamente."),
            AcessoNegadoException =>
                (StatusCodes.Status403Forbidden, "Acesso negado", null, excecao.Message),
            RecursoNaoEncontradoException =>
                (StatusCodes.Status404NotFound, "Recurso não encontrado", null, excecao.Message),
            _ =>
                (StatusCodes.Status500InternalServerError, "Erro interno", null, excecao.Message)
        };

        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe };

        if (codigo is not null)
            problema.Extensions["codigo"] = codigo;

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
