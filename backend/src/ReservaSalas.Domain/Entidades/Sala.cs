namespace ReservaSalas.Domain.Entidades;

public class Sala
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public int Capacidade { get; private set; }

    private Sala() { } // EF Core

    public Sala(string nome, int capacidade)
    {
        Nome = nome;
        Capacidade = capacidade;
    }
}
