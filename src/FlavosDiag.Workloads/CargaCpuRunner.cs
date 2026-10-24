using System.Diagnostics;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Benchmark;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;

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
    /// <summary>
    /// Limite padrão de temperatura da CPU quando a configuração não informa nenhum. Vem de
    /// <see cref="LimitesTermicos"/>: aqui não existe número próprio, para que o runner e o veredito
    /// nunca discordem sobre qual é o limite.
    /// </summary>
    public const double LimiteTemperaturaPadraoCelsius = LimitesTermicos.VereditoCpuCelsius;

    /// <summary>Teto obrigatório quando nenhum sensor de temperatura responde.</summary>
    public static readonly TimeSpan TetoSemSensor = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Teto quando o técnico confirma explicitamente que quer testar sem sensor de temperatura.
    /// Acima disso não vale a pena: sem proteção térmica, a informação ganha não paga o risco.
    /// </summary>
    public static readonly TimeSpan TetoSemSensorConfirmado = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Quantas amostras consecutivas acima do limite são necessárias para parar. Um pico isolado de
    /// leitura não deve abortar um teste saudável; uma temperatura realmente alta se sustenta.
    /// </summary>
    public const int AmostrasAcimaDoLimiteParaParar = 3;

    /// <summary>Duração máxima aceita, para proteger o equipamento e o tempo do técnico.</summary>
    public static readonly TimeSpan DuracaoMaximaPermitida = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Tempo máximo tolerado sem que o predicado de parada seja consultado. Acima disso o vigia
    /// interrompe a carga: um teste sem ninguém avaliando a temperatura não tem proteção.
    /// </summary>
    public static readonly TimeSpan IntervaloMaximoSemVerificacao = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan PeriodoVigia = TimeSpan.FromSeconds(1);

    /// <summary>Amostras consecutivas acima do máximo físico para a parada de emergência.</summary>
    public const int AmostrasConsecutivasEmergencia = 2;

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

        // Sem sensor, o teto é 2 min. O técnico pode estendê-lo confirmando na tela, mas nunca
        // além do teto estendido — e o valor confirmado fica registrado no resultado.
        var tetoSemSensor = configuracao.PermitirSemSensor ? TetoSemSensorConfirmado : TetoSemSensor;
        var duracao = temSensor || solicitada <= tetoSemSensor ? solicitada : tetoSemSensor;

        var workers = Math.Max(1, threadsLogicas > 1 ? threadsLogicas : nucleosFisicos);

        var pararPorUsuario = false;
        var pararPorTemperatura = false;
        var temperaturaMaxima = double.NegativeInfinity;
        var somaTemperatura = 0.0;
        var amostrasTemperatura = 0;
        var instanteUltimaAmostra = TimeSpan.Zero;
        var jaAmostrou = false;
        var amostrasAcimaDoLimite = 0;
        var amostrasEmergencia = 0;
        double? ultimaTemperatura = null;

        // Marca de tempo compartilhada com o vigia: atualizada a cada consulta ao predicado.
        long instanteUltimaVerificacao = 0;

        // A lista de cargas precisa ser a padrão: uma lista vazia faria o executor rodar zero cargas,
        // sem trabalho e sem nunca chamar o predicado de parada.
        var executor = _criarExecutor(MontadorCarga.Fabrica());
        var relogio = Stopwatch.StartNew();

        // Rede de segurança independente da interface: a decisão de parada térmica é tomada aqui,
        // no predicado, que roda em thread de trabalho. Se por algum motivo o predicado deixar de
        // ser consultado (executor travado, bloqueio inesperado), este vigia corta a carga em vez
        // de deixá-la rodando até o teto de duração.
        using var vigia = new CancellationTokenSource();
        using var vigiaRegistro = cancelamento.Register(() => vigia.Cancel());
        using var relogioVigia = new Timer(
            _ =>
            {
                try
                {
                    if (relogio.Elapsed.Ticks - Volatile.Read(ref instanteUltimaVerificacao) > IntervaloMaximoSemVerificacao.Ticks)
                    {
                        vigia.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // O teste já terminou; nada a fazer.
                }
            },
            null,
            PeriodoVigia,
            PeriodoVigia);

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
                // Sinaliza ao vigia que a proteção está viva e sendo avaliada.
                Volatile.Write(ref instanteUltimaVerificacao, relogio.Elapsed.Ticks);

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
                var leituraImplausivel = false;
                if (temperatura is null)
                {
                    // O classificador descarta tudo acima de 120 °C por não ser uma temperatura
                    // plausível. Mas se a leitura de um sensor de confiança alta está acima de
                    // 120 °C, isso é a hora de parar — não de "seguir sem leitura". Sentinelas
                    // conhecidos não chegam aqui (ver TemperaturaCruaAcimaDoFisico).
                    if (TemperaturaCruaAcimaDoFisico(amostra) is not { } crua)
                    {
                        amostrasEmergencia = 0;
                        return false;
                    }

                    // Duas amostras consecutivas: um pico isolado de leitura não derruba o teste.
                    amostrasEmergencia++;
                    if (amostrasEmergencia < AmostrasConsecutivasEmergencia)
                    {
                        return false;
                    }

                    // A parada usa o valor cru, mas ele NÃO entra nas estatísticas: 130 °C não é uma
                    // medição, é um sensor em estado inválido. Registrá-lo como "máxima" colocaria um
                    // número inventado no laudo.
                    temperatura = crua;
                    leituraImplausivel = true;
                }
                else
                {
                    // Leitura plausível quebra a sequência de emergência.
                    amostrasEmergencia = 0;
                }

                if (!leituraImplausivel)
                {
                    temperaturaMaxima = Math.Max(temperaturaMaxima, temperatura.Value);
                    somaTemperatura += temperatura.Value;
                    amostrasTemperatura++;
                    ultimaTemperatura = temperatura;
                }

                // Exigir amostras consecutivas acima do limite evita que um pico isolado de leitura
                // aborte um teste saudável. Os sentinelas (0, 255) já não chegam até aqui.
                if (temperatura.Value >= limite)
                {
                    amostrasAcimaDoLimite++;
                    if (amostrasAcimaDoLimite >= AmostrasAcimaDoLimiteParaParar)
                    {
                        pararPorTemperatura = true;
                        return true;
                    }
                }
                else
                {
                    amostrasAcimaDoLimite = 0;
                }

                return false;
            },
            progressoInterno,
            vigia.Token).ConfigureAwait(false);

        relogio.Stop();

        var motivo = pararPorTemperatura
            ? MotivoParada.Temperatura
            : pararPorUsuario
                ? MotivoParada.CanceladoPeloUsuario

                // O vigia cortou a carga porque a proteção deixou de ser avaliada.
                : vigia.IsCancellationRequested && !cancelamento.IsCancellationRequested
                    ? MotivoParada.Erro
                    : !temSensor && solicitada > TetoSemSensor
                        ? MotivoParada.SemSensorTemperaturaLimite2Min
                        : MotivoParada.ConcluidoPorTempo;

        var texto = motivo switch
        {
            MotivoParada.Temperatura =>
                $"Parado ao atingir {limite:0} °C (limite configurado).",
            MotivoParada.CanceladoPeloUsuario =>
                "Parado a pedido do usuário.",
            MotivoParada.Erro when vigia.IsCancellationRequested && !cancelamento.IsCancellationRequested =>
                "Parado pela proteção: a verificação de temperatura deixou de responder. " +
                "O resultado deste teste não deve ser usado como veredito.",
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
    public Task<ResultadoBenchmarkCpu> BenchmarkAsync(int threads, IProgress<string>? progresso, CancellationToken cancelamento) =>
        BenchmarkAsync(threads, progresso, cancelamento, null);

    /// <summary>
    /// Benchmark com proteção térmica. O benchmark também é carga total na CPU por vários segundos,
    /// então passar a leitura de sensores e o limite é o que impede um teste de pico de aquecer a
    /// máquina sem parada — antes ele rodava com um predicado fixo <c>() => false</c>, ou seja, sem
    /// checar temperatura nenhuma.
    /// </summary>
    /// <param name="leituraSensores">
    /// Leitura de sensores; <c>null</c> quando não há sessão aberta (aí não há proteção possível, e o
    /// benchmark é mais curto).
    /// </param>
    public Task<ResultadoBenchmarkCpu> BenchmarkAsync(
        int threads,
        IProgress<string>? progresso,
        CancellationToken cancelamento,
        Func<AmostraSensores?>? leituraSensores) =>
        BenchmarkAsync(threads, progresso, cancelamento, leituraSensores, LimiteTemperaturaPadraoCelsius, TimeSpan.FromSeconds(30));

    public Task<ResultadoBenchmarkCpu> BenchmarkAsync(
        int threads,
        IProgress<string>? progresso,
        CancellationToken cancelamento,
        Func<AmostraSensores?>? leituraSensores,
        double limiteCelsius,
        TimeSpan teto)
    {
        return Task.Run(() =>
        {
            var relogioBench = System.Diagnostics.Stopwatch.StartNew();
            var acimaDoLimite = 0;
            var emergencia = 0;

            // Mesma regra do estresse: para por temperatura de confiança alta sustentada, e o teto
            // de duração é absoluto. Sem sensor, só o teto protege — e ele é curto para o benchmark.
            bool DeveParar()
            {
                if (relogioBench.Elapsed >= teto)
                {
                    return true;
                }

                if (leituraSensores is null || relogioBench.Elapsed.TotalSeconds < 1)
                {
                    return false;
                }

                var amostra = leituraSensores();
                if (amostra is null)
                {
                    return false;
                }

                var temperatura = TemperaturaDaCpu(amostra);
                if (temperatura is { } temp && temp >= limiteCelsius)
                {
                    acimaDoLimite++;
                    return acimaDoLimite >= AmostrasAcimaDoLimiteParaParar;
                }

                acimaDoLimite = 0;

                if (TemperaturaCruaAcimaDoFisico(amostra) is not null)
                {
                    emergencia++;
                    return emergencia >= AmostrasConsecutivasEmergencia;
                }

                emergencia = 0;
                return false;
            }
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
                    .ExecutarAsync(workers, PerfilCarga.Pesado, janela, DeveParar, null, cancelamento)
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
    /// Temperatura de <b>confiança alta</b> da CPU: sensor do próprio processador
    /// (<see cref="TipoHardware.Cpu"/>) com <see cref="QualidadeSensor.Ok"/>, escolhido pelo
    /// classificador. Devolve <c>null</c> quando não há nenhuma (nunca zero, que seria leitura falsa).
    /// </summary>
    /// <remarks>
    /// Antes esta função pegava a maior temperatura qualquer acima de zero — inclusive o sensor da
    /// placa-mãe (SuperIO). Isso protegia o equipamento pelo sensor errado: a placa pode estar a
    /// 40 °C enquanto o processador cozinha. Agora só o sensor do próprio componente conta para a
    /// parada de segurança.
    /// </remarks>
    public static double? TemperaturaDaCpu(AmostraSensores amostra)
    {
        ArgumentNullException.ThrowIfNull(amostra);
        return ClassificadorTemperatura.DaCpu(amostra, out _);
    }

    /// <summary>
    /// Leitura de temperatura acima do máximo físico plausível (> 120 °C) em sensor de confiança alta
    /// da CPU ou da GPU. Situação de emergência: o valor é alto demais para ser medido.
    /// </summary>
    /// <remarks>
    /// Os sentinelas conhecidos (0, 255, -127, -128, NaN, infinito) devolvem <c>null</c>: 255 é maior
    /// que 120, então uma regra que só comparasse "acima de 120" trataria a sentinela clássica de
    /// "sem leitura" como superaquecimento e abortaria um teste saudável — exatamente o defeito que
    /// a classificação na origem existe para evitar. Sentinela é ausência de leitura, e ausência de
    /// leitura é tratada pelo teto sem sensor, nunca como calor.
    /// <para>
    /// Quem chama exige <b>duas</b> amostras consecutivas: um único pico de leitura não derruba o
    /// teste.
    /// </para>
    /// </remarks>
    public static double? TemperaturaCruaAcimaDoFisico(AmostraSensores amostra)
    {
        ArgumentNullException.ThrowIfNull(amostra);

        var acima = amostra.Valores
            .Where(v => v.Categoria == CategoriaSensor.Temperatura &&
                        v.TipoHardware is TipoHardware.Cpu or TipoHardware.Gpu &&
                        v.Qualidade == QualidadeSensor.Ok &&
                        !EhSentinelaConhecida(v.Valor) &&
                        v.Valor > TemperaturaFisica.MaximoValidoCelsius)
            .Select(v => v.Valor)
            .ToList();

        return acima.Count == 0 ? null : acima.Max();
    }

    /// <summary>
    /// Sentinela <b>conhecida</b>: os códigos fixos de "sem leitura".
    /// </summary>
    /// <remarks>
    /// Não dá para reaproveitar <see cref="TemperaturaFisica.EhSentinela"/> aqui. Aquela função
    /// responde "isto é uma temperatura utilizável?", e 130 °C não é — mas 130 °C é exatamente o
    /// valor que a emergência precisa ver. Usar a mesma função para as duas perguntas faria a
    /// emergência ignorar todo valor alto, que é o único caso em que ela serve para alguma coisa.
    /// </remarks>
    private static bool EhSentinelaConhecida(double valor) =>
        double.IsNaN(valor) || double.IsInfinity(valor) ||
        valor is 0 or 255 or -127 or -128;

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
