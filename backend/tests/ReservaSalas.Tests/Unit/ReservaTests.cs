using ReservaSalas.Domain;

namespace ReservaSalas.Tests.Unit;

[Trait("Category", "Unit")]
public class ReservaTests
{
    private const int SalaId = 1;
    private const int UsuarioId = 1;

    // Instante fixo: as regras recebem o "agora" como parâmetro, então os testes não dependem do relógio.
    private static readonly DateTimeOffset Agora = new(2030, 1, 15, 9, 0, 0, TimeSpan.Zero);

    private static Reserva CriarReserva(int inicioEmHoras = 1, int fimEmHoras = 2) =>
        Reserva.Criar(SalaId, UsuarioId, "Reunião", Agora.AddHours(inicioEmHoras), Agora.AddHours(fimEmHoras), Agora);

    private static void AssertViolacao(string codigo, Action acao) =>
        Assert.Equal(codigo, Assert.Throws<RegraNegocioException>(acao).Codigo);

    [Fact]
    public void Criar_reserva_valida_fica_ativa()
    {
        var reserva = CriarReserva();

        Assert.Equal(StatusReserva.Ativa, reserva.Status);
        Assert.Equal(SalaId, reserva.SalaId);
        Assert.Equal(UsuarioId, reserva.UsuarioId);
    }

    [Fact]
    public void Criar_normaliza_datas_para_utc_mantendo_o_instante()
    {
        var inicioBrasilia = new DateTimeOffset(2030, 1, 15, 10, 0, 0, TimeSpan.FromHours(-3));

        var reserva = Reserva.Criar(SalaId, UsuarioId, "Reunião", inicioBrasilia, inicioBrasilia.AddHours(1), Agora);

        Assert.Equal(TimeSpan.Zero, reserva.Inicio.Offset);
        Assert.Equal(13, reserva.Inicio.Hour);
        Assert.Equal(inicioBrasilia, reserva.Inicio);
    }

    // RN01

    [Theory]
    [InlineData(0)]  // fim igual ao início
    [InlineData(-1)] // fim antes do início
    public void RN01_criar_com_fim_que_nao_e_posterior_ao_inicio_e_rejeitado(int duracaoEmHoras)
    {
        AssertViolacao("RN01", () => CriarReserva(inicioEmHoras: 2, fimEmHoras: 2 + duracaoEmHoras));
    }

    [Fact]
    public void RN01_editar_com_fim_igual_ao_inicio_e_rejeitado()
    {
        var reserva = CriarReserva();

        AssertViolacao("RN01", () => reserva.Editar(SalaId, "Reunião", Agora.AddHours(3), Agora.AddHours(3), Agora));
    }

    // RN02

    [Fact]
    public void RN02_criar_com_inicio_no_passado_e_rejeitado()
    {
        AssertViolacao("RN02", () =>
            Reserva.Criar(SalaId, UsuarioId, "Reunião", Agora.AddMinutes(-1), Agora.AddHours(1), Agora));
    }

    [Fact]
    public void RN02_criar_com_inicio_exatamente_agora_e_permitido()
    {
        var reserva = Reserva.Criar(SalaId, UsuarioId, "Reunião", Agora, Agora.AddHours(1), Agora);

        Assert.Equal(Agora, reserva.Inicio);
    }

    [Fact]
    public void RN02_mover_inicio_para_o_passado_e_rejeitado()
    {
        var reserva = CriarReserva(inicioEmHoras: 3, fimEmHoras: 4);
        var maisTarde = Agora.AddHours(2);

        AssertViolacao("RN02", () => reserva.Editar(SalaId, "Reunião", Agora.AddHours(1), Agora.AddHours(4), maisTarde));
    }

    [Fact]
    public void RN02_reserva_em_andamento_pode_ter_titulo_e_fim_alterados()
    {
        var reserva = CriarReserva(inicioEmHoras: 1, fimEmHoras: 2);
        var duranteAReuniao = Agora.AddHours(1.5);

        reserva.Editar(SalaId, "Reunião estendida", reserva.Inicio, Agora.AddHours(3), duranteAReuniao);

        Assert.Equal("Reunião estendida", reserva.Titulo);
        Assert.Equal(Agora.AddHours(3), reserva.Fim);
    }

    // RN04

    [Fact]
    public void RN04_cancelar_muda_status_sem_excluir()
    {
        var reserva = CriarReserva();

        reserva.Cancelar();

        Assert.Equal(StatusReserva.Cancelada, reserva.Status);
    }

    [Fact]
    public void RN04_reserva_cancelada_nao_pode_ser_editada()
    {
        var reserva = CriarReserva();
        reserva.Cancelar();

        AssertViolacao("RN04", () => reserva.Editar(SalaId, "Outro título", reserva.Inicio, reserva.Fim, Agora));
    }

    [Fact]
    public void Cancelar_reserva_ja_cancelada_e_rejeitado()
    {
        var reserva = CriarReserva();
        reserva.Cancelar();

        AssertViolacao("RESERVA_JA_CANCELADA", reserva.Cancelar);
    }

    // Permissões (seção 4)

    [Theory]
    [InlineData(UsuarioId, PerfilUsuario.Comum, true)]          // dono
    [InlineData(UsuarioId + 1, PerfilUsuario.Comum, false)]     // outro usuário comum
    [InlineData(UsuarioId + 1, PerfilUsuario.Administrador, true)] // administrador
    public void Somente_o_dono_ou_um_administrador_pode_alterar(int usuarioId, PerfilUsuario perfil, bool esperado)
    {
        Assert.Equal(esperado, CriarReserva().PodeSerAlteradaPor(usuarioId, perfil));
    }

    // Decisões adicionais (README)

    [Fact]
    public void Reserva_que_ja_terminou_nao_pode_ser_editada()
    {
        var reserva = CriarReserva(inicioEmHoras: 1, fimEmHoras: 2);
        var depoisDoFim = Agora.AddHours(2);

        AssertViolacao("RESERVA_ENCERRADA", () =>
            reserva.Editar(SalaId, "Outro título", reserva.Inicio, reserva.Fim, depoisDoFim));
    }

    [Fact]
    public void Editar_pode_trocar_a_sala()
    {
        var reserva = CriarReserva();

        reserva.Editar(salaId: 2, "Reunião", reserva.Inicio, reserva.Fim, Agora);

        Assert.Equal(2, reserva.SalaId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Titulo_vazio_e_rejeitado(string titulo)
    {
        AssertViolacao("TITULO_OBRIGATORIO", () =>
            Reserva.Criar(SalaId, UsuarioId, titulo, Agora.AddHours(1), Agora.AddHours(2), Agora));
    }

    [Fact]
    public void Titulo_acima_do_tamanho_maximo_e_rejeitado()
    {
        var titulo = new string('a', Reserva.TamanhoMaximoTitulo + 1);

        AssertViolacao("TITULO_MUITO_LONGO", () =>
            Reserva.Criar(SalaId, UsuarioId, titulo, Agora.AddHours(1), Agora.AddHours(2), Agora));
    }

    [Fact]
    public void Titulo_e_gravado_sem_espacos_nas_pontas()
    {
        var reserva = Reserva.Criar(SalaId, UsuarioId, "  Reunião  ", Agora.AddHours(1), Agora.AddHours(2), Agora);

        Assert.Equal("Reunião", reserva.Titulo);
    }
}
