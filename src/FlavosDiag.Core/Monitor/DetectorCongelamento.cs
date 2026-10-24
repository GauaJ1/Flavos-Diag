using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Core.Monitor;

/// <summary>
/// Detecta sensor de temperatura que parou de acompanhar a realidade: valor rigorosamente idêntico
/// por muito tempo, sob carga alta, em componente que deveria esquentar.
/// </summary>
/// <remarks>
/// A versão anterior tentava fazer isso de forma instantânea, olhando <c>Min == Max</c>, e estava
/// errada: nas primeiras leituras <c>Min</c> e <c>Max</c> são sempre iguais, e qualquer sensor
/// estável tem os dois iguais por natureza. Estabilidade só é suspeita quando é <b>implausível</b>:
/// <list type="bullet">
///   <item>o componente é a CPU — GPU e disco podem ficar parados sem que isso seja estranho;</item>
///   <item>a carga da CPU está acima de <see cref="CargaMinimaPercentual"/>, então a temperatura
///   deveria estar variando;</item>
///   <item>o valor não muda há <see cref="SegundosParaSuspeitar"/> segundos.</item>
/// </list>
/// Em repouso, estabilidade é normal e não é suspeita.
/// <para>
/// <b>Suspeito não é sem leitura.</b> O valor continua aparecendo no gráfico e no laudo; o que ele
/// perde é a condição de "confiança alta", então não pode ser o único responsável por uma parada
/// automática nem por um veredito OK.
/// </para>
/// </remarks>
public sealed class DetectorCongelamento
{
    public const double CargaMinimaPercentual = 50;

    public const double SegundosParaSuspeitar = 120;

    private readonly Dictionary<string, (double Valor, DateTimeOffset Desde)> _repeticoes = new(StringComparer.Ordinal);

    /// <summary>
    /// Atualiza o estado com uma amostra e devolve os identificadores que devem ser marcados como
    /// <see cref="QualidadeSensor.Suspeito"/>. A decisão depende só do que foi observado nesta
    /// sessão; um sensor que varia uma vez volta a ser confiável.
    /// </summary>
    public IReadOnlySet<string> Avaliar(AmostraSensores amostra)
    {
        ArgumentNullException.ThrowIfNull(amostra);

        var carga = CargaDaCpu(amostra);
        var suspeitos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sensor in amostra.Valores)
        {
            if (sensor.Categoria != CategoriaSensor.Temperatura ||
                sensor.TipoHardware != TipoHardware.Cpu ||
                sensor.Qualidade != QualidadeSensor.Ok)
            {
                continue;
            }

            // Sem carga alta não há motivo para esperar variação: não é suspeita.
            if (carga is not { } c || c < CargaMinimaPercentual)
            {
                _repeticoes.Remove(sensor.Id);
                continue;
            }

            if (!_repeticoes.TryGetValue(sensor.Id, out var estado) || estado.Valor != sensor.Valor)
            {
                _repeticoes[sensor.Id] = (sensor.Valor, amostra.Quando);
                continue;
            }

            if ((amostra.Quando - estado.Desde) >= TimeSpan.FromSeconds(SegundosParaSuspeitar))
            {
                suspeitos.Add(sensor.Id);
            }
        }

        return suspeitos;
    }

    /// <summary>Zera o histórico (novo teste, nova sessão).</summary>
    public void Reiniciar() => _repeticoes.Clear();

    private static double? CargaDaCpu(AmostraSensores amostra)
    {
        foreach (var sensor in amostra.Valores)
        {
            if (sensor.Categoria == CategoriaSensor.Carga &&
                sensor.TipoHardware == TipoHardware.Cpu &&
                !double.IsNaN(sensor.Valor) &&
                !double.IsInfinity(sensor.Valor))
            {
                return sensor.Valor;
            }
        }

        return null;
    }
}
