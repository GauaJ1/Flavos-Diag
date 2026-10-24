using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;

namespace FlavosDiag.App.ViewModels;

/// <summary>
/// Stress controlável com monitor de temperatura ao vivo.
/// </summary>
/// <remarks>
/// <para>
/// O técnico escolhe quando parar: não há limite que encerre o teste sozinho. O limiar configurado
/// apenas <b>destaca</b> a temperatura no gráfico e na leitura atual. Encerrar automaticamente
/// seria pior do que parece: um sensor que devolve 255 °C (sentinela) abortaria um teste saudável.
/// </para>
/// <para>
/// A amostragem corre num <see cref="DispatcherTimer"/> de 1 s. Um segundo é o intervalo em que uma
/// curva de temperatura ainda mostra a subida da carga sem transformar o gráfico num borrão.
/// </para>
/// </remarks>
public sealed partial class StressViewModel : PaginaBase
{
    private readonly HistoricoAmostras _historico = new(capacidade: 7200);
    private DispatcherTimer? _relogio;
    private DateTimeOffset _inicio;
    private CancellationTokenSource? _cancelamento;

    /// <summary>
    /// Sessão de sensores aberta só enquanto o teste roda. O driver é exclusivo e precisa ser
    /// fechado, senão a tela de diagnóstico não consegue abrir o seu depois.
    /// </summary>
    private Core.Abstractions.ISensorProvider? _sensores;

    public StressViewModel(MainViewModel principal)
        : base(principal, "Stress", "IconeCpu")
    {
        JanelaGraficoSegundos = Math.Clamp(principal.Configuracao.JanelaGraficoSegundos, 20, 600);
        LimiarAlerta = principal.Configuracao.LimiarAlertaTemperaturaCelsius;
    }

    /// <summary>Pontos exibidos no gráfico; lista nova a cada amostra para o gráfico redesenhar.</summary>
    [ObservableProperty]
    private IReadOnlyList<AmostraTemporal> _pontos = Array.Empty<AmostraTemporal>();

    [ObservableProperty]
    private bool _executando;

    [ObservableProperty]
    private double _limiarAlerta = 95;

    [ObservableProperty]
    private int _janelaGraficoSegundos = 60;

    [ObservableProperty]
    private string _temperaturaCpuTexto = "—";

    [ObservableProperty]
    private string _temperaturaGpuTexto = "—";

    [ObservableProperty]
    private string _cargaCpuTexto = "—";

    [ObservableProperty]
    private string _duracaoTexto = "00:00";

    [ObservableProperty]
    private string? _avisoTemperatura;

    /// <summary>Motivo pelo qual não há curva. Sem isso, o gráfico vazio pareceria travado.</summary>
    [ObservableProperty]
    private string? _causaSemLeitura;

    [ObservableProperty]
    private string? _resumoExecucao;

    [ObservableProperty]
    private bool _alertaAtivo;

    /// <summary>
    /// Descrição textual do gráfico para leitores de tela. Um gráfico desenhado à mão não é lido
    /// por tecnologia assistiva, então o resumo precisa existir como texto.
    /// </summary>
    [ObservableProperty]
    private string _resumoGrafico = "Gráfico de temperatura: nenhuma amostra ainda.";

    /// <summary>Quantidade de threads usada; mostrada ao técnico para ele saber a carga aplicada.</summary>
    public int Threads => Math.Max(1, Environment.ProcessorCount - 1);

    /// <summary>
    /// Inicia o stress e passa a amostrar a temperatura. Só termina quando o técnico clicar em
    /// Parar, quando a janela fechar, ou quando a carga concluir por conta própria.
    /// </summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task IniciarAsync(CancellationToken cancelamento)
    {
        if (Executando)
        {
            return;
        }

        Executando = true;
        ResumoExecucao = null;
        AvisoTemperatura = null;
        CausaSemLeitura = null;
        AlertaAtivo = false;
        _historico.Limpar();
        Pontos = Array.Empty<AmostraTemporal>();
        _inicio = DateTimeOffset.Now;
        _cancelamento?.Dispose();
        _cancelamento = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        var token = _cancelamento.Token;

        _sensores = Principal.Servicos.Sensores.Criar();
        var abriu = await _sensores.AbrirAsync(token);
        if (!abriu.Disponivel)
        {
            CausaSemLeitura = abriu.Causa ?? "não foi possível abrir os sensores de temperatura";
        }

        IniciarRelogio();

        try
        {
            var configuracao = new ConfiguracaoEstresse
            {
                Perfil = PerfilCarga.Pesado,

                // Teto alto de propósito: quem encerra é o técnico, não o relógio.
                DuracaoMaxima = TimeSpan.FromHours(4),

                // Sem limite automático de temperatura — o gráfico avisa, o técnico decide.
                LimiteTemperaturaCelsius = double.MaxValue,
            };

            var resultado = await Principal.Servicos.CargaCpu.EstressarAsync(
                configuracao,
                Threads,
                Threads,
                LerAmostraBruta,
                progresso: null,
                token);

            ResumoExecucao = resultado is null
                ? "O stress terminou sem produzir resultado."
                : $"Stress encerrado após {FormatacaoBr.Duracao(resultado.DuracaoReal)} com " +
                  $"{resultado.CiclosTotais:N0} ciclos e {resultado.ErrosDeCalculo} erro(s) de cálculo.";
        }
        catch (OperationCanceledException)
        {
            ResumoExecucao = "Teste interrompido por você. Nenhum veredito foi emitido.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Erro = "Não foi possível executar o stress. Detalhe: " + ex.Message;
        }
        finally
        {
            PararRelogio();
            Executando = false;
            if (_sensores is not null)
            {
                await _sensores.DisposeAsync();
                _sensores = null;
            }
        }
    }

