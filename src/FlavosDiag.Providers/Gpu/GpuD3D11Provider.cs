using System.Diagnostics;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace FlavosDiag.Providers.Gpu;

/// <summary>
/// GPU real: carga em Direct3D 11 fora de tela, detecção de artefatos por leitura de pixels e
/// monitoramento de reinício do driver (TDR).
///
/// Segurança e enquadramento: usa as APIs públicas do Windows (Direct3D 11/DXGI) por meio de bindings de
/// código aberto (MIT), não instala nada, não altera configuração da placa, não faz overclock e desenha
/// fora da tela. O limite de temperatura da GPU (85 °C por padrão) interrompe a carga.
///
/// Sem Direct3D disponível (driver ausente, máquina virtual sem aceleração), a categoria fica
/// Inconclusiva com a causa — nunca "OK" por omissão.
/// </summary>
public sealed class GpuD3D11Provider : IGpuProvider
{
    /// <summary>Limite padrão de temperatura da GPU, conforme a especificação.</summary>
    public const double LimiteTemperaturaPadraoCelsius = 85.0;

    /// <summary>Resolução do alvo de renderização: pequena o suficiente para rodar em qualquer GPU.</summary>
    public const int Largura = 256;

    public const int Altura = 256;

    /// <summary>Bytes de um pixel no formato R32G32B32A32_Float.</summary>
    public const int BytesPorPixel = 16;

    private readonly ISensorProviderFactory _sensores;

    public GpuD3D11Provider(ISensorProviderFactory sensores)
    {
        _sensores = sensores;
    }

    public string Nome => "Direct3D 11 (carga fora de tela)";

