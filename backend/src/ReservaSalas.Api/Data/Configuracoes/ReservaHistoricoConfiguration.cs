using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Data.Configuracoes;

public class ReservaHistoricoConfiguration : IEntityTypeConfiguration<ReservaHistorico>
{
    public void Configure(EntityTypeBuilder<ReservaHistorico> builder)
    {
        builder.ToTable("ReservasHistorico");

        builder.Property(h => h.Acao).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(h => h.Reserva).WithMany().HasForeignKey(h => h.ReservaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
