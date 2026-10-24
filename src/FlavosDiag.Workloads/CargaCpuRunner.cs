using System.Diagnostics;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Benchmark;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Workloads;

/// <summary>
/// Runner de estresse e benchmark de CPU com as travas de segurança obrigatórias:
/// <list type="bullet">
/// <item>duração máxima obrigatória (nunca "infinito");</item>
/// <item>parada automática por temperatura no limite configurado;</item>
/// <item>teto de 2 minutos quando não há sensor de temperatura;</item>
/// <item>parada imediata por pedido do usuário (a carga roda em threads de trabalho);</item>
/// <item>motivo da parada sempre registrado no resultado.</item>
/// </list>
/// Não toca em hardware além de consumir CPU: nenhuma escrita, nenhuma configuração.
/// </summary>
public sealed class CargaCpuRunner : ICargaCpuRunner
{
    /// <summary>Limite padrão de temperatura da CPU, conforme a especificação.</summary>
    public const double LimiteTemperaturaPadraoCelsius = 90.0;

    /// <summary>Teto obrigatório quando nenhum sensor de temperatura responde.</summary>
    public static readonly TimeSpan TetoSemSensor = TimeSpan.FromMinutes(2);

    /// <summary>Duração máxima aceita, para proteger o equipamento e o tempo do técnico.</summary>
    public static readonly TimeSpan DuracaoMaximaPermitida = TimeSpan.FromMinutes(60);

    private readonly Func<IReadOnlyList<Func<ICargaCpu>>, ExecutorCarga> _criarExecutor;

    public CargaCpuRunner(Func<IReadOnlyList<Func<ICargaCpu>>, ExecutorCarga>? criarExecutor = null)
    {
        _criarExecutor = criarExecutor ?? (f => new ExecutorCarga(f));
    }