    public Task<IReadOnlyList<InfoGpu>> ListarAsync(CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<InfoGpu>>(Array.Empty<InfoGpu>());

    public Task<ResultadoGpu> TestarAsync(
        ConfiguracaoEstresse configuracao,
        Func<bool> deveParar,
        IProgress<ResultadoEstresse>? progresso,
        CancellationToken cancelamento)
    {
        return Task.Run(() => Executar(configuracao, deveParar, progresso, cancelamento));
    }

    /// <summary>Situação do dispositivo no início do teste.</summary>
    public static readonly TimeSpan TetoSemSensor = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan DuracaoMaximaPermitida = TimeSpan.FromMinutes(60);

    private ResultadoGpu Executar(
        ConfiguracaoEstresse configuracao,
        Func<bool> deveParar,
        IProgress<ResultadoEstresse>? progresso,
        CancellationToken cancelamento)
    {
        var executadoEm = DateTimeOffset.Now;
        var limite = configuracao.LimiteTemperaturaGpuCelsius ?? LimiteTemperaturaPadraoCelsius;

        using var sessao = SessaoGpuCriada.Criar();
        if (sessao is null)
        {
            return SemTeste(
                executadoEm,
                "Não foi possível iniciar a prova de GPU: " +
                (SessaoGpuCriada.UltimaFalha ?? "Direct3D 11 não está disponível neste equipamento.") +
                " A categoria fica Inconclusiva.");
        }

        var sensor = AbrirSensores();
        try
        {
            var iniciado = DateTimeOffset.Now;
            var solicitada = configuracao.DuracaoMaxima <= TimeSpan.Zero
                ? TimeSpan.FromMinutes(1)
                : configuracao.DuracaoMaxima;
            if (solicitada > DuracaoMaximaPermitida)
            {
                solicitada = DuracaoMaximaPermitida;
            }

            var temSensor = LerTemperaturaGpu(sensor) is not null;
            var duracao = temSensor || solicitada <= TetoSemSensor ? solicitada : TetoSemSensor;

            var relogio = Stopwatch.StartNew();
            long frames = 0;
            long pixelsDivergentes = 0;
            long primeiroDeslocamento = -1;
            var temperaturaMaxima = double.NegativeInfinity;
            var somaTemperatura = 0.0;
            var amostrasTemperatura = 0;
            double? ultimaTemperatura = null;
            var instanteUltimaAmostra = TimeSpan.Zero;
            var jaAmostrou = false;
            var parouPorTemperatura = false;
            var parouPorUsuario = false;
            var tdr = false;

            // Referência determinística: o mesmo shader com o mesmo frame deve produzir o mesmo resultado.
            var referencia = sessao.Executar(1);
            if (referencia is null)
            {
                return SemTeste(
                    executadoEm,
                    "O dispositivo Direct3D 11 foi criado (" + sessao.NomeAdaptador + "), mas a execução do shader " +
                    "falhou. A categoria fica Inconclusiva.",
                    sessao.NomeAdaptador);
            }

            var hashEsperado = referencia.Value.Hash;
            var amostrasFps = new List<double>();
            var inicioLote = Stopwatch.GetTimestamp();

            while (relogio.Elapsed < duracao)
            {
                if (cancelamento.IsCancellationRequested)
                {
                    parouPorUsuario = true;
                    break;
                }

                if (deveParar())
                {
                    parouPorUsuario = true;
                    break;
                }

                var atendido = sessao.Executar(1);
                if (atendido is null)
                {
                    tdr = true;
                    break;
                }

                frames += 1;
                if (atendido.Value.Hash != hashEsperado)
                {
                    pixelsDivergentes += 1;
                    if (primeiroDeslocamento < 0)
                    {
                        primeiroDeslocamento = atendido.Value.PrimeiroDeslocamentoDivergente;
                    }
                }

                if (frames % 20 == 0)
                {
                    var decorrido = (Stopwatch.GetTimestamp() - inicioLote) / (double)Stopwatch.Frequency;
                    if (decorrido > 0.001)
                    {
                        amostrasFps.Add(20.0 / decorrido);
                    }

                    inicioLote = Stopwatch.GetTimestamp();
                }

                if (jaAmostrou && relogio.Elapsed - instanteUltimaAmostra < TimeSpan.FromSeconds(1))
                {
                    continue;
                }

                jaAmostrou = true;
                instanteUltimaAmostra = relogio.Elapsed;

                if (LerTemperaturaGpu(sensor) is { } temperatura)
                {
                    ultimaTemperatura = temperatura;
                    temperaturaMaxima = Math.Max(temperaturaMaxima, temperatura);
                    somaTemperatura += temperatura;
                    amostrasTemperatura++;
                    if (temperatura >= limite)
                    {
                        parouPorTemperatura = true;
                        break;
                    }
                }

                progresso?.Report(new ResultadoEstresse
                {
                    IniciadoEm = iniciado,
                    DuracaoReal = relogio.Elapsed,
                    DuracaoSolicitada = duracao,
                    Perfil = configuracao.Perfil,
                    MotivoParada = MotivoParada.ConcluidoPorTempo,
                    MotivoParadaTexto = "em execução",
                    Workers = 1,
                    CiclosTotais = frames,
                    ErrosDeCalculo = (int)Math.Min(int.MaxValue, pixelsDivergentes),
                    TemperaturaMaximaCelsius = ultimaTemperatura is { } t
                        ? Leitura<double>.Ok(t, "sensor de GPU")
                        : Leitura<double>.NaoSuportado("sem leitura de temperatura da GPU"),
                    TemperaturaMediaCelsius = Leitura<double>.NaoSuportado("em execução"),
                    ClockSustentadoMhz = Leitura<double>.NaoSuportado("em execução"),
                    ThrottlingDetectado = Leitura<bool>.NaoSuportado("em execução"),
                    LimiteTemperaturaCelsius = limite,
                });
            }

            relogio.Stop();

            var motivo = tdr
                ? MotivoParada.Erro
                : parouPorTemperatura
                    ? MotivoParada.Temperatura
                    : parouPorUsuario
                        ? MotivoParada.CanceladoPeloUsuario
                        : !temSensor && solicitada > TetoSemSensor
                            ? MotivoParada.SemSensorTemperaturaLimite2Min
                            : MotivoParada.ConcluidoPorTempo;

            var texto = motivo switch
            {
                MotivoParada.Erro =>
                    "O driver de vídeo reiniciou durante o teste (TDR). Indica instabilidade na GPU ou no driver.",
                MotivoParada.Temperatura => $"Parado ao atingir {limite:0} °C (limite configurado).",
                MotivoParada.CanceladoPeloUsuario => "Parado a pedido do usuário.",
                MotivoParada.SemSensorTemperaturaLimite2Min =>
                    "Parado no teto de 2 minutos: não há sensor de temperatura da GPU para proteger o equipamento.",
                _ => $"Concluído: a duração solicitada ({FormatacaoBr.Duracao(relogio.Elapsed)}) terminou.",
            };

            var estresse = new ResultadoEstresse
            {
                IniciadoEm = iniciado,
                DuracaoReal = relogio.Elapsed,
                DuracaoSolicitada = duracao,
                Perfil = configuracao.Perfil,
                MotivoParada = motivo,
                MotivoParadaTexto = texto,
                Workers = 1,
                CiclosTotais = frames,
                ErrosDeCalculo = (int)Math.Min(int.MaxValue, pixelsDivergentes),
                TemperaturaMaximaCelsius = amostrasTemperatura > 0
                    ? Leitura<double>.Ok(Math.Round(temperaturaMaxima, 1), "sensor de GPU")
                    : Leitura<double>.NaoSuportado(
                        temSensor
                            ? "Nenhuma amostra de temperatura foi coletada."
                            : "Este equipamento não expõe sensor de temperatura da GPU."),
                TemperaturaMediaCelsius = amostrasTemperatura > 0
                    ? Leitura<double>.Ok(Math.Round(somaTemperatura / amostrasTemperatura, 1), "sensor de GPU")
                    : Leitura<double>.NaoSuportado("Sem amostras de temperatura da GPU."),
                ClockSustentadoMhz = Leitura<double>.NaoSuportado("O clock da GPU não é lido por esta versão."),
                ThrottlingDetectado = Leitura<bool>.NaoSuportado("O throttling da GPU não é avaliado por esta versão."),
                LimiteTemperaturaCelsius = limite,
                AvisoSemSensor = temSensor
                    ? null
                    : "Nenhum sensor de temperatura da GPU respondeu: teste limitado a 2 minutos.",
            };

            ResultadoBenchmarkGpu? benchmark = null;
            if (amostrasFps.Count > 0 && !tdr)
            {
                benchmark = new ResultadoBenchmarkGpu
                {
                    ExecutadoEm = executadoEm,
                    VersaoAlgoritmo = 1,
                    Pontuacao = Math.Round(Mediana(amostrasFps) / 10.0, 1),
                    FpsMedia = Math.Round(amostrasFps.Average(), 1),
                    Amostras = amostrasFps.Select(v => Math.Round(v, 1)).ToList(),
                    PixelsDivergentes = pixelsDivergentes,
                    TextoAviso = "Pontuação própria do Flavos Diag (versão 1). Não é comparável com outros programas.",
                };
            }

            return new ResultadoGpu
            {
                ExecutadoEm = executadoEm,
                Adaptador = sessao.NomeAdaptador,
                Estresse = estresse,
                Benchmark = benchmark,
            };
        }
        finally
        {
            if (sensor is not null)
            {
                try
                {
                    sensor.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // falha ao descarregar o driver de sensores não invalida o resultado do teste
                }
            }
        }
    }

    /// <summary>Resultado explícito de "não foi possível testar": a categoria fica Inconclusiva com a causa.</summary>
    internal static ResultadoGpu SemTeste(DateTimeOffset executadoEm, string causa, string adaptador = "não determinado") => new()
    {
        ExecutadoEm = executadoEm,
        Adaptador = adaptador,
        Estresse = null,
        Benchmark = null,
        CausaAusencia = causa,
    };

    internal static double Mediana(IReadOnlyList<double> valores)
    {
        if (valores.Count == 0)
        {
            return 0;
        }

        var ordenados = valores.OrderBy(v => v).ToList();
        var meio = ordenados.Count / 2;
        return ordenados.Count % 2 == 1 ? ordenados[meio] : (ordenados[meio - 1] + ordenados[meio]) / 2.0;
    }

    private ISensorProvider? AbrirSensores()
    {
        try
        {
            var sessao = _sensores.Criar();
            var abertura = sessao.AbrirAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (abertura.Disponivel)
            {
                return sessao;
            }

            sessao.DisposeAsync().AsTask().GetAwaiter().GetResult();
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    /// Temperatura da GPU: a maior leitura de categoria Temperatura cujo rótulo cita GPU/vídeo.
    /// Devolve <c>null</c> quando não há leitura — nunca zero, que seria um valor inventado.
    /// </summary>
    public static double? LerTemperaturaGpu(ISensorProvider? sensor)
    {
        if (sensor is null)
        {
            return null;
        }

        try
        {
            var amostra = sensor.Ler();
            var valores = new List<double>();
            foreach (var valor in amostra.Valores)
            {
                if (valor.Categoria != CategoriaSensor.Temperatura || valor.Valor <= 0 || valor.Valor >= 150)
                {
                    continue;
                }

                if (valor.Rotulo.Contains("GPU", StringComparison.OrdinalIgnoreCase)
                    || valor.Rotulo.Contains("video", StringComparison.OrdinalIgnoreCase)
                    || valor.Rotulo.Contains("vídeo", StringComparison.OrdinalIgnoreCase))
                {
                    valores.Add(valor.Valor);
                }
            }

            return valores.Count == 0 ? null : valores.Max();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
