using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Core.Monitor;

/// <summary>
/// Regra de proteção térmica, isolada do WPF para poder ser testada sem interface e sem hardware.
/// </summary>
/// <remarks>
/// <para>
/// A versão anterior deste projeto rodava o stress <b>sem nenhuma proteção</b> quando a CPU não
/// tinha leitura: o limite térmico era <see cref="double.MaxValue"/> e a duração ia até 4 horas.
/// A justificativa era que um sentinela de 255 °C abortaria um teste saudável — mas isso já é
/// resolvido antes, na leitura (<see cref="TemperaturaFisica.Valida"/>). O resultado prático era
/// carga total por horas, sem sensor, num equipamento que podia estar superaquecendo.
/// </para>
/// <para>
/// Decisão revista: o gráfico e o botão Parar continuam sob controle do técnico, mas existem três
/// limites independentes de segurança, e nenhum deles depende de a interface estar respondendo.
/// </para>
/// </remarks>
public sealed class ProtecaoTermica
{
    /// <summary>Quantas leituras consecutivas acima do limite são necessárias para parar.</summary>
    public const int AmostrasConsecutivasParaParar = 3;

    private int _consecutivasAcima;

    public ProtecaoTermica(double limiteParadaCelsius, TimeSpan tetoSemSensor, TimeSpan tetoDuracao)
    {
        LimiteParadaCelsius = limiteParadaCelsius;
        TetoSemSensor = tetoSemSensor;
        TetoDuracao = tetoDuracao;
    }

    public double LimiteParadaCelsius { get; }

    /// <summary>Teto quando não há temperatura válida da CPU.</summary>
    public TimeSpan TetoSemSensor { get; }

    /// <summary>Teto total de duração, com ou sem sensor.</summary>
    public TimeSpan TetoDuracao { get; }

    /// <summary>Motivo registrado quando a proteção decide parar; <c>null</c> enquanto pode continuar.</summary>
    public MotivoParadaProtecao? Avaliar(double? temperaturaCpu, TimeSpan decorrido, bool temSensor)
    {
        if (!temSensor && decorrido >= TetoSemSensor)
        {
            return new MotivoParadaProtecao(
                MotivoParada.SemSensorTemperaturaLimite2Min,
                $"Parado no teto de {FormatacaoBr.Duracao(TetoSemSensor)}: não há leitura de temperatura da " +
                "CPU para proteger o equipamento.");
        }

        if (decorrido >= TetoDuracao)
        {
            return new MotivoParadaProtecao(
                MotivoParada.ConcluidoPorTempo,
                $"Parado ao atingir o tempo máximo configurado ({FormatacaoBr.Duracao(TetoDuracao)}).");
        }

        // Só temperatura de confiança alta (Qualidade = Ok) chega aqui: o classificador já descartou
        // sentinelas e valores fora de faixa. Exigimos amostras consecutivas para não abortar por pico.
        if (temperaturaCpu is { } t)
        {
            if (t >= LimiteParadaCelsius)
            {
                _consecutivasAcima++;
                if (_consecutivasAcima >= AmostrasConsecutivasParaParar)
                {
                    return new MotivoParadaProtecao(
                        MotivoParada.Temperatura,
                        $"Parado automaticamente: {t.ToString("0.0", FormatacaoBr.Cultura)} °C ≥ limite " +
                        $"{LimiteParadaCelsius.ToString("0", FormatacaoBr.Cultura)} °C.");
                }
            }
            else
            {
                // Voltou para baixo do limite: a sequência realmente terminou.
                _consecutivasAcima = 0;
            }

            return null;
        }

        // Sem leitura depois de uma sequência alta: o sensor pode estar oscilando entre "muito quente"
        // e "sem leitura" — a pior hora para zerar o contador. A sequência é mantida, e o teto sem
        // sensor continua valendo em paralelo, então isto não vira teste infinito.
        return null;
    }

    /// <summary>Reinicia o contador de amostras (a cada novo teste ou a cada leitura abaixo do limite).</summary>
    public void Reiniciar() => _consecutivasAcima = 0;
}

/// <summary>Decisão de parada automática com o texto pronto para a tela e para o laudo.</summary>
public sealed record MotivoParadaProtecao(MotivoParada Motivo, string Texto);
