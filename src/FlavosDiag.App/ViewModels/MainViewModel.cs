using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using FlavosDiag.App.Views;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.App.Services;
using FlavosDiag.Core;
using FlavosDiag.Core.Benchmark;
using FlavosDiag.Core.Diagnostics;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Providers.Fakes;

namespace FlavosDiag.App.ViewModels;

/// <summary>Estado compartilhado da bancada: resultados coletados na sessão e a tela ativa.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly Servicos _servicos;
    private readonly Configuracao _configuracao;

    public MainViewModel(Servicos servicos, Configuracao configuracao, bool demonstracao)
    {
        _servicos = servicos;
        _configuracao = configuracao;
        Configuracao = configuracao;
        Demonstracao = demonstracao;
        Historico = HistoricoBenchmark.Carregar(ConfiguracaoStore.CaminhoHistorico);
        Checklist = new ObservableCollection<ItemChecklist>(Fixtures.Checklist().Select(i => i with { Status = StatusChecklist.Pendente }));

        Resumo = new ResumoViewModel(this);
        SistemaVm = new SistemaViewModel(this);
        DiscoVm = new DiscoViewModel(this);
        MemoriaVm = new MemoriaViewModel(this);
        CpuVm = new CpuViewModel(this);
        StressVm = new StressViewModel(this);
        GpuVm = new GpuViewModel(this);
        BateriaVm = new BateriaViewModel(this);
        RedeVm = new RedeViewModel(this);
        ChecklistVm = new ChecklistViewModel(this);
        LaudoVm = new LaudoViewModel(this);
        ConfigVm = new ConfigViewModel(this);

        Paginas = new ObservableCollection<PaginaBase>
        {
            Resumo, SistemaVm, DiscoVm, MemoriaVm, CpuVm, StressVm, GpuVm, BateriaVm, RedeVm, ChecklistVm, LaudoVm,
        };
        _paginaAtual = Resumo;
        CriarMenu();
        PaginaAtual = Resumo;
        TituloPagina = Resumo.Titulo;
        AtualizarDestaque();
    }

    public Window Janela { get; set; } = null!;

    public Servicos Servicos => _servicos;

    public Configuracao Configuracao { get; }

    public bool Demonstracao { get; }

    public string Versao => _servicos.Versao;

    public HistoricoBenchmark Historico { get; }

    public ObservableCollection<ItemChecklist> Checklist { get; }

    public ObservableCollection<PaginaBase> Paginas { get; }

    public ResumoViewModel Resumo { get; }

    public SistemaViewModel SistemaVm { get; }

    public DiscoViewModel DiscoVm { get; }

    public MemoriaViewModel MemoriaVm { get; }

    public CpuViewModel CpuVm { get; }

    public StressViewModel StressVm { get; }

    public GpuViewModel GpuVm { get; }

    public BateriaViewModel BateriaVm { get; }

    public RedeViewModel RedeVm { get; }

    public ChecklistViewModel ChecklistVm { get; }

    public LaudoViewModel LaudoVm { get; }

    public ConfigViewModel ConfigVm { get; }

    [ObservableProperty]
    private PaginaBase _paginaAtual;

    [ObservableProperty]
    private string _tituloPagina = "Resumo";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RotuloTema))]
    private bool _temaEscuro;

    public string RotuloTema => TemaEscuro ? "Tema escuro" : "Tema claro";

    public string StatusBarra => Demonstracao
        ? "Modo demonstração — dados fictícios (não são leituras do computador)"
        : (_servicos.Elevacao.Elevado
            ? "Executando como administrador (leitura completa de sensores e discos)"
            : "Modo reduzido: sem administrador (sensores de temperatura e alguns dados de disco podem ficar Inconclusivo)");

    // ---- Resultados da sessão ----
    public ResultadoDiagnosticoRapido? UltimoDiagnostico { get; set; }

    public InventarioSistema? Inventario { get; set; }

    public IReadOnlyList<DiscoInfo> Discos = Array.Empty<DiscoInfo>();

    public InfoBateriaDetalhe? Bateria { get; set; }

    public InfoRede? Rede { get; set; }

    public ResultadoEstresse? EstresseCpu { get; set; }

    public ResultadoBenchmarkCpu? BenchmarkCpu { get; set; }

    public ResultadoGpu? ResultadoGpu { get; set; }

    public ResultadoTesteMemoria? TesteMemoria { get; set; }

    public IReadOnlyList<AmostraSensores> AmostrasSensores { get; set; } = Array.Empty<AmostraSensores>();

    public IReadOnlyList<ResultadoVarreduraSuperficie> Varreduras { get; set; } = Array.Empty<ResultadoVarreduraSuperficie>();

    public ResultadoVerificacaoSistemaArquivos? VerificacaoFs { get; set; }

    public string ChaveMaquina =>
        Mascaramento.HashChave(Inventario?.PlacaMae.Fabricante.Valor, Inventario?.PlacaMae.Modelo.Valor, Inventario?.PlacaMae.NumeroSerie.Valor);

    // ---- Callbacks de UI (preenchidos pela janela) ----
    public Func<string, string, bool>? SolicitarConfirmacao { get; set; }

    public Func<(bool Ok, string? Pasta)>? SolicitarPasta { get; set; }

    public Func<string, string, (bool Ok, string? Arquivo)>? SolicitarArquivo { get; set; }

    public bool Confirmar(string titulo, string texto) => SolicitarConfirmacao?.Invoke(titulo, texto) ?? false;

    /// <summary>
    /// Pede o aceite para ler os discos, uma vez por sessão. Sem aceite, o provider não abre o disco com
    /// acesso de escrita e a saúde fica Inconclusivo com a causa (D78). Devolve <c>true</c> quando a
    /// leitura está autorizada.
    /// </summary>
    public bool AutorizarLeituraDisco()
    {
        if (_servicos.LeituraDiscoAutorizada.Autorizada)
        {
            return true;
        }

        var aceitou = Confirmar(
            "Ler a saúde dos discos",
            FlavosDiag.Providers.Discos.DiscosWindowsProvider.AvisoLeituraDisco);
        _servicos.LeituraDiscoAutorizada.Registrar(aceitou);
        return aceitou;
    }

    public void AoIniciar(Window janela)
    {
        Janela = janela;
        TemaEscuro = Configuracao.Tema != "Claro";

        // Fechar a janela precisa encerrar o stress. Sem isto, as threads de carga continuavam
        // ocupando o processador depois do aplicativo fechar — e sem nenhuma tela para pará-las.
        janela.Closing += (_, _) => EncerrarTudo();
        Application.Current.SessionEnding += (_, _) => EncerrarTudo();
    }

    /// <summary>Interrompe trabalho em andamento e fecha a sessão de sensores.</summary>
    public void EncerrarTudo()
    {
        try
        {
            StressVm.Encerrar();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Falha ao encerrar não pode impedir o fechamento da janela.
        }

        try
        {
            Servicos.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Idem: encerramento best-effort.
        }
    }

    public ObservableCollection<ItemNav> ItensNav { get; } = new();

    /// <summary>
    /// Item do rodapé que leva a Configurações. Fica na mesma lógica de destaque dos itens de
    /// categoria, para que a tela ativa apareça marcada onde quer que ela esteja no menu.
    /// </summary>
    public ItemNav ItemConfiguracoes { get; private set; } = null!;

    public void NavegarPara(PaginaBase pagina)
    {
        if (pagina is null)
        {
            return;
        }

        PaginaAtual = pagina;
        TituloPagina = pagina.Titulo;
        if (!DemonstracaoDeTeste)
        {
            pagina.AoAtivar();
        }

        AtualizarDestaque();
    }

    /// <summary>Usado pelos testes de interface: navega sem disparar coleta assíncrona.</summary>
    public bool DemonstracaoDeTeste { get; set; }

    /// <summary>
    /// Monta o menu uma única vez. Cada item recebe um comando sem parâmetro que captura a sua
    /// própria página — assim o botão do DataTemplate funciona sem <c>CommandParameter</c>.
    /// </summary>
    private void CriarMenu()
    {
        foreach (var pagina in Paginas)
        {
            ItensNav.Add(CriarItem(pagina));
        }

        ItemConfiguracoes = CriarItem(ConfigVm);
    }

    private ItemNav CriarItem(PaginaBase pagina)
    {
        var geometria = Application.Current?.TryFindResource(pagina.Icone) as Geometry;
        return new ItemNav
        {
            Pagina = pagina,
            Rotulo = pagina.Titulo,
            GeometriaIcone = geometria ?? Geometry.Empty,
            Comando = new RelayCommand(() => NavegarPara(pagina)),
            Selecionado = false,
        };
    }

    /// <summary>
    /// Atualiza o destaque <b>in loco</b>. Recriar <see cref="ItensNav"/> substituiria os botões e o
    /// menu perderia o foco do teclado a cada clique.
    /// </summary>
    private void AtualizarDestaque()
    {
        foreach (var item in ItensNav)
        {
            item.Selecionado = ReferenceEquals(item.Pagina, PaginaAtual);
        }

        if (ItemConfiguracoes is not null)
        {
            ItemConfiguracoes.Selecionado = ReferenceEquals(ConfigVm, PaginaAtual);
        }
    }

    partial void OnPaginaAtualChanged(PaginaBase value) => AtualizarDestaque();

    [RelayCommand]
    private void NavegarIndice(string indice)
    {
        if (int.TryParse(indice, out var i) && i >= 0 && i < Paginas.Count)
        {
            NavegarPara(Paginas[i]);
        }
    }

    [RelayCommand]
    private void AlternarTema()
    {
        TemaEscuro = !TemaEscuro;
        Configuracao.Tema = TemaEscuro ? "Escuro" : "Claro";
        App.AplicarTema(TemaEscuro);
        AvisoSalvamento = ConfiguracaoStore.Salvar(Configuracao);
    }

    /// <summary>Motivo da última falha ao gravar configurações (null = gravado).</summary>
    [ObservableProperty]
    private string? _avisoSalvamento;

    [RelayCommand]
    private void AbrirConfiguracoes() => NavegarPara(ConfigVm);

    /// <summary>
    /// Atalho Ctrl+N: começa um atendimento novo — volta ao Resumo, limpa os resultados da sessão e
    /// dispara o diagnóstico rápido. Não apaga laudos gravados nem o histórico em disco.
    /// </summary>
    [RelayCommand]
    private void IniciarNovoDiagnostico()
    {
        NavegarPara(Resumo);

        UltimoDiagnostico = null;
        Inventario = null;
        Discos = Array.Empty<DiscoInfo>();
        Bateria = null;
        Rede = null;
        EstresseCpu = null;
        BenchmarkCpu = null;
        ResultadoGpu = null;
        TesteMemoria = null;
        AmostrasSensores = Array.Empty<AmostraSensores>();
        Varreduras = Array.Empty<ResultadoVarreduraSuperficie>();
        VerificacaoFs = null;

        Resumo.LimparResultados();

        if (Resumo.DiagnosticoRapidoCommand.CanExecute(null))
        {
            Resumo.DiagnosticoRapidoCommand.Execute(null);
        }
    }

    public (bool Ok, string? Pasta) EscolherPasta() => SolicitarPasta?.Invoke() ?? (false, null);
}
