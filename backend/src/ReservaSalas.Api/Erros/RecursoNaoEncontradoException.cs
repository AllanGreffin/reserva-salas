namespace ReservaSalas.Api.Erros;

public class RecursoNaoEncontradoException(string mensagem) : Exception(mensagem);
