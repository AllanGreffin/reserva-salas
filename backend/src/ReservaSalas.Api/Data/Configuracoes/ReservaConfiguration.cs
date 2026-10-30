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

        // rowversion: o SQL Server muda o valor a cada UPDATE, e o EF inclui "AND Versao = @original"
        // no WHERE. Nenhuma linha afetada = outra pessoa alterou antes (DbUpdateConcurrencyException).
        builder.Property(r => r.Versao).IsRowVersion();

        builder.HasOne(r => r.Sala).WithMany().HasForeignKey(r => r.SalaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Usuario).WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Verificação de conflito (RN03): SalaId = @sala AND Inicio < @fim AND Fim > @inicio.
        // O seek é por Fim (e não por Inicio) para descartar as reservas que já terminaram;
        // Inicio é incluído para evitar key lookup. Reservas canceladas não entram no índice.
        builder.HasIndex(r => new { r.SalaId, r.Fim })
            .IncludeProperties(r => r.Inicio)
            .HasFilter("[Status] = 'Ativa'")
            .HasDatabaseName("IX_Reservas_SalaId_Fim_Ativas");

        // Listagem (D3): atende o filtro de período (Inicio < @ate) e a ordenação (Inicio, Id) sem sort.
        // Inclui canceladas, então não pode ser o índice filtrado acima. Sem SalaId na chave: com poucas
        // salas, o filtro por sala é pouco seletivo e é aplicado sobre o resultado deste índice.
        builder.HasIndex(r => new { r.Inicio, r.Id })
            .HasDatabaseName("IX_Reservas_Inicio_Id");
    }
}