    public async Task<ResultadoEstresse> EstressarAsync(
        ConfiguracaoEstresse configuracao,
        int nucleosFisicos,
        int threadsLogicas,
        Func<AmostraSensores?> leituraSensores,
        IProgress<ProgressoEstresse>? progresso,
        CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        var iniciado = DateTimeOffset.Now;
        var limite = configuracao.LimiteTemperaturaCelsius ?? LimiteTemperaturaPadraoCelsius;

        // Duração efetiva: nunca acima do máximo permitido, nunca acima do teto sem sensor.
        var solicitada = configuracao.DuracaoMaxima <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(1)
            : configuracao.DuracaoMaxima;
        if (solicitada > DuracaoMaximaPermitida)
        {
            solicitada = DuracaoMaximaPermitida;
        }

        var (temSensor, temperaturaInicialAmostra) = SondarTemperatura(leituraSensores);
        var avisoSemSensor = temSensor
            ? null
            : "Nenhum sensor de temperatura respondeu: o teste é limitado a 2 minutos por segurança.";

        var duracao = temSensor || solicitada <= TetoSemSensor ? solicitada : TetoSemSensor;

        var workers = Math.Max(1, threadsLogicas > 1 ? threadsLogicas : nucleosFisicos);

        var pararPorUsuario = false;
        var pararPorTemperatura = false;
        var temperaturaMaxima = double.NegativeInfinity;
        var somaTemperatura = 0.0;
        var amostrasTemperatura = 0;
        var instanteUltimaAmostra = TimeSpan.Zero;
        var jaAmostrou = false;
        double? ultimaTemperatura = null;

        // A lista de cargas precisa ser a padrão: uma lista vazia faria o executor rodar zero cargas,
        // sem trabalho e sem nunca chamar o predicado de parada.
        var executor = _criarExecutor(MontadorCarga.Fabrica());
        var relogio = Stopwatch.StartNew();

        // O executor reporta ciclos/erros; nós enriquecemos com a temperatura mais recente.
        var progressoInterno = progresso is null
            ? null
            : new Progress<ExecutorCarga.Progresso>(p => progresso.Report(new ProgressoEstresse(
                p.Decorrido,
                (int)Math.Min(int.MaxValue, p.Ciclos),
                p.Erros,
                ultimaTemperatura,
                null)));

        var resumo = await executor.ExecutarAsync(
            workers,
            configuracao.Perfil,
            duracao,
            () =>
            {
                if (cancelamento.IsCancellationRequested)
                {
                    pararPorUsuario = true;
                    return true;
                }

                // Amostragem de temperatura no máximo a cada segundo, dentro do predicado de parada,
                // para que a parada aconteça assim que o limite é atingido.
                // A PRIMEIRA chamada sempre lê: com o relógio de intervalo zerado, um teste curto
                // nunca chegaria a amostrar temperatura e o limite de segurança não valeria nada.
                // Marcamos o instante sobre o MESMO cronômetro do teste (misturar bases de tempo
                // de dois Stopwatch diferentes fazia a diferença dar negativo e nunca amostrar).
                if (jaAmostrou && relogio.Elapsed - instanteUltimaAmostra < TimeSpan.FromSeconds(1))
                {
                    return false;
                }

                jaAmostrou = true;
                instanteUltimaAmostra = relogio.Elapsed;

                if (leituraSensores() is not { } amostra || amostra.Valores.Count == 0)
                {
                    return false;
                }

                var temperatura = TemperaturaDaCpu(amostra);
                if (temperatura is null)
                {
                    return false;
                }

                ultimaTemperatura = temperatura;
                temperaturaMaxima = Math.Max(temperaturaMaxima, temperatura.Value);
                somaTemperatura += temperatura.Value;
                amostrasTemperatura++;

                if (temperatura.Value >= limite)
                {
                    pararPorTemperatura = true;
                    return true;
                }

                return false;
            },
            progressoInterno,
            cancelamento).ConfigureAwait(false);

        relogio.Stop();

        var motivo = pararPorTemperatura
            ? MotivoParada.Temperatura
            : pararPorUsuario
                ? MotivoParada.CanceladoPeloUsuario
                : !temSensor && solicitada > TetoSemSensor
                    ? MotivoParada.SemSensorTemperaturaLimite2Min
                    : MotivoParada.ConcluidoPorTempo;

        var texto = motivo switch
        {
            MotivoParada.Temperatura =>
                $"Parado ao atingir {limite:0} °C (limite configurado).",
            MotivoParada.CanceladoPeloUsuario =>
                "Parado a pedido do usuário.",
            MotivoParada.SemSensorTemperaturaLimite2Min =>
                "Parado no teto de 2 minutos: não há sensor de temperatura para proteger o equipamento.",
            _ =>
                $"Concluído: a duração solicitada ({FormatacaoBr.Duracao(duracao)}) terminou sem atingir o limite.",
        };

        Leitura<double> temperaturaMax = amostrasTemperatura > 0
            ? Leitura<double>.Ok(Math.Round(temperaturaMaxima, 1), "sensor de CPU")
            : Leitura<double>.NaoSuportado(
                temSensor
                    ? "Nenhuma amostra de temperatura foi coletada durante o teste."
                    : "Este equipamento não expôs sensor de temperatura para a CPU.");

        Leitura<double> temperaturaMedia = amostrasTemperatura > 0
            ? Leitura<double>.Ok(Math.Round(somaTemperatura / amostrasTemperatura, 1), "sensor de CPU")
            : Leitura<double>.NaoSuportado("Sem amostras de temperatura.");

        return new ResultadoEstresse
        {
            IniciadoEm = iniciado,
            DuracaoReal = resumo.DuracaoEfetiva,
            DuracaoSolicitada = duracao,
            Perfil = configuracao.Perfil,
            MotivoParada = motivo,
            MotivoParadaTexto = texto,
            Workers = workers,
            CiclosTotais = resumo.Ciclos,
            ErrosDeCalculo = resumo.Erros,
            TemperaturaMaximaCelsius = temperaturaMax,
            TemperaturaMediaCelsius = temperaturaMedia,
            ClockSustentadoMhz = ClockSustentado(leituraSensores),
            ThrottlingDetectado = DetectarThrottling(temperaturaMaxima, amostrasTemperatura, limite),
            LimiteTemperaturaCelsius = limite,
            AvisoSemSensor = avisoSemSensor,
        };
    }

