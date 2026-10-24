using System.Security.Principal;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
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
                        IsGpuEnabled = false, // a GPU é tratada pela prova de vídeo da Fase 4
                        IsControllerEnabled = false,
                        IsNetworkEnabled = false,
                        IsPsuEnabled = false,
                        IsBatteryEnabled = false,
                    };

                    _computador.Open();

                    // Enumeramos os sensores uma vez; a lista serve de catálogo para a interface.
                    _disponiveis = LerTodos().ToList();
                    _aberto = true;
                    _causaFalha = null;

                    if (_disponiveis.Count == 0)
                    {
                        var causa = ProcessoElevado()
                            ? "A biblioteca abriu, mas este equipamento não expõe sensores legíveis."
                            : "Sem elevação: este equipamento só expõe sensores para um processo administrador. " +
                              "Execute o aplicativo como administrador para ver temperatura e clock.";
                        _causaFalha = causa;
                        return Leitura<bool>.NaoSuportado(causa);
                    }

                    return Leitura<bool>.Ok(true, _disponiveis.Count + " sensor(es) disponíveis");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    FecharInterno();
                    var causa = ProcessoElevado()
                        ? "Não foi possível abrir os sensores: " + ex.Message
                        : "Sem elevação: o driver de leitura de sensores não pôde ser carregado (" + ex.Message + "). " +
                          "Execute o aplicativo como administrador.";
                    _causaFalha = causa;
                    return Leitura<bool>.NaoSuportado(causa);
                }
            }
        }, cancelamento);
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
                var valores = LerTodos().ToList();
                return new AmostraSensores
                {
                    Quando = DateTimeOffset.Now,
                    Valores = valores,
                    CausaAusencia = valores.Count == 0
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

    private IEnumerable<ValorSensor> LerTodos()
    {
        if (_computador is null)
        {
            yield break;
        }

        foreach (var hardware in _computador.Hardware)
        {
            foreach (var sensor in LerHardware(hardware))
            {
                yield return sensor;
            }
        }
    }

    private static IEnumerable<ValorSensor> LerHardware(IHardware hardware)
    {
        try
        {
            hardware.Update();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Um controlador problemático não pode derrubar a leitura dos demais.
            yield break;
        }

        foreach (var sub in hardware.SubHardware)
        {
            foreach (var sensor in LerHardware(sub))
            {
                yield return sensor;
            }
        }

        foreach (var sensor in hardware.Sensors)
        {
            var tipo = sensor.SensorType;

            var categoria = CategoriaDe(tipo);
            if (categoria is null)
            {
                continue;
            }

            var valor = sensor.Value;
            if (valor is null || double.IsNaN(valor.Value) || double.IsInfinity(valor.Value))
            {
                // Sem valor não é zero: o sensor é omitido e aparece como "sem leitura".
                continue;
            }

            yield return new ValorSensor
            {
                Id = hardware.Identifier + "/" + sensor.Identifier,
                Categoria = categoria.Value,
                Rotulo = hardware.Name + " — " + sensor.Name,
                Valor = Math.Round(valor.Value, 2),
                Unidade = UnidadeDe(tipo),
            };
        }
    }

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
