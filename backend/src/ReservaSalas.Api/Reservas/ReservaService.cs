using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ReservaSalas.Api.Autenticacao;
using ReservaSalas.Api.Data;
using ReservaSalas.Api.Erros;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Reservas;

/// <summary>
/// Único ponto de escrita de reservas. Cada operação roda numa transação explícita que inclui
/// o registro de histórico (RN05). Criação e edição seguem o protocolo de exclusão mútua da RN03:
/// bloquear a sala, verificar conflito e gravar, tudo antes do commit. Editar e cancelar verificam
/// a propriedade (seção 4) depois de carregar a reserva e antes de alterá-la.
/// </summary>
public class ReservaService(ReservasDbContext db, TimeProvider relogio)
{
    private static readonly JsonSerializerOptions OpcoesHistorico = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<Reserva> CriarAsync(
        int salaId, string titulo, DateTimeOffset inicio, DateTimeOffset fim, UsuarioAtual usuario,
        CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var reserva = Reserva.Criar(salaId, usuario.Id, titulo, inicio, fim, agora);

        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        await BloquearSalaAsync(salaId, ct);
        await GarantirSemConflitoAsync(reserva, ct);

        db.Reservas.Add(reserva);
        RegistrarHistorico(reserva, AcaoReserva.Criacao, usuario.Id, agora, antes: null);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return reserva;
    }

    public async Task<Reserva> EditarAsync(
        int reservaId, int salaId, string titulo, DateTimeOffset inicio, DateTimeOffset fim, byte[] versaoVista,
        UsuarioAtual usuario, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();

        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var reserva = await ObterParaAlteracaoAsync(reservaId, usuario, ct);

        // Concorrência otimista (D1): o UPDATE compara com a versão que o cliente viu, e não com a que
        // acabou de ser lida. Se outra pessoa gravou antes, nenhuma linha é afetada e o EF lança
        // DbUpdateConcurrencyException (409), sem sobrescrever a outra gravação.
        db.Entry(reserva).Property(r => r.Versao).OriginalValue = versaoVista;

        var antes = Dados(reserva);
        reserva.Editar(salaId, titulo, inicio, fim, agora);

        // O conflito só pode surgir na sala de destino; sair de uma sala apenas libera horário.
        await BloquearSalaAsync(salaId, ct);
        await GarantirSemConflitoAsync(reserva, ct);

        RegistrarHistorico(reserva, AcaoReserva.Edicao, usuario.Id, agora, antes);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return reserva;
    }

    public async Task<Reserva> CancelarAsync(int reservaId, UsuarioAtual usuario, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();

        // Cancelar só libera horário, então não precisa do lock da sala.
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var reserva = await ObterParaAlteracaoAsync(reservaId, usuario, ct);
        var antes = Dados(reserva);
        reserva.Cancelar();

        RegistrarHistorico(reserva, AcaoReserva.Cancelamento, usuario.Id, agora, antes);

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

    // 404 antes de 403: todas as reservas são visíveis a todos, então a existência não é segredo.
    private async Task<Reserva> ObterParaAlteracaoAsync(int reservaId, UsuarioAtual usuario, CancellationToken ct)
    {
        var reserva = await db.Reservas.FirstOrDefaultAsync(r => r.Id == reservaId, ct)
            ?? throw new RecursoNaoEncontradoException("Reserva não encontrada.");

        if (!reserva.PodeSerAlteradaPor(usuario.Id, usuario.Perfil))
            throw new AcessoNegadoException();

        return reserva;
    }

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
