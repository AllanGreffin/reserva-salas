namespace ReservaSalas.Domain.Excecoes;

/// <summary>
/// RN03: já existe uma reserva ativa na sala que se sobrepõe ao intervalo pedido.
/// </summary>
public class ConflitoDeHorarioException() : Exception("Já existe uma reserva ativa nesta sala que se sobrepõe ao horário informado.");
