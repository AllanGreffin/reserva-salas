namespace ReservaSalas.Domain.Excecoes;

/// <summary>
/// O usuário autenticado não tem permissão para alterar a reserva (seção 4 do enunciado).
/// </summary>
public class AcessoNegadoException() : Exception("Você só pode editar ou cancelar as suas próprias reservas.");
