using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Data.Configuracoes;

public class SalaConfiguration : IEntityTypeConfiguration<Sala>
{
    public void Configure(EntityTypeBuilder<Sala> builder)
    {
        builder.Property(s => s.Nome).HasMaxLength(100);

        builder.HasData(
            new { Id = 1, Nome = "Sala Azul", Capacidade = 4 },
            new { Id = 2, Nome = "Sala Verde", Capacidade = 8 },
            new { Id = 3, Nome = "Auditório", Capacidade = 30 });
    }
}
