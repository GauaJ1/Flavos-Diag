using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Abstractions;
using FlavosDiag.Providers.Wmi;

namespace FlavosDiag.Providers.Fakes;

/// <summary>
/// Fonte WMI baseada em fixtures (JSON) ou em um dicionário de linhas.
/// Usada SOMENTE em testes e no modo demonstração — nunca em produção.
/// </summary>
public sealed class WmiFonteFake : IWmiFonte
{
    private readonly Dictionary<string, IReadOnlyList<WmiLinha>> _dados;

    public WmiFonteFake(Dictionary<string, IReadOnlyList<WmiLinha>> dados)
    {
        _dados = dados;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<WmiLinha>> Dados => _dados;

    public Task<IReadOnlyList<WmiLinha>> ConsultarAsync(string consulta, string? namespaceWmi = "root\\cimv2", CancellationToken cancelamento = default)
    {
        var classe = WmiFonteAuditada.ExtrairClasse(consulta);
        if (_dados.TryGetValue(classe, out var linhas))
        {
            return Task.FromResult(linhas);
        }

        return Task.FromResult<IReadOnlyList<WmiLinha>>(Array.Empty<WmiLinha>());
    }

    public static WmiFonteFake DeJson(string json)
    {
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var dict = new Dictionary<string, IReadOnlyList<WmiLinha>>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                continue;
            }

            var linhas = new List<WmiLinha>();
            foreach (var item in prop.Value.EnumerateArray())
            {
                var linha = new WmiLinha();
                foreach (var campo in item.EnumerateObject())
                {
                    linha[campo.Name] = campo.Value.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => campo.Value.GetString(),
                        System.Text.Json.JsonValueKind.Number => campo.Value.TryGetInt64(out var l) ? l : campo.Value.GetDouble(),
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        _ => null,
                    };
                }

                linhas.Add(linha);
            }

            dict[prop.Name] = linhas;
        }

        return new WmiFonteFake(dict);
    }
}

public sealed class FakeAmbiente : IAmbiente
{
    public bool EhAdministrador { get; init; } = true;

    public bool ModoDemonstracao { get; init; } = true;

    public string PastaUsuario { get; init; } = Path.Combine(Path.GetTempPath(), "flavos-diag-fake");
}

public sealed class FakeInventarioProvider : IInventarioProvider
{
    private readonly Func<InventarioSistema> _criar;

    public FakeInventarioProvider(InventarioSistema modelo)
    {
        _criar = () => modelo;
    }

    public FakeInventarioProvider(Func<InventarioSistema> criar)
    {
        _criar = criar;
    }

    public string Nome => "Inventário de demonstração (dados fictícios)";

    public Task<InventarioSistema> ColetarAsync(CancellationToken cancelamento = default) => Task.FromResult(_criar());
}

public sealed class FakeSensorProviderFactory : ISensorProviderFactory
{
    private readonly Func<ISensorProvider> _criar;

    public FakeSensorProviderFactory(Func<ISensorProvider> criar)
    {
        _criar = criar;
    }

    public ISensorProvider Criar() => _criar();
}

/// <summary>Sessão de sensores fake com séries programáveis (rampa de temperatura etc.).</summary>
public sealed class FakeSensorProvider : ISensorProvider
{
    private readonly Func<int, IReadOnlyList<ValorSensor>> _gerador;
    private int _amostra;
    private readonly Leitura<bool> _abertura;

    public FakeSensorProvider(Func<int, IReadOnlyList<ValorSensor>> gerador, Leitura<bool>? abertura = null)
    {
        _gerador = gerador;
        _abertura = abertura ?? Leitura<bool>.Ok(true);
    }

    public string Nome => "Sensores de demonstração";

    public IReadOnlyList<ValorSensor> SensoresDisponiveis { get; private set; } = Array.Empty<ValorSensor>();

    public Task<Leitura<bool>> AbrirAsync(CancellationToken cancelamento = default)
    {
        SensoresDisponiveis = _abertura.Disponivel ? _gerador(0) : Array.Empty<ValorSensor>();
        return Task.FromResult(_abertura);
    }

