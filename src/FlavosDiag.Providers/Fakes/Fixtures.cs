using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.Providers.Fakes;

/// <summary>
/// Dados FICTÍCIOS usados apenas no modo demonstração (--modo-demo, com faixa visível na tela e no laudo)
/// e nos testes de interface. Nunca são usados em produção.
/// </summary>
public static class Fixtures
{
    public const string FabricanteMaquina = "Demonstração";

    public static InventarioSistema Inventario() => new()
    {
        Sistema = new InfoSistema
        {
            Nome = Leitura<string>.Ok("Windows 11 Pro (demonstração)", "fixture"),
            Versao = Leitura<string>.Ok("24H2", "fixture"),
            Build = Leitura<string>.Ok("26100.1000", "fixture"),
            Arquitetura = Leitura<string>.Ok("64-bit", "fixture"),
            NomeComputador = Leitura<string>.Ok("BANCADA-DEMO", "fixture"),
            Usuario = Leitura<string>.Ok("Técnico", "fixture"),
            DataInstalacao = Leitura<DateTime>.Ok(new DateTime(2025, 3, 14), "fixture"),
            Ativacao = Leitura<string>.Ok("Ativado", "fixture"),
        },
        Bios = new InfoBios
        {
            Fabricante = Leitura<string>.Ok("Fabricante de demonstração", "fixture"),
            Versao = Leitura<string>.Ok("1.24", "fixture"),
            Data = Leitura<DateTime>.Ok(new DateTime(2025, 1, 10), "fixture"),
            Modo = Leitura<string>.Ok("UEFI", "fixture"),
        },
        PlacaMae = new InfoPlacaMae
        {
            Fabricante = Leitura<string>.Ok(FabricanteMaquina, "fixture"),
            Modelo = Leitura<string>.Ok("Placa de exemplo X570 (fictícia)", "fixture"),
            Versao = Leitura<string>.Ok("Rev 1.0", "fixture"),
            NumeroSerie = Leitura<string>.Ok("DEMO-SN-000000123456", "fixture"),
        },
        Cpu = new InfoCpu
        {
            Nome = Leitura<string>.Ok("Processador de demonstração 8 núcleos", "fixture"),
            Fabricante = Leitura<string>.Ok("Fabricante de demonstração", "fixture"),
            NucleosFisicos = Leitura<int>.Ok(8, "fixture"),
            ThreadsLogicas = Leitura<int>.Ok(16, "fixture"),
            ClockBaseMhz = Leitura<double>.Ok(3800, "fixture"),
            ClockMaximoMhz = Leitura<double>.Ok(4800, "fixture"),
            CacheL2Kb = Leitura<double>.Ok(4096, "fixture"),
            CacheL3Kb = Leitura<double>.Ok(32768, "fixture"),
            Arquitetura = Leitura<string>.Ok("64-bit", "fixture"),
        },
        Memoria = new InfoMemoria
        {
            TotalBytes = Leitura<long>.Ok(32L * 1024 * 1024 * 1024, "fixture"),
            SlotsUsados = Leitura<int>.Ok(2, "fixture"),
            SlotsTotais = Leitura<int>.Ok(4, "fixture"),
            Timings = Leitura<string>.NaoSuportado("Não disponível: nenhuma fonte confiável e com licença compatível foi confirmada."),
            Modulos = new[]
            {
                new ModuloMemoria
                {
                    Local = Leitura<string>.Ok("DIMM_A1"),
                    CapacidadeBytes = Leitura<long>.Ok(16L * 1024 * 1024 * 1024),
                    Tipo = Leitura<string>.Ok("DDR4"),
                    VelocidadeNominalMhz = Leitura<int>.Ok(3200),
                    VelocidadeConfiguradaMhz = Leitura<int>.Ok(3200),
                    Fabricante = Leitura<string>.Ok("Fabricante de demonstração"),
                    NumeroPeca = Leitura<string>.Ok("DEMO-16GB-3200"),
                    NumeroSerie = Leitura<string>.Ok("DIMMSN-DEMO-0001"),
                },
                new ModuloMemoria
                {
                    Local = Leitura<string>.Ok("DIMM_A2"),
                    CapacidadeBytes = Leitura<long>.Ok(16L * 1024 * 1024 * 1024),
                    Tipo = Leitura<string>.Ok("DDR4"),
                    VelocidadeNominalMhz = Leitura<int>.Ok(3200),
                    VelocidadeConfiguradaMhz = Leitura<int>.Ok(3200),
                    Fabricante = Leitura<string>.Ok("Fabricante de demonstração"),
                    NumeroPeca = Leitura<string>.Ok("DEMO-16GB-3200"),
                    NumeroSerie = Leitura<string>.Ok("DIMMSN-DEMO-0002"),
                },
            },
        },
        Gpus = new[]
        {
            new InfoGpu
            {
                Nome = Leitura<string>.Ok("GPU de demonstração", "fixture"),
                VramDedicadaBytes = Leitura<long>.Ok(8L * 1024 * 1024 * 1024, "fixture"),
                VersaoDriver = Leitura<string>.Ok("31.0.15.0000", "fixture"),
                DataDriver = Leitura<DateTime>.Ok(new DateTime(2025, 6, 1), "fixture"),
                Fabricante = Leitura<string>.Ok("Fabricante de demonstração", "fixture"),
            },
        },
        Monitores = new[]
        {
            new InfoMonitor
            {
                Largura = Leitura<int>.Ok(1920),
                Altura = Leitura<int>.Ok(1080),
                TaxaAtualizacaoHz = Leitura<int>.Ok(144),
                Nome = Leitura<string>.Ok("Monitor de demonstração"),
            },
        },
        Adaptadores = new[]
        {
            new InfoAdaptadorRede
            {
                Nome = Leitura<string>.Ok("Ethernet"),
                Descricao = Leitura<string>.Ok("Adaptador de demonstração"),
                Mac = Leitura<string>.Ok("02:00:00:00:00:01"),
                VelocidadeBitsPorSegundo = Leitura<long>.Ok(1_000_000_000),
                Tipo = Leitura<string>.Ok("Ethernet 802.3"),
                Ativo = true,
            },
        },
        Bateria = new InfoBateria { Presente = false, Nome = Leitura<string>.NaoSuportado("Sem bateria."), Fabricante = Leitura<string>.NaoSuportado("Sem bateria.") },
        DispositivosComProblema = Array.Empty<DispositivoComProblema>(),
        ColetadoEm = DateTimeOffset.Now,
        Limitacoes = new[] { new DadoAusente("Timings de memória", "Sem fonte confiável com licença compatível.") },
    };

