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
        GpuVm = new GpuViewModel(this);
        BateriaVm = new BateriaViewModel(this);
        RedeVm = new RedeViewModel(this);
        ChecklistVm = new ChecklistViewModel(this);
        LaudoVm = new LaudoViewModel(this);
        ConfigVm = new ConfigViewModel(this);

        Paginas = new ObservableCollection<PaginaBase>
        {
            Resumo, SistemaVm, DiscoVm, MemoriaVm, CpuVm, GpuVm, BateriaVm, RedeVm, ChecklistVm, LaudoVm,
        };
        PaginaAtual = Resumo;
        TituloPagina = Resumo.Titulo;
        AtualizarMenu();
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

    public void AoIniciar(Window janela)
    {
        Janela = janela;
        TemaEscuro = Configuracao.Tema != "Claro";
    }

    public ObservableCollection<ItemNav> ItensNav { get; } = new();

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

        AtualizarMenu();
    }

    /// <summary>Usado pelos testes de interface: navega sem disparar coleta assíncrona.</summary>
    public bool DemonstracaoDeTeste { get; set; }

    private void AtualizarMenu()
    {
        ItensNav.Clear();
        foreach (var pagina in Paginas)
        {
            var geometria = Application.Current?.TryFindResource(pagina.Icone) as Geometry;
            ItensNav.Add(new ItemNav
            {
                Pagina = pagina,
                Rotulo = pagina.Titulo,
                GeometriaIcone = geometria ?? Geometry.Empty,
                Comando = new RelayCommand<PaginaBase>(p => NavegarPara(p!)),
                Selecionado = ReferenceEquals(pagina, PaginaAtual),
            });
        }
    }

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

    public (bool Ok, string? Pasta) EscolherPasta() => SolicitarPasta?.Invoke() ?? (false, null);
}