    public AmostraSensores Ler()
    {
        if (!_abertura.Disponivel)
        {
            return new AmostraSensores { Quando = DateTimeOffset.Now, Valores = Array.Empty<ValorSensor>(), CausaAusencia = _abertura.Causa };
        }

        var valores = _gerador(_amostra++);
        return new AmostraSensores { Quando = DateTimeOffset.Now, Valores = valores };
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Série de temperatura do "CPU Package" subindo linearmente.</summary>
    public static Func<int, IReadOnlyList<ValorSensor>> Rampa(double inicio = 40, double passo = 1.0) => i => new[]
    {
        new ValorSensor { Id = "cpu/temp", Categoria = CategoriaSensor.Temperatura, Rotulo = "CPU Temperatura", Valor = inicio + i * passo, Unidade = "°C" },
        new ValorSensor { Id = "cpu/clock", Categoria = CategoriaSensor.Clock, Rotulo = "CPU Clock", Valor = 4200 - Math.Min(i * 10, 800), Unidade = "MHz" },
        new ValorSensor { Id = "cpu/load", Categoria = CategoriaSensor.Carga, Rotulo = "CPU Carga", Valor = 100, Unidade = "%" },
    };
}

public sealed class FakeDiscoProvider : IDiscoProvider
{
    private readonly IReadOnlyList<DiscoInfo> _discos;

    public FakeDiscoProvider(IReadOnlyList<DiscoInfo> discos)
    {
        _discos = discos;
    }

    public string Nome => "Discos de demonstração (dados fictícios)";

    public Task<IReadOnlyList<DiscoInfo>> ListarAsync(CancellationToken cancelamento = default) => Task.FromResult(_discos);

    public Task<ResultadoBenchmarkDisco> BenchmarkAsync(string pasta, long tamanhoBytes, IProgress<double>? progresso, CancellationToken cancelamento) =>
        Task.FromResult(new ResultadoBenchmarkDisco
        {
            ExecutadoEm = DateTimeOffset.Now,
            Pasta = pasta,
            TamanhoArquivoBytes = tamanhoBytes,
            SeqLeituraMbps = 520,
            SeqEscritaMbps = 480,
            Aleatorio4kLeituraIops = 38000,
            LatenciaMediaMs = 0.4,
            LatenciaP95Ms = 1.1,
        });

    public Task<ResultadoVarreduraSuperficie> VarrerSuperficieAsync(string caminhoArquivo, IProgress<double>? progresso, CancellationToken cancelamento) =>
        Task.FromResult(new ResultadoVarreduraSuperficie
        {
            ExecutadoEm = DateTimeOffset.Now,
            CaminhoArquivo = caminhoArquivo,
            TotalBytes = 1024 * 1024 * 64,
            BlocosLidos = 16,
            BlocosLentos = 0,
            BlocosComErro = 0,
            TempoTotalSegundos = 3,
            Cancelada = false,
            Blocos = Array.Empty<BlocoSuperficie>(),
        });

    public Task<ResultadoVerificacaoSistemaArquivos> VerificarSistemaArquivosAsync(string volume, CancellationToken cancelamento = default) =>
        Task.FromResult(new ResultadoVerificacaoSistemaArquivos
        {
            ExecutadoEm = DateTimeOffset.Now,
            Volume = volume,
            CodigoSaida = Leitura<int>.Ok(0),
            CanceladaOuInterrompida = false,
            MensagensRelevantes = new[] { "Varredura somente leitura concluída (dados de demonstração)." },
            TextoResumo = "Nenhum problema encontrado.",
        });
}

public sealed class FakeBateriaProvider : IBateriaProvider
{
    private readonly InfoBateriaDetalhe _info;

    public FakeBateriaProvider(InfoBateriaDetalhe info)
    {
        _info = info;
    }

    public string Nome => "Bateria de demonstração (dados fictícios)";

    public Task<InfoBateriaDetalhe> LerAsync(CancellationToken cancelamento = default) => Task.FromResult(_info);
}

public sealed class FakeRedeProvider : IRedeProvider
{
    private readonly InfoRede _info;

    public FakeRedeProvider(InfoRede info)
    {
        _info = info;
    }

    public string Nome => "Rede de demonstração (dados fictícios)";

    public Task<InfoRede> LerAsync(IReadOnlyList<string> alvos, string? nomeDns, CancellationToken cancelamento = default) => Task.FromResult(_info);

    public Task<ResultadoVelocidadeRede> TestarVelocidadeAsync(string urlServidor, long bytes, CancellationToken cancelamento = default) =>
        Task.FromResult(new ResultadoVelocidadeRede { Servidor = urlServidor, DownloadMbps = 0, BytesRecebidos = 0, Segundos = 0, Erro = "Indisponível em modo demonstração." });
}

public sealed class FakeElevacaoService : IElevacaoService
{
    public bool Elevado { get; init; } = true;

    public string ExplicacaoElevacao => "Modo demonstração: a elevação não é solicitada.";

    public Task<bool> SolicitarAsync(CancellationToken cancelamento = default) => Task.FromResult(Elevado);
}