    /// <summary>Parada explícita pelo técnico.</summary>
    [RelayCommand]
    private void Parar()
    {
        if (!Executando)
        {
            return;
        }

        _cancelamento?.Cancel();
    }

    /// <summary>Encerra o que estiver rodando. Chamado ao fechar a janela.</summary>
    public void Encerrar()
    {
        PararRelogio();
        _cancelamento?.Cancel();
        _sensores?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _sensores = null;
    }

    private AmostraSensores? LerAmostraBruta()
    {
        try
        {
            return _sensores?.Ler();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private void IniciarRelogio()
    {
        PararRelogio();
        _relogio = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
        _relogio.Tick += (_, _) => Amostrar();
        _relogio.Start();
        Amostrar();
    }

    private void PararRelogio()
    {
        if (_relogio is null)
        {
            return;
        }

        _relogio.Stop();
        _relogio = null;
    }

    /// <summary>
    /// Uma amostra. A ausência de leitura é registrada com causa; nunca vira 0 no gráfico.
    /// </summary>
    private void Amostrar()
    {
        var agora = DateTimeOffset.Now;
        var segundos = Math.Max(0, (agora - _inicio).TotalSeconds);
        DuracaoTexto = FormatacaoBr.Duracao(TimeSpan.FromSeconds(segundos));

        double? tempCpu = null;
        double? tempGpu = null;
        string? causaCpu = null;
        string? causaGpu = null;
        double? carga = null;

        try
        {
            var amostra = _sensores?.Ler();
            if (amostra is null)
            {
                causaCpu = amostra?.CausaAusencia ?? "o coletor de sensores não devolveu amostra";
            }
            else
            {
                var temperaturas = amostra.Valores
                    .Where(v => v.Categoria == CategoriaSensor.Temperatura)
                    .Select(v => (v.Rotulo, v.Valor))
                    .ToList();

                tempCpu = ClassificadorTemperatura.DaCpu(temperaturas, out causaCpu);
                tempGpu = ClassificadorTemperatura.DaGpu(temperaturas, out causaGpu);

                // FirstOrDefault devolve a referência padrão (null) do record quando não há carga
                // "Total"; ler .Valor direto estouraria. Por isso a carga é tratada como opcional.
                var sensorCarga = amostra.Valores.FirstOrDefault(
                    v => v.Categoria == CategoriaSensor.Carga &&
                         v.Rotulo.Contains("Total", StringComparison.OrdinalIgnoreCase));
                carga = sensorCarga?.Valor;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            causaCpu = "falha ao ler os sensores: " + ex.Message;
        }

        _historico.Adicionar(new AmostraTemporal
        {
            Quando = agora,
            Segundos = segundos,
            TemperaturaCpuCelsius = tempCpu,
            TemperaturaGpuCelsius = tempGpu,
            CargaCpuPercentual = carga,
            CausaCpu = causaCpu,
            CausaGpu = causaGpu,
        });

        Pontos = _historico.Janela(JanelaGraficoSegundos);

        var br = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        TemperaturaCpuTexto = tempCpu is null ? "sem leitura" : tempCpu.Value.ToString("0.0", br) + " °C";
        TemperaturaGpuTexto = tempGpu is null ? "sem leitura" : tempGpu.Value.ToString("0.0", br) + " °C";
        CargaCpuTexto = carga is null ? "sem leitura" : carga.Value.ToString("0", br) + " %";

        var maior = Math.Max(tempCpu ?? 0, tempGpu ?? 0);
        AlertaAtivo = maior > 0 && maior >= LimiarAlerta;
        AvisoTemperatura = AlertaAtivo
            ? $"Temperatura em {maior.ToString("0.0", br)} °C, acima do limiar de alerta " +
              $"({LimiarAlerta.ToString("0", br)} °C). Você decide quando parar."
            : null;

        var br2 = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        ResumoGrafico = (tempCpu, tempGpu) switch
        {
            ({ } c, { } g) => $"Gráfico de temperatura: CPU {c.ToString("0.0", br2)} °C, GPU {g.ToString("0.0", br2)} °C.",
            ({ } c, null) => $"Gráfico de temperatura: CPU {c.ToString("0.0", br2)} °C, sem leitura da GPU.",
            (null, { } g) => $"Gráfico de temperatura: sem leitura da CPU, GPU {g.ToString("0.0", br2)} °C.",
            _ => "Gráfico de temperatura: sem leitura de temperatura.",
        };

        CausaSemLeitura = tempCpu is null && tempGpu is null
            ? causaCpu ?? causaGpu ?? "os sensores não informaram temperatura"
            : null;
    }

    protected override Task CarregarAsync() => Task.CompletedTask;
}