    /// <summary>
    /// Benchmark de CPU: mede unidades/segundo em janelas de 3 s e usa a mediana de 5 amostras
    /// (conforme a especificação), com a pontuação normalizada do próprio algoritmo.
    /// </summary>
    public Task<ResultadoBenchmarkCpu> BenchmarkAsync(int threads, IProgress<string>? progresso, CancellationToken cancelamento)
    {
        return Task.Run(() =>
        {
            const int amostras = 5;
            var janela = TimeSpan.FromSeconds(3);
            var fabricas = MontadorCarga.Fabrica();
            var executadoEm = DateTimeOffset.Now;

            // Single e multi medem A MESMA carga (a primeira da lista). Antes, o multi somava ciclos
            // das quatro cargas enquanto o single media só a primeira: os dois números não eram
            // comparáveis entre si e a relação "single × núcleos ≈ multi" nunca fechava.
            var workers = Math.Max(1, threads);

            var linhas = new List<double>(amostras);
            for (var i = 0; i < amostras; i++)
            {
                cancelamento.ThrowIfCancellationRequested();
                progresso?.Report($"Amostra {i + 1} de {amostras} (1 thread)…");

                var carga = fabricas[0]();
                var (unidades, _) = MedidorSingle.Medir(
                    carga,
                    MontadorCarga.Tamanho(PerfilCarga.Pesado, carga.Id),
                    janela);
                linhas.Add(unidades);
            }

            var mult = new List<double>(amostras);
            for (var i = 0; i < amostras; i++)
            {
                cancelamento.ThrowIfCancellationRequested();
                progresso?.Report($"Amostra {i + 1} de {amostras} ({workers} threads)…");

                // Apenas a carga de referência, repetida em cada worker — a mesma do modo single.
                var cargaRef = fabricas[0];
                var executor = new ExecutorCarga(new[] { cargaRef });
                var resumo = executor
                    .ExecutarAsync(workers, PerfilCarga.Pesado, janela, () => false, null, cancelamento)
                    .GetAwaiter()
                    .GetResult();
                var segundos = Math.Max(0.001, resumo.DuracaoEfetiva.TotalSeconds);
                mult.Add(resumo.Ciclos / segundos);
            }

            var pontosSingle = PontuacaoCpu.Pontos(PontuacaoCpu.Mediana(linhas));
            var pontosMulti = PontuacaoCpu.Pontos(PontuacaoCpu.Mediana(mult));
            var dispersaoSingle = PontuacaoCpu.Dispersao(linhas);
            var dispersaoMulti = PontuacaoCpu.Dispersao(mult);

            progresso?.Report("Benchmark concluído.");

            var ressalva = PontuacaoCpu.AvisoDeInstabilidade(dispersaoSingle, dispersaoMulti);
            var aviso = $"Pontuação própria do Flavos Diag (versão {PontuacaoCpu.VersaoAtual}). " +
                        "Não comparável com outros programas; válida apenas entre execuções do próprio Flavos Diag.";

            return new ResultadoBenchmarkCpu
            {
                ExecutadoEm = executadoEm,
                VersaoAlgoritmo = PontuacaoCpu.VersaoAtual,
                PontuacaoSingle = Math.Round(pontosSingle, 1),
                PontuacaoMulti = Math.Round(pontosMulti, 1),
                AmostrasSingle = linhas.Select(l => Math.Round(PontuacaoCpu.Pontos(l), 1)).ToList(),
                AmostrasMulti = mult.Select(l => Math.Round(PontuacaoCpu.Pontos(l), 1)).ToList(),
                ThreadsMulti = workers,
                TextoAviso = ressalva is null ? aviso : aviso + " " + ressalva,
            };
        }, cancelamento);
    }

    /// <summary>
    /// Pontuação a partir de unidades por segundo. Mantida como atalho para chamadores antigos;
    /// a fórmula vive em <see cref="PontuacaoCpu"/>, para não haver duas escalas divergentes.
    /// </summary>
    public static double Pontuacao(double unidadesPorSegundo) =>
        Math.Round(PontuacaoCpu.Pontos(unidadesPorSegundo), 1);

    /// <summary>Mediana de uma lista (nunca depende da ordem de chegada).</summary>
    public static double Mediana(IReadOnlyList<double> valores) =>
        double.IsNaN(PontuacaoCpu.Mediana(valores)) ? 0 : PontuacaoCpu.Mediana(valores);

    private static (bool TemSensor, double? Inicial) SondarTemperatura(Func<AmostraSensores?> leitura)
    {
        try
        {
            if (leitura() is not { } amostra)
            {
                return (false, null);
            }

            var temperatura = TemperaturaDaCpu(amostra);
            return (temperatura is not null, temperatura);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, null);
        }
    }

    /// <summary>
    /// Temperatura representativa da CPU: a maior leitura de temperatura de categoria CPU/placa.
    /// Devolve <c>null</c> quando não há nenhuma (nunca zero, que seria uma leitura falsa).
    /// </summary>
    public static double? TemperaturaDaCpu(AmostraSensores amostra)
    {
        var temperaturas = amostra.Valores
            .Where(v => v.Categoria == CategoriaSensor.Temperatura)
            .Select(v => v.Valor)
            .Where(v => v > 0 && v < 150)
            .ToList();

        return temperaturas.Count == 0 ? null : temperaturas.Max();
    }

    private static Leitura<double> ClockSustentado(Func<AmostraSensores?> leitura)
    {
        try
        {
            if (leitura() is not { } amostra)
            {
                return Leitura<double>.NaoSuportado("Sem leitura de sensores durante o teste.");
            }

            var clocks = amostra.Valores
                .Where(v => v.Categoria == CategoriaSensor.Clock && v.Valor > 0)
                .Select(v => v.Valor)
                .ToList();

            return clocks.Count == 0
                ? Leitura<double>.NaoSuportado("Este equipamento não expõe clock da CPU.")
                : Leitura<double>.Ok(Math.Round(clocks.Max(), 0), "sensor de clock");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Leitura<double>.NaoSuportado("Falha ao ler o clock: " + ex.Message);
        }
    }

    /// <summary>
    /// Throttling = a temperatura chegou perto do limite durante a carga. Sem amostras não há como
    /// afirmar nada: devolve "sem leitura" em vez de "não houve throttling".
    /// </summary>
    private static Leitura<bool> DetectarThrottling(double maxima, int amostras, double limite)
    {
        if (amostras == 0)
        {
            return Leitura<bool>.NaoSuportado("Sem amostras de temperatura, não é possível avaliar throttling.");
        }

        var proximo = maxima >= limite * 0.95;
        return Leitura<bool>.Ok(proximo, "temperatura próxima do limite");
    }
}
