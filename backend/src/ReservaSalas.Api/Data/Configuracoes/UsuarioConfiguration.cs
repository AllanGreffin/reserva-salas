using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReservaSalas.Domain.Entidades;
using ReservaSalas.Domain.Enums;

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

        // Hashes gerados uma única vez com PasswordHasher (PBKDF2-HMAC-SHA512, 100 mil iterações, salt aleatório).
        // As senhas de demonstração estão no README, como pede o enunciado.
        builder.HasData(
            new
            {
                Id = 1, Nome = "Ana Souza", Login = "ana", Perfil = PerfilUsuario.Comum,
                SenhaHash = "AQAAAAIAAYagAAAAEKMUfEqWOuU/5iOKb2xd/QzK/KQy5xKRsCSDuKcgVL1gnjjaIM7qCV9rzVVCmTyOBw=="
            },
            new
            {
                Id = 2, Nome = "Bruno Lima", Login = "bruno", Perfil = PerfilUsuario.Comum,
                SenhaHash = "AQAAAAIAAYagAAAAEItBrlv/fcG7Y5yo/b28LYTklG0WS9W6/LQDKOIRKxxeGH5n5CSilz/u0mWxSBtj/Q=="
            },
            new
            {
                Id = 3, Nome = "Administrador", Login = "admin", Perfil = PerfilUsuario.Administrador,
                SenhaHash = "AQAAAAIAAYagAAAAEAQgD+atyoJ8S7E5Fr70nMvomqhIEc6MGOgyUzKIxIny6Mty2Y/Ey7GmfsJ/q3md5w=="
            });
    }
}