    public static IReadOnlyList<DiscoInfo> Discos() => new[]
    {
        new DiscoInfo
        {
            Numero = 0,
            Modelo = Leitura<string>.Ok("NVMe de demonstração 1 TB", "fixture"),
            Firmware = Leitura<string>.Ok("FWR100", "fixture"),
            Barramento = Leitura<string>.Ok("NVMe", "fixture"),
            Midia = Leitura<TipoMidia>.Ok(TipoMidia.Ssd, "fixture"),
            TamanhoBytes = Leitura<long>.Ok(1L * 1000 * 1000 * 1000 * 1000, "fixture"),
            NumeroSerie = Leitura<string>.Ok("DEMO-NVME-000000789", "fixture"),
            Saude = new SaudeDisco
            {
                Fontes = new[] { FonteSaudeDisco.Nvme },
                AtributosAta = Array.Empty<AtributoSmart>(),
                Nvme = new SaudeNvme
                {
                    AvisoCritico = Leitura<int>.Ok(0),
                    TemperaturaCelsius = Leitura<int>.Ok(41),
                    EspacoDisponivelPercentual = Leitura<int>.Ok(100),
                    LimiarEspacoDisponivel = Leitura<int>.Ok(10),
                    PercentualVidaUsada = Leitura<double>.Ok(4),
                    DadosEscritosGb = Leitura<long>.Ok(12000),
                    DadosLidosGb = Leitura<long>.Ok(22000),
                    HorasLigado = Leitura<long>.Ok(2400),
                    CiclosLigadoDesligado = Leitura<long>.Ok(410),
                    ErrosIntegridadeMidia = Leitura<long>.Ok(0),
                },
                Confiabilidade = null,
                Limitacoes = Array.Empty<DadoAusente>(),
            },
        },
    };

