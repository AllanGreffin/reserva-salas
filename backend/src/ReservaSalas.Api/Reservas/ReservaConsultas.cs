using Microsoft.EntityFrameworkCore;
using ReservaSalas.Api.Autenticacao;
using ReservaSalas.Api.Data;
using ReservaSalas.Api.Erros;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Reservas;

/// <summary>
/// Lado de leitura, separado do ReservaService (escrita com transação e lock). As entidades são
/// carregadas, e não projetadas em SQL, para que podeEditar/podeCancelar venham dos métodos do domínio.
/// </summary>
public class ReservaConsultas(ReservasDbContext db, TimeProvider relogio)
{
    public async Task<IReadOnlyList<ReservaResponse>> ListarAsync(UsuarioAtual usuario, CancellationToken ct)
    {
        var reservas = await Consulta().OrderBy(r => r.Inicio).ToListAsync(ct);
        var agora = relogio.GetUtcNow();
        return reservas.Select(r => ParaResponse(r, usuario, agora)).ToList();
    }

    public async Task<ReservaResponse> ObterAsync(int id, UsuarioAtual usuario, CancellationToken ct)
    {
        var reserva = await Consulta().FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new RecursoNaoEncontradoException("Reserva não encontrada.");
        return ParaResponse(reserva, usuario, relogio.GetUtcNow());
    }

    public async Task<IReadOnlyList<SalaResponse>> ListarSalasAsync(CancellationToken ct) =>
        await db.Salas.AsNoTracking()
            .OrderBy(s => s.Nome)
            .Select(s => new SalaResponse(s.Id, s.Nome, s.Capacidade))
            .ToListAsync(ct);

    private IQueryable<Reserva> Consulta() =>
        db.Reservas.AsNoTracking().Include(r => r.Sala).Include(r => r.Usuario);

    private static ReservaResponse ParaResponse(Reserva r, UsuarioAtual usuario, DateTimeOffset agora) =>
        new(
            r.Id,
            r.Titulo,
            new ResumoResponse(r.Sala.Id, r.Sala.Nome),
            new ResumoResponse(r.Usuario.Id, r.Usuario.Nome),
            r.Inicio,
            r.Fim,
            r.Status,
            r.PodeSerEditadaPor(usuario.Id, usuario.Perfil, agora),
            r.PodeSerCanceladaPor(usuario.Id, usuario.Perfil));
}
