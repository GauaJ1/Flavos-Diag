using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;
using FlavosDiag.Providers;
using FlavosDiag.Providers.Abstractions;
using FlavosDiag.Providers.Dxgi;
using FlavosDiag.Providers.Fakes;
using FlavosDiag.Providers.Nativo;
using FlavosDiag.Providers.Wmi;
using FlavosDiag.Workloads;

namespace FlavosDiag.App.Services;

/// <summary>Configurações persistentes do técnico (JSON em %LOCALAPPDATA%\FlavosDiag\config.json).</summary>
public sealed class Configuracao
{
    public string NomeTecnico { get; set; } = string.Empty;

    public string Empresa { get; set; } = string.Empty;

    public string Cnpj { get; set; } = string.Empty;

    public string Contato { get; set; } = string.Empty;

    /// <summary>
    /// Limite de veredito térmico da CPU. O padrão vem de <see cref="LimitesTermicos"/> e passou de
    /// 90 para 95 °C: 90 °C de projeto é normal em processador moderno sob carga, e reprovar máquina
    /// saudável é a forma mais rápida de ensinar o técnico a ignorar o veredito.
    /// </summary>
    public double LimiteTemperaturaCpuCelsius { get; set; } = LimitesTermicos.VereditoCpuCelsius;

    /// <summary>Limite de veredito térmico da GPU: o limite da CPU mais a margem de projeto da placa.</summary>
    public double LimiteTemperaturaGpuCelsius { get; set; } =
        LimitesTermicos.LimiteDoComponente(TipoHardware.Gpu, LimitesTermicos.VereditoCpuCelsius);

    public string ServidorTesteRede { get; set; } = string.Empty;

    public string AlvosPing { get; set; } = string.Empty;

    public string NomeDnsTeste { get; set; } = string.Empty;

    public bool MascararSeriais { get; set; } = true;

    public string PastaExportacao { get; set; } = string.Empty;

    public string Tema { get; set; } = "Escuro";

    public int JanelaGraficoSegundos { get; set; } = 60;

    /// <summary>
    /// Temperatura a partir da qual o gráfico de stress destaca a faixa de alerta. O destaque é
    /// só visual; a parada automática usa <see cref="LimiteParadaAutomaticaCelsius"/>.
    /// </summary>
    public double LimiarAlertaTemperaturaCelsius { get; set; } = LimitesTermicos.AlertaVisualCelsius;

    /// <summary>
    /// Temperatura de confiança alta a partir da qual o stress para sozinho. Padrão 95 °C: acima
    /// disso, manter carga total só faz sentido se alguém quiser queimar o equipamento de propósito.
    /// O teto de 110 °C é aplicado pela validação de <see cref="LimitesTermicos"/>.
    /// </summary>
    public double LimiteParadaAutomaticaCelsius { get; set; } = LimitesTermicos.ParadaAutomaticaCelsius;

    /// <summary>
    /// Teto de duração do stress quando há leitura de temperatura. Padrão 30 minutos, ajustável até
    /// 2 horas — mais que isso é tempo de técnico desperdiçado, não informação nova.
    /// </summary>
    public int TetoDuracaoStressMinutos { get; set; } = 30;

