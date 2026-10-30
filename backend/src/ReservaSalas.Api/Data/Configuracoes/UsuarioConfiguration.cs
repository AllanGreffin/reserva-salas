using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReservaSalas.Domain;

namespace ReservaSalas.Api.Data.Configuracoes;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.Property(u => u.Nome).HasMaxLength(100);
        builder.Property(u => u.Login).HasMaxLength(50);
        builder.Property(u => u.SenhaHash).HasMaxLength(200);
        builder.Property(u => u.Perfil).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(u => u.Login).IsUnique();
    }
}
