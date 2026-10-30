using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ReservaSalas.Api.Data;
using ReservaSalas.Api.Erros;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Reservas;

/// <summary>
/// Único ponto de escrita de reservas. Cada operação roda numa transação explícita que inclui
/// o registro de histórico (RN05). Criação e edição seguem o protocolo de exclusão mútua da RN03:
/// bloquear a sala, verificar conflito e gravar, tudo antes do commit.
/// </summary>
public class ReservaService(ReservasDbContext db, TimeProvider relogio)
{
    private static readonly JsonSerializerOptions OpcoesHistorico = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<Reserva> CriarAsync(
        int salaId, string titulo, DateTimeOffset inicio, DateTimeOffset fim, int usuarioId, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var reserva = Reserva.Criar(salaId, usuarioId, titulo, inicio, fim, agora);

        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        await BloquearSalaAsync(salaId, ct);
        await GarantirSemConflitoAsync(reserva, ct);

        db.Reservas.Add(reserva);
        RegistrarHistorico(reserva, AcaoReserva.Criacao, usuarioId, agora, antes: null);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return reserva;
    }

    public async Task<Reserva> EditarAsync(
        int reservaId, int salaId, string titulo, DateTimeOffset inicio, DateTimeOffset fim, int usuarioId,
        CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();

        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var reserva = await ObterAsync(reservaId, ct);
        var antes = Dados(reserva);
        reserva.Editar(salaId, titulo, inicio, fim, agora);

        // O conflito só pode surgir na sala de destino; sair de uma sala apenas libera horário.
        await BloquearSalaAsync(salaId, ct);
        await GarantirSemConflitoAsync(reserva, ct);

        RegistrarHistorico(reserva, AcaoReserva.Edicao, usuarioId, agora, antes);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return reserva;
    }

    public async Task<Reserva> CancelarAsync(int reservaId, int usuarioId, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();

        // Cancelar só libera horário, então não precisa do lock da sala.
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var reserva = await ObterAsync(reservaId, ct);
        var antes = Dados(reserva);
        reserva.Cancelar();

        RegistrarHistorico(reserva, AcaoReserva.Cancelamento, usuarioId, agora, antes);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return reserva;
    }

    /// <summary>
    /// Bloqueia a linha da sala até o fim da transação. UPDLOCK é incompatível com outro UPDLOCK,
    /// então requisições para a mesma sala fazem fila aqui (em qualquer instância da API), e salas
    /// diferentes não se bloqueiam. Hints de lock ignoram o versionamento do RCSI.
    /// </summary>
    private async Task BloquearSalaAsync(int salaId, CancellationToken ct)
    {
        var salas = await db.Database
            .SqlQuery<int>($"SELECT Id AS Value FROM Salas WITH (UPDLOCK, ROWLOCK) WHERE Id = {salaId}")
            .ToListAsync(ct);

        if (salas.Count == 0)
            throw new RegraNegocioException("SALA_INEXISTENTE", "A sala informada não existe.");
    }

    private async Task GarantirSemConflitoAsync(Reserva reserva, CancellationToken ct)
    {
        var reservaIgnorada = reserva.Id == 0 ? (int?)null : reserva.Id;
        var conflito = await db.Reservas.AnyAsync(
            Reserva.ConflitaCom(reserva.SalaId, reserva.Inicio, reserva.Fim, reservaIgnorada), ct);

        if (conflito)
            throw new ConflitoDeHorarioException();
    }

    private async Task<Reserva> ObterAsync(int reservaId, CancellationToken ct) =>
        await db.Reservas.FirstOrDefaultAsync(r => r.Id == reservaId, ct)
            ?? throw new RecursoNaoEncontradoException("Reserva não encontrada.");

    private void RegistrarHistorico(
        Reserva reserva, AcaoReserva acao, int usuarioId, DateTimeOffset agora, DadosReserva? antes)
    {
        var dados = JsonSerializer.Serialize(new { antes, depois = Dados(reserva) }, OpcoesHistorico);
        db.ReservasHistorico.Add(new ReservaHistorico(reserva, acao, usuarioId, agora, dados));
    }

    private static DadosReserva Dados(Reserva r) => new(r.SalaId, r.Titulo, r.Inicio, r.Fim, r.Status);

    private sealed record DadosReserva(
        int SalaId, string Titulo, DateTimeOffset Inicio, DateTimeOffset Fim, StatusReserva Status);
}