    [JsonIgnore]
    public IReadOnlyList<string> AlvosPingLista =>
        AlvosPing.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public static class ConfiguracaoStore
{
    public static string PastaDados { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlavosDiag");

    public static string CaminhoConfig => Path.Combine(PastaDados, "config.json");

    public static string CaminhoHistorico => Path.Combine(PastaDados, "historico.json");

    public static string PastaFotos => Path.Combine(PastaDados, "fotos");

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Configuracao Carregar()
    {
        try
        {
            if (File.Exists(CaminhoConfig))
            {
                return JsonSerializer.Deserialize<Configuracao>(File.ReadAllText(CaminhoConfig), Opcoes) ?? new Configuracao();
            }
        }
        catch (Exception)
        {
            // Configuração corrompida: volta ao padrão (o padrão é só-leitura e seguro).
        }

        return new Configuracao();
    }

    /// <summary>Salva a configuração. Falha de gravação não derruba o aplicativo: devolve o motivo.</summary>
    public static string? Salvar(Configuracao configuracao)
    {
        try
        {
            Directory.CreateDirectory(PastaDados);
            var temp = CaminhoConfig + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(configuracao, Opcoes));
            File.Move(temp, CaminhoConfig, overwrite: true);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

/// <summary>Composição dos providers reais (ou de demonstração quando solicitado explicitamente).</summary>
public sealed class Servicos : IDisposable
{
    public required IAmbiente Ambiente { get; init; }

    public required bool Demonstracao { get; init; }

    public required IInventarioProvider Inventario { get; init; }

    public required ISensorProviderFactory Sensores { get; init; }

    public required IDiscoProvider Discos { get; init; }

    public required IBateriaProvider Bateria { get; init; }

    public required IRedeProvider Rede { get; init; }

    public required IGpuProvider Gpu { get; init; }

    public required ITesteMemoriaProvider Memoria { get; init; }

    public required ICargaCpuRunner CargaCpu { get; init; }

    public required IElevacaoService Elevacao { get; init; }

    /// <summary>
    /// Aceite do usuário para ler o disco por pass-through. O provider consulta isto antes de abrir o
    /// disco com acesso de escrita (D78); a interface registra a resposta do aviso.
    /// </summary>
    public required IAutorizacaoLeituraDisco LeituraDiscoAutorizada { get; init; }

    public required WmiFonteAuditada Wmi { get; init; }

    public string Versao { get; } = typeof(Servicos).Assembly.GetName().Version?.ToString(3) ?? "0.5.0";

    public static Servicos Criar(bool demonstracao)
    {
        var ambiente = new AmbienteSistema { ModoDemonstracao = demonstracao };
        var wmi = new WmiFonteAuditada(demonstracao ? WmiFonteFake.DeJson(Recursos.FixtureWmi) : new WmiFonte());


        if (demonstracao)
        {
            return new Servicos
            {
                Ambiente = ambiente,
                Demonstracao = true,
                Wmi = wmi,
                Inventario = new FakeInventarioProvider(Fixtures.Inventario()),
                Sensores = new FakeSensorProviderFactory(() => new FakeSensorProvider(FakeSensorProvider.Rampa(48, 0.6))),
                Discos = new FakeDiscoProvider(Fixtures.Discos()),
                Bateria = new FakeBateriaProvider(Fixtures.Bateria()),
                Rede = new FakeRedeProvider(Fixtures.Rede()),
                Gpu = new GpuProviderIndisponivel("Modo demonstração: teste de GPU não é executado com dados fictícios; o inventário de GPU aparece em Sistema."),
                Memoria = new MemoriaIndisponivel("Teste de memória indisponível no modo de demonstração."),
                CargaCpu = new CargaCpuIndisponivel("Carga de CPU indisponível no modo de demonstração."),
                Elevacao = new FakeElevacaoService(),
                LeituraDiscoAutorizada = new AutorizacaoLeituraDisco(),
            };
        }

        var leituraDiscoAutorizada = new AutorizacaoLeituraDisco();
        return new Servicos
        {
            Ambiente = ambiente,
            Demonstracao = false,
            Wmi = wmi,
            Inventario = new InventarioWmiProvider(wmi, ambiente, new DxgiInfo()),
            Sensores = new FlavosDiag.Providers.Sensores.SensoresHardwareFactory(),
            Discos = new FlavosDiag.Providers.Discos.DiscosWindowsProvider(ambiente, leituraDiscoAutorizada),
            Bateria = new FlavosDiag.Providers.Bateria.BateriaWindowsProvider(),
            Rede = new FlavosDiag.Providers.Rede.RedeWindowsProvider(),
            Gpu = new FlavosDiag.Providers.Gpu.GpuD3D11Provider(new FlavosDiag.Providers.Sensores.SensoresHardwareFactory()),
            Memoria = new FlavosDiag.Workloads.TesteMemoriaRunner(),
            CargaCpu = new FlavosDiag.Workloads.CargaCpuRunner(),
            Elevacao = new ElevacaoService(ambiente),
            LeituraDiscoAutorizada = leituraDiscoAutorizada,
        };
    }

    public void Dispose()
    {
        // Nada a liberar hoje: sensores são abertos por sessão (ISensorProviderFactory).
    }
}
