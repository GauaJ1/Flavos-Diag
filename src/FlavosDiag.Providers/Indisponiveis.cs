using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Providers;

// Implementações provisórias das fases 2–4. Existem para manter o projeto compilando e para que
// nenhuma tela exiba dado inventado: cada chamada devolve estado "não suportado" com causa.
// São substituídas, nesta ordem, pelas implementações reais e pelos fakes de teste.

public sealed class SensoresIndisponiveis : ISensorProviderFactory
{
    public ISensorProvider Criar() => new SessaoSensoresIndisponivel();
}

public sealed class SessaoSensoresIndisponivel : ISensorProvider
{
    public string Nome => "Indisponível nesta versão";

    public IReadOnlyList<ValorSensor> SensoresDisponiveis => Array.Empty<ValorSensor>();

    public Task<Leitura<bool>> AbrirAsync(CancellationToken cancelamento = default) =>
        Task.FromResult(Leitura<bool>.NaoSuportado("Leitura de sensores ainda não habilitada nesta versão."));

    public AmostraSensores Ler() => new()
    {
        Quando = DateTimeOffset.Now,
        Valores = Array.Empty<ValorSensor>(),
        CausaAusencia = "Leitura de sensores ainda não habilitada nesta versão.",
    };

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class DiscosIndisponiveis : IDiscoProvider
{
    public string Nome => "Indisponível nesta versão";

    public Task<IReadOnlyList<DiscoInfo>> ListarAsync(CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<DiscoInfo>>(Array.Empty<DiscoInfo>());

    public Task<ResultadoBenchmarkDisco> BenchmarkAsync(string pasta, long tamanhoBytes, IProgress<double>? progresso, CancellationToken cancelamento) =>
        Task.FromException<ResultadoBenchmarkDisco>(new NotSupportedException("Benchmark de disco ainda não habilitado nesta versão."));

    public Task<ResultadoVarreduraSuperficie> VarrerSuperficieAsync(string caminhoArquivo, IProgress<double>? progresso, CancellationToken cancelamento) =>
        Task.FromException<ResultadoVarreduraSuperficie>(new NotSupportedException("Varredura de superfície ainda não habilitada nesta versão."));

    public Task<ResultadoVerificacaoSistemaArquivos> VerificarSistemaArquivosAsync(string volume, CancellationToken cancelamento = default) =>
        Task.FromException<ResultadoVerificacaoSistemaArquivos>(new NotSupportedException("Verificação de sistema de arquivos ainda não habilitada nesta versão."));
}

public sealed class BateriasIndisponivel : IBateriaProvider
{
    public string Nome => "Indisponível nesta versão";

    public Task<InfoBateriaDetalhe> LerAsync(CancellationToken cancelamento = default) =>
        Task.FromResult(new InfoBateriaDetalhe
        {
            Presente = false,
            Nome = Leitura<string>.NaoSuportado("Leitura detalhada de bateria ainda não habilitada nesta versão."),
            CapacidadeProjetoMwh = Leitura<int>.NaoSuportado("Leitura detalhada de bateria ainda não habilitada nesta versão."),
            CapacidadeAtualMwh = Leitura<int>.NaoSuportado("Leitura detalhada de bateria ainda não habilitada nesta versão."),
            Ciclos = Leitura<int>.NaoSuportado("Leitura detalhada de bateria ainda não habilitada nesta versão."),
            Fabricante = Leitura<string>.NaoSuportado("Leitura detalhada de bateria ainda não habilitada nesta versão."),
            NumeroSerie = Leitura<string>.NaoSuportado("Leitura detalhada de bateria ainda não habilitada nesta versão."),
            Estado = Leitura<string>.NaoSuportado("Leitura detalhada de bateria ainda não habilitada nesta versão."),
            SaudePercentual = Leitura<double>.NaoSuportado("Leitura detalhada de bateria ainda não habilitada nesta versão."),
            ColetadoEm = DateTimeOffset.Now,
            Limitacoes = new[] { new DadoAusente("Bateria", "Leitura detalhada ainda não habilitada nesta versão.") },
        });
}

public sealed class RedeIndisponivel : IRedeProvider
{
    public string Nome => "Indisponível nesta versão";

    public Task<InfoRede> LerAsync(IReadOnlyList<string> alvos, string? nomeDns, CancellationToken cancelamento = default) =>
        Task.FromResult(new InfoRede
        {
            Adaptadores = Array.Empty<InfoAdaptadorRede>(),
            EnderecoIp = Leitura<string>.NaoSuportado("Testes de rede ainda não habilitados nesta versão."),
            Gateway = Leitura<string>.NaoSuportado("Testes de rede ainda não habilitados nesta versão."),
            ServidoresDns = Leitura<string>.NaoSuportado("Testes de rede ainda não habilitados nesta versão."),
            Wifi = new StatusWifi
            {
                Ssid = Leitura<string>.NaoSuportado("Não habilitado."),
                QualidadePercentual = Leitura<int>.NaoSuportado("Não habilitado."),
                ForcaDbm = Leitura<int>.NaoSuportado("Não habilitado."),
            },
            LatenciaGateway = Leitura<ResultadoLatencia>.NaoSuportado("Testes de rede ainda não habilitados nesta versão."),
            Alvos = Array.Empty<ResultadoLatencia>(),
            Dns = Leitura<ResultadoDns>.NaoSuportado("Não habilitado."),
            ColetadoEm = DateTimeOffset.Now,
            Limitacoes = new[] { new DadoAusente("Rede", "Testes de rede ainda não habilitados nesta versão.") },
        });

    public Task<ResultadoVelocidadeRede> TestarVelocidadeAsync(string urlServidor, long bytes, CancellationToken cancelamento = default) =>
        Task.FromResult(new ResultadoVelocidadeRede { Servidor = urlServidor, DownloadMbps = 0, BytesRecebidos = 0, Segundos = 0, Erro = "Não habilitado." });
}

public sealed class GpuProviderIndisponivel : IGpuProvider
{
    private readonly string _causa;

    public GpuProviderIndisponivel(string causa)
    {
        _causa = causa;
    }

    public string Nome => "Indisponível nesta versão";

    public Task<IReadOnlyList<InfoGpu>> ListarAsync(CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<InfoGpu>>(Array.Empty<InfoGpu>());

    public Task<ResultadoGpu> TestarAsync(ConfiguracaoEstresse configuracao, Func<bool> deveParar, IProgress<ResultadoEstresse>? progresso, CancellationToken cancelamento) =>
        Task.FromException<ResultadoGpu>(new NotSupportedException(_causa));
}

public sealed class MemoriaIndisponivel : ITesteMemoriaProvider
{
    private readonly string _causa;

    public MemoriaIndisponivel(string causa)
    {
        _causa = causa;
    }

    public Task<ResultadoTesteMemoria> TestarAsync(long bytes, IProgress<double>? progresso, CancellationToken cancelamento) =>
        Task.FromException<ResultadoTesteMemoria>(new NotSupportedException(_causa));
}

public sealed class CargaCpuIndisponivel : ICargaCpuRunner
{
    private readonly string _causa;

    public CargaCpuIndisponivel(string causa)
    {
        _causa = causa;
    }

    public Task<ResultadoEstresse> EstressarAsync(ConfiguracaoEstresse configuracao, int nucleosFisicos, int threadsLogicas, Func<AmostraSensores?> leituraSensores, IProgress<ProgressoEstresse>? progresso, CancellationToken cancelamento) =>
        Task.FromException<ResultadoEstresse>(new NotSupportedException(_causa));

    public Task<ResultadoBenchmarkCpu> BenchmarkAsync(int threads, IProgress<string>? progresso, CancellationToken cancelamento) =>
        Task.FromException<ResultadoBenchmarkCpu>(new NotSupportedException(_causa));
}
