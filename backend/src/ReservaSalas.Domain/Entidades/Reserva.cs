using System.Linq.Expressions;
using ReservaSalas.Domain.Enums;
using ReservaSalas.Domain.Excecoes;

namespace ReservaSalas.Domain.Entidades;

public class Reserva
{
    public const int TamanhoMaximoTitulo = 200;

    public int Id { get; private set; }
    public int SalaId { get; private set; }
    public Sala Sala { get; private set; } = null!;
    public int UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public string Titulo { get; private set; } = null!;
    public DateTimeOffset Inicio { get; private set; }
    public DateTimeOffset Fim { get; private set; }
    public StatusReserva Status { get; private set; }

    /// <summary>
    /// Token de concorrência otimista (D1), gerado pelo banco a cada alteração. Quem edita informa a
    /// versão que viu; se outra pessoa gravou antes, a versão mudou e a gravação é rejeitada.
    /// </summary>
    public byte[] Versao { get; private set; } = null!;

    private Reserva() { } // EF Core

    public static Reserva Criar(
        int salaId, int usuarioId, string titulo, DateTimeOffset inicio, DateTimeOffset fim, DateTimeOffset agora)
    {
        ValidarTitulo(titulo);
        ValidarIntervalo(inicio, fim);
        ValidarInicioNaoPassou(inicio, agora);

        return new Reserva
        {
            SalaId = salaId,
            UsuarioId = usuarioId,
            Titulo = titulo.Trim(),
            Inicio = inicio.ToUniversalTime(),
            Fim = fim.ToUniversalTime(),
            Status = StatusReserva.Ativa
        };
    }

    public void Editar(int salaId, string titulo, DateTimeOffset inicio, DateTimeOffset fim, DateTimeOffset agora)
    {
        if (EstaCancelada)
            throw new RegraNegocioException("RN04", "Reservas canceladas não podem ser editadas.");

        if (EstaEncerrada(agora))
            throw new RegraNegocioException("RESERVA_ENCERRADA", "Reservas que já terminaram não podem ser editadas.");

        ValidarTitulo(titulo);
        ValidarIntervalo(inicio, fim);

        // RN02 só vale se o início for movido: uma reserva em andamento pode ter título ou fim alterados.
        if (inicio != Inicio)
            ValidarInicioNaoPassou(inicio, agora);

        SalaId = salaId;
        Titulo = titulo.Trim();
        Inicio = inicio.ToUniversalTime();
        Fim = fim.ToUniversalTime();
    }

    public void Cancelar()
    {
        if (EstaCancelada)
            throw new RegraNegocioException("RESERVA_JA_CANCELADA", "A reserva já está cancelada.");

        Status = StatusReserva.Cancelada;
    }

    /// <summary>
    /// Seção 4: editar e cancelar são permitidos ao responsável pela reserva ou a um administrador.
    /// </summary>
    public bool PodeSerAlteradaPor(int usuarioId, PerfilUsuario perfil) =>
        perfil == PerfilUsuario.Administrador || UsuarioId == usuarioId;

    // Indicam à interface quais ações oferecer, com os mesmos predicados que Editar e Cancelar aplicam.
    // São um retrato do momento da consulta: cada operação revalida tudo ao ser executada.

    public bool PodeSerEditadaPor(int usuarioId, PerfilUsuario perfil, DateTimeOffset agora) =>
        PodeSerAlteradaPor(usuarioId, perfil) && !EstaCancelada && !EstaEncerrada(agora);

    public bool PodeSerCanceladaPor(int usuarioId, PerfilUsuario perfil) =>
        PodeSerAlteradaPor(usuarioId, perfil) && !EstaCancelada;

    private bool EstaCancelada => Status == StatusReserva.Cancelada;

    private bool EstaEncerrada(DateTimeOffset agora) => Fim <= agora;

    /// <summary>
    /// RN03: reservas ativas da mesma sala cujo intervalo [Inicio, Fim) se sobrepõe a [inicio, fim).
    /// É a única definição da regra: traduzida para SQL pelo EF Core e compilada nos testes unitários.
    /// </summary>
    public static Expression<Func<Reserva, bool>> ConflitaCom(
        int salaId, DateTimeOffset inicio, DateTimeOffset fim, int? reservaIgnorada = null) =>
        r => r.SalaId == salaId
            && r.Status == StatusReserva.Ativa
            && r.Inicio < fim
            && r.Fim > inicio
            && (reservaIgnorada == null || r.Id != reservaIgnorada);

    private static void ValidarTitulo(string titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new RegraNegocioException("TITULO_OBRIGATORIO", "O título é obrigatório.");

        if (titulo.Trim().Length > TamanhoMaximoTitulo)
            throw new RegraNegocioException(
                "TITULO_MUITO_LONGO", $"O título deve ter no máximo {TamanhoMaximoTitulo} caracteres.");
    }

    private static void ValidarIntervalo(DateTimeOffset inicio, DateTimeOffset fim)
    {
        if (fim <= inicio)
            throw new RegraNegocioException("RN01", "O fim da reserva deve ser posterior ao início.");
    }

    private static void ValidarInicioNaoPassou(DateTimeOffset inicio, DateTimeOffset agora)
    {
        if (inicio < agora)
            throw new RegraNegocioException("RN02", "Não é permitido reservar com início no passado.");
    }
}
