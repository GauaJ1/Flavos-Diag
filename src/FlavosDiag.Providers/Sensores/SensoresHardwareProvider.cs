using System.Security.Principal;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;
using LibreHardwareMonitor.Hardware;

namespace FlavosDiag.Providers.Sensores;

/// <summary>
/// Sensores reais via biblioteca de monitoramento de hardware de código aberto (atrás de
/// <see cref="ISensorProvider"/>, conforme a especificação).
/// A biblioteca carrega um driver de kernel para ler sensores do chip; quando isso não é permitido
/// (sem administrador, driver bloqueado, máquina virtual), a abertura falha e a causa é registrada —
/// o aplicativo continua funcionando sem sensores, mostrando "sem leitura", nunca zero.
/// Somente leitura: nenhum sensor é alterado, nenhuma ventoinha é controlada.
/// </summary>
public sealed class SensoresHardwareProvider : ISensorProvider
{
    private readonly object _trava = new();
    private Computer? _computador;
    private List<ValorSensor> _disponiveis = new();
    private bool _aberto;
    private string? _causaFalha;

    // Último catálogo de sensores de temperatura sem leitura, para não reenumerar a cada segundo.
    private List<SensorSemLeitura> _semLeitura = new();

    public string Nome => "LibreHardwareMonitor (sensores de placa)";

    public IReadOnlyList<ValorSensor> SensoresDisponiveis
    {
        get
        {
            lock (_trava)
            {
                return _disponiveis.ToList();
            }
        }
    }

    /// <summary>Sensores de temperatura reconhecidos pelo coletor mas sem valor utilizável.</summary>
    public IReadOnlyList<SensorSemLeitura> SensoresSemLeitura
    {
        get
        {
            lock (_trava)
            {
                return _semLeitura.ToList();
            }
        }
    }

