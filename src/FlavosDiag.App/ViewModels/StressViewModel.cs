using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.App.Services;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;
using FlavosDiag.Workloads;

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

    /// <summary>Proteção térmica da execução atual.</summary>
    private ProtecaoTermica? _protecao;

    /// <summary>Motivo quando a própria proteção encerra o teste.</summary>
    private string? _paradaAutomatica;

    /// <summary>Confirmação explícita de seguir sem leitura de temperatura.</summary>
    private bool _confirmaSemSensor;

    /// <summary>Relógio da proteção; separado do cronômetro do runner para incluir a abertura.</summary>
    private readonly System.Diagnostics.Stopwatch _relogioProtecao = new();

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

    /// <summary>Aviso sobre o teto de duração quando não há leitura de temperatura.</summary>
    [ObservableProperty]
    private string? _avisoSemSensor;

    /// <summary>Causa da falta de leitura, com orientação do que fazer.</summary>
    [ObservableProperty]
    private string? _diagnosticoSensores;

    /// <summary>Se o comando de instalação do driver deve aparecer na faixa de aviso.</summary>
    [ObservableProperty]
    private bool _sugereInstalarDriver;

    /// <summary>Comando sugerido, apenas para copiar — nunca executado pelo aplicativo.</summary>
    public string ComandoInstalacaoDriver => Core.Monitor.DiagnosticoSensores.ComandoInstalacao;

    /// <summary>Aviso de que faltam menos de 1 minuto para o teto de duração.</summary>
    [ObservableProperty]
    private string? _avisoFinal;

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
            // Sonda a temperatura antes de aplicar carga: decidir o teto com a CPU já sob stress
            // seria tarde para escolher quanta carga expor um equipamento que não responde.
            var temSensor = Amostrar().TemperaturaCpuCelsius is not null;

            _protecao = new ProtecaoTermica(
                Principal.Configuracao.LimiteParadaAutomaticaCelsius,
                CargaCpuRunner.TetoSemSensor,
                TimeSpan.FromMinutes(Math.Clamp(Principal.Configuracao.TetoDuracaoStressMinutos, 1, 120)));
            _protecao.Reiniciar();
            _paradaAutomatica = null;

            if (!temSensor)
            {
                // Sem leitura de temperatura nenhuma, o teto de 2 minutos vale. Seguir além disso
                // só com confirmação explícita, porque carga total sem proteção é risco ao equipamento.
                var estender = Principal.Confirmar(
                    "Carga sem leitura de temperatura",
                    "A temperatura da CPU não tem leitura nesta execução.\n\n" +
                    $"Por segurança, o teste para sozinho em {FormatacaoBr.Duracao(CargaCpuRunner.TetoSemSensor)}.\n\n" +
                    $"Deseja continuar mesmo assim, até o máximo de " +
                    $"{FormatacaoBr.Duracao(CargaCpuRunner.TetoSemSensorConfirmado)}? " +
                    "Nesse caso nenhum limite térmico poderá protegê-lo.");

                if (!estender)
                {
                    AvisoSemSensor = "Teste cancelado: não há leitura de temperatura para proteger o equipamento.";
                    return;
                }

                _confirmaSemSensor = true;
                AvisoSemSensor = "Carga sem leitura de temperatura, confirmada por você. O teste será encerrado em " +
                                 FormatacaoBr.Duracao(CargaCpuRunner.TetoSemSensorConfirmado) + ".";
            }

            var configuracao = new ConfiguracaoEstresse
            {
                Perfil = PerfilCarga.Pesado,

                // Três limites independentes: o teto de duração, o teto sem sensor e a parada térmica.
                DuracaoMaxima = _protecao.TetoDuracao,

                // A parada térmica pela temperatura de confiança alta. Sentinelas (0/255) já foram
                // descartados na leitura, então este limite nunca é atingido por leitura inválida.
                LimiteTemperaturaCelsius = _protecao.LimiteParadaCelsius,

                PermitirSemSensor = _confirmaSemSensor,
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
            // A parada automática também cancela; nesse caso a mensagem é da proteção, não do técnico.
            ResumoExecucao = _paradaAutomatica
                ?? "Teste interrompido por você. Nenhum veredito foi emitido.";
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

    /// <summary>
    /// Fecha e reabre a sessão de sensores. Serve para depois de o técnico instalar o driver ou
    /// reabrir o app elevado — o driver só passa a valer numa sessão nova.
    /// </summary>
    [RelayCommand]
    private async Task ReverificarSensoresAsync()
    {
        if (_sensores is not null)
        {
            await _sensores.DisposeAsync();
            _sensores = null;
        }

        _sensores = Principal.Servicos.Sensores.Criar();
        var abertura = await _sensores.AbrirAsync();
        CausaSemLeitura = abertura.Disponivel ? null : abertura.Causa;
        AtualizarDiagnostico();
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
    /// <remarks>
    /// Cancela antes de descarregar o driver. A liberação é feita sem bloquear a interface: o runner
    /// pode estar dentro de <c>Ler()</c>, segurando o bloqueio do provider. Esperar por ele aqui
    /// travaria o fechamento da janela — e o cancelamento já basta para a carga parar.
    /// </remarks>
    public void Encerrar()
    {
        PararRelogio();
        _cancelamento?.Cancel();

        var sessao = _sensores;
        _sensores = null;
        if (sessao is not null)
        {
            _ = sessao.DisposeAsync().AsTask();
        }
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
    /// Devolve a amostra para que a proteção térmica possa decidir com o mesmo dado da tela.
    /// </summary>
    private AmostraTemporal Amostrar()
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
                causaCpu = "o coletor de sensores não devolveu amostra";
            }
            else
            {
                tempCpu = ClassificadorTemperatura.DaCpu(amostra, out causaCpu);
                tempGpu = ClassificadorTemperatura.DaGpu(amostra, out causaGpu);

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

        var temporal = new AmostraTemporal
        {
            Quando = agora,
            Segundos = segundos,
            TemperaturaCpuCelsius = tempCpu,
            TemperaturaGpuCelsius = tempGpu,
            CargaCpuPercentual = carga,
            CausaCpu = causaCpu,
            CausaGpu = causaGpu,
        };

        _historico.Adicionar(temporal);

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

        AtualizarDiagnostico();
        AplicarProtecao(tempCpu, TimeSpan.FromSeconds(segundos));

        return temporal;
    }

    /// <summary>
    /// Atualiza a causa da falta de leitura com a orientação certa (elevação, driver, biblioteca).
    /// </summary>
    private void AtualizarDiagnostico()
    {
        var ultima = _sensores is null ? null : LerAmostraBruta();
        var servico = new ServicoDiagnosticoSensores(Principal.Servicos);
        var fatos = servico.FatosCompletos(ultima);

        DiagnosticoSensores = Core.Monitor.DiagnosticoSensores.TextoDeCausa(fatos);
        SugereInstalarDriver = Core.Monitor.DiagnosticoSensores.SugereInstalacaoDeDriver(fatos);
    }

    /// <summary>
    /// Aplica os limites de segurança a cada tique. O runner também avalie a temperatura por conta
    /// própria (e o teto sem sensor e o de duração valem lá), então esta camada é a que atualiza a
    /// tela e antecipa a parada. Qualquer falha aqui cancela o teste: um erro na verificação não
    /// pode significar continuar a carga sem ninguém olhando.
    /// </summary>
    private void AplicarProtecao(double? temperaturaCpu, TimeSpan decorrido)
    {
        if (_protecao is null || !Executando || _paradaAutomatica is not null)
        {
            return;
        }

        try
        {
            var temSensor = temperaturaCpu is not null;
            var restante = _protecao.TetoDuracao - decorrido;
            AvisoFinal = temSensor && restante <= TimeSpan.FromMinutes(1) && restante > TimeSpan.Zero
                ? $"Faltam {FormatacaoBr.Duracao(restante)} para o teto de duração do teste."
                : null;

            if (_protecao.Avaliar(temperaturaCpu, decorrido, temSensor) is { } parada)
            {
                _paradaAutomatica = parada.Texto;
                ResumoExecucao = parada.Texto;
                _cancelamento?.Cancel();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _paradaAutomatica =
                "Parado pela proteção: falha ao verificar os limites de segurança (" + ex.Message + ").";
            ResumoExecucao = _paradaAutomatica;
            _cancelamento?.Cancel();
        }
    }

    protected override Task CarregarAsync() => Task.CompletedTask;
}
