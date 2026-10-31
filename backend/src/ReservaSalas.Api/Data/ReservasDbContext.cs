using Microsoft.EntityFrameworkCore;
using ReservaSalas.Domain.Entidades;

namespace ReservaSalas.Api.Data;

public class ReservasDbContext(DbContextOptions<ReservasDbContext> options) : DbContext(options)
{
    public DbSet<Sala> Salas => Set<Sala>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<ReservaHistorico> ReservasHistorico => Set<ReservaHistorico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReservasDbContext).Assembly);
    }
}
