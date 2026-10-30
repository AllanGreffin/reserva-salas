using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Data.Configuracoes;

public class SalaConfiguration : IEntityTypeConfiguration<Sala>
{
    public void Configure(EntityTypeBuilder<Sala> builder)
    {
        builder.Property(s => s.Nome).HasMaxLength(100);
    }
}
