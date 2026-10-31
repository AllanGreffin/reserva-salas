using Microsoft.EntityFrameworkCore;
using ReservaSalas.Api.Data;

namespace ReservaSalas.Api.Salas;

public record SalaResponse(int Id, string Nome, int Capacidade);

public class SalaConsultas(ReservasDbContext db)
{
    public async Task<IReadOnlyList<SalaResponse>> ListarAsync(CancellationToken ct) =>
        await db.Salas.AsNoTracking()
            .OrderBy(s => s.Nome)
            .Select(s => new SalaResponse(s.Id, s.Nome, s.Capacidade))
            .ToListAsync(ct);
}
