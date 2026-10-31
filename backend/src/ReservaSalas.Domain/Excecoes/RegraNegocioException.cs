namespace ReservaSalas.Domain.Excecoes;

/// <summary>
/// Violação de uma regra de negócio. A requisição é bem formada, mas a operação não é permitida.
/// </summary>
public class RegraNegocioException(string codigo, string mensagem) : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}
