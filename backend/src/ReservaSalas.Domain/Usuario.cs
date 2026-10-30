namespace ReservaSalas.Domain;

public class Usuario
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Login { get; private set; } = null!;
    public string SenhaHash { get; private set; } = null!;
    public PerfilUsuario Perfil { get; private set; }

    private Usuario() { } // EF Core

    public Usuario(string nome, string login, string senhaHash, PerfilUsuario perfil)
    {
        Nome = nome;
        Login = login;
        SenhaHash = senhaHash;
        Perfil = perfil;
    }
}