    /// <summary>Indica se o processo está elevado (a leitura de sensores depende disso na maioria dos PCs).</summary>
    public static bool ProcessoElevado()
    {
        try
        {
            using var identidade = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identidade).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public Task<Leitura<bool>> AbrirAsync(CancellationToken cancelamento = default)
    {
        return Task.Run<Leitura<bool>>(() =>
        {
            lock (_trava)
            {
                if (_aberto)
                {
                    return Leitura<bool>.Ok(true, "sessão já aberta");
                }

                try
                {
                    cancelamento.ThrowIfCancellationRequested();
                    _computador = new Computer
                    {
                        IsCpuEnabled = true,
                        IsMotherboardEnabled = true,
                        IsMemoryEnabled = true,
                        IsStorageEnabled = true,
                        IsGpuEnabled = true, // temperatura da GPU para o gráfico de stress
                        IsControllerEnabled = false,
                        IsNetworkEnabled = false,
                        IsPsuEnabled = false,
                        IsBatteryEnabled = false,
                    };

                    _computador.Open();

                    // Enumeramos os sensores uma vez; a lista serve de catálogo para a interface.
                    var leitura = LerTudo();
                    _disponiveis = leitura.Valores;
                    _semLeitura = leitura.SemLeitura;
                    _aberto = true;
                    _causaFalha = null;

                    // "Nenhum sensor" não é o mesmo que "sensores, mas sem temperatura utilizável".
                    // Uma máquina que expõe carga e clock mas não temperatura ainda é útil.
                    if (_disponiveis.Count == 0)
                    {
                        _causaFalha = DiagnosticoSensores.TextoDeCausa(
                            DiagnosticoSensores.Avaliar(
                                ProcessoElevado(),
                                ConsultarPawnIo(),
                                sensoresTermicos: _semLeitura.Count,
                                sensoresTermicosComLeitura: 0,
                                mensagemAbertura: "A biblioteca não expôs nenhum sensor legível."));
                        return Leitura<bool>.NaoSuportado(_causaFalha);
                    }

                    return Leitura<bool>.Ok(true, _disponiveis.Count + " sensor(es) disponíveis");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    FecharInterno();
                    _causaFalha = DiagnosticoSensores.TextoDeCausa(
                        DiagnosticoSensores.Avaliar(
                            ProcessoElevado(),
                            ConsultarPawnIo(),
                            sensoresTermicos: 0,
                            sensoresTermicosComLeitura: 0,
                            mensagemAbertura: "Não foi possível abrir os sensores: " + ex.Message));
                    return Leitura<bool>.NaoSuportado(_causaFalha);
                }
            }
        }, cancelamento);
    }

    /// <summary>Consulta o PawnIO pela API pública da biblioteca; devolve null se não for possível saber.</summary>
    public static bool? ConsultarPawnIo()
    {
        try
        {
            return LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A API pode mudar entre versões da biblioteca; nesse caso a causa fica "não determinada".
            return null;
        }
    }

    public AmostraSensores Ler()
    {
        lock (_trava)
        {
            if (!_aberto || _computador is null)
            {
                return new AmostraSensores
                {
                    Quando = DateTimeOffset.Now,
                    Valores = Array.Empty<ValorSensor>(),
                    CausaAusencia = _causaFalha ?? "A sessão de sensores não foi aberta.",
                };
            }

            try
            {
                var leitura = LerTudo();
                _semLeitura = leitura.SemLeitura;

                return new AmostraSensores
                {
                    Quando = DateTimeOffset.Now,
                    Valores = leitura.Valores,
                    SemLeitura = leitura.SemLeitura,
                    CausaAusencia = leitura.Valores.Count == 0
                        ? "Nenhum sensor devolveu valor nesta leitura."
                        : null,
                };
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return new AmostraSensores
                {
                    Quando = DateTimeOffset.Now,
                    Valores = Array.Empty<ValorSensor>(),
                    CausaAusencia = "Falha ao ler os sensores: " + ex.Message,
                };
            }
        }
    }

    private (List<ValorSensor> Valores, List<SensorSemLeitura> SemLeitura) LerTudo()
    {
        var valores = new List<ValorSensor>();
        var semLeitura = new List<SensorSemLeitura>();

        if (_computador is null)
        {
            return (valores, semLeitura);
        }

        foreach (var hardware in _computador.Hardware)
        {
            LerHardware(hardware, valores, semLeitura);
        }

        return (valores, semLeitura);
    }

    /// <summary>
    /// Lê um componente e seus subcomponentes (SuperIO fica em SubHardware da placa).
    /// </summary>
    private static void LerHardware(
        IHardware hardware,
        List<ValorSensor> valores,
        List<SensorSemLeitura> semLeitura)
    {
        try
        {
            hardware.Update();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Um controlador problemático não pode derrubar a leitura dos demais.
            return;
        }

        foreach (var sub in hardware.SubHardware)
        {
            LerHardware(sub, valores, semLeitura);
        }

        var tipoHardware = TipoDe(hardware.HardwareType);

        foreach (var sensor in hardware.Sensors)
        {
            var tipo = sensor.SensorType;
            var categoria = CategoriaDe(tipo);
            if (categoria is null)
            {
                continue;
            }

            var nomeSensor = sensor.Name;
            var id = hardware.Identifier + "/" + sensor.Identifier;
            var valor = sensor.Value;

            // A sentinela é tratada aqui, na origem, e não só na hora de exibir: assim o catálogo,
            // o laudo e os vereditos veem "sem leitura" e não um 0 °C que parece medição.
            if (tipo == SensorType.Temperature)
            {
                if (TemperaturaFisica.EhSentinela(valor, sensor.Min, sensor.Max) || !TemperaturaFisica.Valida(valor))
                {
                    semLeitura.Add(new SensorSemLeitura
                    {
                        Id = id,
                        Categoria = CategoriaSensor.Temperatura,
                        TipoHardware = tipoHardware,
                        NomeHardware = hardware.Name,
                        NomeSensor = nomeSensor,
                        Unidade = "°C",
                        ValorBruto = valor,
                        Minimo = sensor.Min,
                        Maximo = sensor.Max,
                        Motivo = TemperaturaFisica.MotivoSemLeitura(valor, sensor.Min, sensor.Max),
                    });
                    continue;
                }
            }
            else if (valor is null || double.IsNaN(valor.Value) || double.IsInfinity(valor.Value))
            {
                // Sem valor não é zero: o sensor é omitido e aparece como "sem leitura".
                continue;
            }

            valores.Add(new ValorSensor
            {
                Id = id,
                Categoria = categoria.Value,
                Rotulo = hardware.Name + " — " + nomeSensor,
                Valor = Math.Round(valor!.Value, 2),
                Unidade = UnidadeDe(tipo),
                TipoHardware = tipoHardware,
                NomeHardware = hardware.Name,
                NomeSensor = nomeSensor,
                Qualidade = QualidadeSensor.Ok,
            });
        }
    }

    private static TipoHardware TipoDe(HardwareType tipo) => tipo switch
    {
        HardwareType.Cpu => TipoHardware.Cpu,
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => TipoHardware.Gpu,
        HardwareType.Motherboard => TipoHardware.Placa,
        HardwareType.SuperIO or HardwareType.EmbeddedController => TipoHardware.SuperIo,
        HardwareType.Memory => TipoHardware.Memoria,
        HardwareType.Storage => TipoHardware.Armazenamento,
        _ => TipoHardware.Outro,
    };

    private static CategoriaSensor? CategoriaDe(SensorType tipo) => tipo switch
    {
        SensorType.Temperature => CategoriaSensor.Temperatura,
        SensorType.Clock => CategoriaSensor.Clock,
        SensorType.Load => CategoriaSensor.Carga,
        SensorType.Power => CategoriaSensor.Potencia,
        SensorType.Voltage => CategoriaSensor.Tensao,
        SensorType.Fan => CategoriaSensor.Ventoinha,
        _ => null,
    };

    private static string UnidadeDe(SensorType tipo) => tipo switch
    {
        SensorType.Temperature => "°C",
        SensorType.Clock => "MHz",
        SensorType.Load => "%",
        SensorType.Power => "W",
        SensorType.Voltage => "V",
        SensorType.Fan => "RPM",
        _ => string.Empty,
    };

    private void FecharInterno()
    {
        try
        {
            _computador?.Close();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Falha ao descarregar o driver não deve impedir o encerramento do app.
        }
        finally
        {
            _computador = null;
            _aberto = false;
            _disponiveis = new List<ValorSensor>();
            _semLeitura = new List<SensorSemLeitura>();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_trava)
        {
            FecharInterno();
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Fábrica das sessões de sensores reais (permite fechar e reabrir o driver).</summary>
public sealed class SensoresHardwareFactory : ISensorProviderFactory
{
    public ISensorProvider Criar() => new SensoresHardwareProvider();
}
