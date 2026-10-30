using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Data.Configuracoes;

public class ReservaConfiguration : IEntityTypeConfiguration<Reserva>
{
    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        builder.Property(r => r.Titulo).HasMaxLength(Reserva.TamanhoMaximoTitulo);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(r => r.Sala).WithMany().HasForeignKey(r => r.SalaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Usuario).WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Verificação de conflito (RN03): SalaId = @sala AND Inicio < @fim AND Fim > @inicio.
        // O seek é por Fim (e não por Inicio) para descartar as reservas que já terminaram;
        // Inicio é incluído para evitar key lookup. Reservas canceladas não entram no índice.
        builder.HasIndex(r => new { r.SalaId, r.Fim })
            .IncludeProperties(r => r.Inicio)
            .HasFilter("[Status] = 'Ativa'")
            .HasDatabaseName("IX_Reservas_SalaId_Fim_Ativas");
    }
}
