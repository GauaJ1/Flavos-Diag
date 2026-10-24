using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Core.Abstractions;

/// <summary>Coleta o inventário completo (E1). Implementações não devem lançar: cada campo carrega seu estado.</summary>
public interface IInventarioProvider
{
    string Nome { get; }

    Task<InventarioSistema> ColetarAsync(CancellationToken cancelamento = default);
}

/// <summary>Sessão de leitura de sensores (E2).</summary>
public interface ISensorProvider : IAsyncDisposable
{
    string Nome { get; }

    /// <summary>Abre a sessão. Se não for possível, devolve a causa (ex.: sem elevação).</summary>
    Task<Leitura<bool>> AbrirAsync(CancellationToken cancelamento = default);

    /// <summary>Ids de sensores expostos pelo hardware (depois de AbrirAsync).</summary>
    IReadOnlyList<ValorSensor> SensoresDisponiveis { get; }

    /// <summary>Lê uma amostra de todos os sensores.</summary>
    AmostraSensores Ler();
}

/// <summary>Fábrica de sessões de sensores — permite fechar/abrir o monitor de hardware.</summary>
public interface ISensorProviderFactory
{
    ISensorProvider Criar();
}

/// <summary>Leitura de discos: identificação, SMART/NVMe/fallback, varredura, verificação e benchmark (E3).</summary>
public interface IDiscoProvider
{
    string Nome { get; }

    Task<IReadOnlyList<DiscoInfo>> ListarAsync(CancellationToken cancelamento = default);

    Task<ResultadoBenchmarkDisco> BenchmarkAsync(string pasta, long tamanhoBytes, IProgress<double>? progresso, CancellationToken cancelamento);

    Task<ResultadoVarreduraSuperficie> VarrerSuperficieAsync(string caminhoArquivo, IProgress<double>? progresso, CancellationToken cancelamento);

    Task<ResultadoVerificacaoSistemaArquivos> VerificarSistemaArquivosAsync(string volume, CancellationToken cancelamento = default);
}

public interface IBateriaProvider
{
    string Nome { get; }

    Task<InfoBateriaDetalhe> LerAsync(CancellationToken cancelamento = default);
}

public interface IRedeProvider
{
    string Nome { get; }

    Task<InfoRede> LerAsync(IReadOnlyList<string> alvos, string? nomeDns, CancellationToken cancelamento = default);

    Task<ResultadoVelocidadeRede> TestarVelocidadeAsync(string urlServidor, long bytes, CancellationToken cancelamento = default);
}

/// <summary>Elevação on-demand (UAC). Nunca instala nada; apenas reabre o processo com mais privilégios.</summary>
public interface IElevacaoService
{
    bool Elevado { get; }

    /// <summary>Explica o que exige elevação e por quê.</summary>
    string ExplicacaoElevacao { get; }

    Task<bool> SolicitarAsync(CancellationToken cancelamento = default);
}

public interface IGpuProvider
{
    string Nome { get; }

    Task<IReadOnlyList<InfoGpu>> ListarAsync(CancellationToken cancelamento = default);

    Task<ResultadoGpu> TestarAsync(ConfiguracaoEstresse configuracao, Func<bool> deveParar, IProgress<ResultadoEstresse>? progresso, CancellationToken cancelamento);
}

public interface ITesteMemoriaProvider
{
    /// <summary>
    /// Executa o teste de memória. <paramref name="bytes"/> &lt;= 0 usa o tamanho padrão seguro.
    /// O cancelamento é verificado a cada bloco de trabalho.
    /// </summary>
    Task<ResultadoTesteMemoria> TestarAsync(long bytes, IProgress<double>? progresso, CancellationToken cancelamento);
}

/// <summary>Executa cargas de estresse/benchmark de CPU.</summary>
public interface ICargaCpuRunner
{
    Task<ResultadoEstresse> EstressarAsync(
        ConfiguracaoEstresse configuracao,
        int nucleosFisicos,
        int threadsLogicas,
        Func<AmostraSensores?> leituraSensores,
        IProgress<ProgressoEstresse>? progresso,
        CancellationToken cancelamento);

    Task<ResultadoBenchmarkCpu> BenchmarkAsync(int threads, IProgress<string>? progresso, CancellationToken cancelamento);
}

public sealed record ProgressoEstresse(TimeSpan Decorrido, int Ciclos, int Erros, double? TemperaturaCelsius, double? ClockMhz);