    public static InfoBateriaDetalhe Bateria() => new()
    {
        Presente = true,
        Nome = Leitura<string>.Ok("Bateria de demonstração", "fixture"),
        CapacidadeProjetoMwh = Leitura<int>.Ok(48000, "fixture"),
        CapacidadeAtualMwh = Leitura<int>.Ok(36500, "fixture"),
        Ciclos = Leitura<int>.Ok(210, "fixture"),
        Fabricante = Leitura<string>.Ok("Fabricante de demonstração", "fixture"),
        NumeroSerie = Leitura<string>.Ok("BAT-DEMO-0000456", "fixture"),
        Estado = Leitura<string>.Ok("Descarregando", "fixture"),
        SaudePercentual = Leitura<double>.Ok(76.0, "fixture"),
        ColetadoEm = DateTimeOffset.Now,
        Limitacoes = Array.Empty<DadoAusente>(),
    };

    public static InfoRede Rede() => new()
    {
        Adaptadores = Inventario().Adaptadores,
        EnderecoIp = Leitura<string>.Ok("192.168.0.50", "fixture"),
        Gateway = Leitura<string>.Ok("192.168.0.1", "fixture"),
        ServidoresDns = Leitura<string>.Ok("192.168.0.1", "fixture"),
        Wifi = new StatusWifi
        {
            Ssid = Leitura<string>.NaoSuportado("Adaptador de demonstração é cabeado."),
            QualidadePercentual = Leitura<int>.NaoSuportado("Sem Wi-Fi."),
            ForcaDbm = Leitura<int>.NaoSuportado("Sem Wi-Fi."),
        },
        LatenciaGateway = Leitura<ResultadoLatencia>.Ok(new ResultadoLatencia
        {
            Alvo = "192.168.0.1",
            IcMPRespondido = true,
            TcpRespondido = true,
            Enviados = 20,
            Recebidos = 20,
            PerdaPercentual = 0,
            LatenciaMediaMs = 1.2,
            LatenciaMinimaMs = 0.8,
            LatenciaMaximaMs = 3.1,
        }),
        Alvos = Array.Empty<ResultadoLatencia>(),
        Dns = Leitura<ResultadoDns>.NaoSuportado("Nenhum nome configurado."),
        ColetadoEm = DateTimeOffset.Now,
        Limitacoes = Array.Empty<DadoAusente>(),
    };

    public static IReadOnlyList<ItemChecklist> Checklist() => new[]
    {
        new ItemChecklist { Id = "boot-usb", Titulo = "Teste de boot por pendrive", Instrucao = "Iniciar um sistema ao vivo pelo pendrive.", Status = StatusChecklist.Ok },
        new ItemChecklist { Id = "memoria-externa", Titulo = "Teste de memória externo", Instrucao = "Executar o teste completo pelo pendrive.", Status = StatusChecklist.Pendente },
        new ItemChecklist { Id = "teclado", Titulo = "Teclado", Instrucao = "Testar todas as teclas.", Status = StatusChecklist.Ok },
        new ItemChecklist { Id = "touchpad", Titulo = "Touchpad", Instrucao = "Testar clique, toque e gestos.", Status = StatusChecklist.NaoSeAplica },
        new ItemChecklist { Id = "usb", Titulo = "Portas USB", Instrucao = "Conectar um dispositivo em cada porta.", Status = StatusChecklist.Ok },
        new ItemChecklist { Id = "audio", Titulo = "Áudio", Instrucao = "Reproduzir som nos alto-falantes e microfone.", Status = StatusChecklist.Ok },
        new ItemChecklist { Id = "camera", Titulo = "Câmera", Instrucao = "Abrir o aplicativo de câmera.", Status = StatusChecklist.Ok },
        new ItemChecklist { Id = "tela", Titulo = "Tela (pixels mortos)", Instrucao = "Exibir cores sólidas e procurar pontos.", Status = StatusChecklist.Ok },
        new ItemChecklist { Id = "carregador", Titulo = "Carregador", Instrucao = "Verificar carga com a bateria em uso.", Status = StatusChecklist.Ok },
        new ItemChecklist { Id = "cooler", Titulo = "Cooler", Instrucao = "Ouvir e observar as ventoinhas sob carga.", Status = StatusChecklist.Pendente },
    };
}
