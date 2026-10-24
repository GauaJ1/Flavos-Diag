using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Core.Monitor;

/// <summary>
/// Regra única de "isto é uma temperatura utilizável?". O coletor e o classificador precisam
/// concordar: se cada um tivesse a sua faixa, o catálogo diria que existe leitura e o gráfico
/// diria que não — o técnico veria duas verdades na mesma tela.
/// </summary>
public static class TemperaturaFisica
{
    public const double MinimoValidoCelsius = 1;

    public const double MaximoValidoCelsius = 120;

    /// <summary>
    /// A leitura é utilizável? 0 e 255 são sentinelas de "sem dado"; negativos (inclusive -128,
    /// usado por alguns chips como leitura inválida) e acima de 120 °C não são temperatura de
    /// componente em operação.
    /// </summary>
    public static bool Valida(double? celsius) =>
        celsius is { } v && !double.IsNaN(v) && !double.IsInfinity(v) &&
        v >= MinimoValidoCelsius && v <= MaximoValidoCelsius;

    /// <summary>
    /// O sensor devolveu uma sentinela de "não consegui ler"? A decisão é pelo <b>valor</b>, nunca
    /// por <c>Min == Max</c>.
    /// </summary>
    /// <remarks>
    /// A versão anterior também tratava <c>Min == Max</c> como ausência de leitura, com o argumento
    /// de que um sensor que "nunca variou" não estaria medindo. Estava errada, e o erro foi medido
    /// nesta máquina: <c>Min</c> e <c>Max</c> são acumulados desde a abertura da sessão, então
    /// <c>Min == Max</c> é verdade em toda primeira leitura, e continua verdade para qualquer sensor
    /// estável e de resolução de 1 °C. O <c>GPU Core</c> desta máquina devolveu
    /// <c>46 (min 46, max 46)</c> em três leituras ao longo de 5 s — uma leitura perfeitamente boa,
    /// que a regra antiga descartava. O caso do dono (CPU <c>0 / Min 0 / Max 0</c>) continua sendo
    /// tratado, porque o <b>valor</b> 0 já é sentinela por si só.
    /// <para>
    /// Estabilidade só é suspeita quando é improvável: sensor de CPU, sob carga, com o valor
    /// idêntico por muito tempo. Isso é <see cref="QualidadeSensor.Suspeito"/>, decidido pelo
    /// coletor ao longo do tempo — não aqui, que avalia uma amostra isolada.
    /// </para>
    /// </remarks>
    public static bool EhSentinela(double? valor, double? minimo = null, double? maximo = null)
    {
        _ = minimo;
        _ = maximo;

        if (valor is not { } v || double.IsNaN(v) || double.IsInfinity(v))
        {
            return true;
        }

        // 0 e 255 são os sentinelas clássicos de leitura inválida. Nunca são temperatura de
        // componente em operação, independentemente do que o sensor informe em Min/Max.
        if (v == 0 || v == 255)
        {
            return true;
        }

        // Qualquer valor fora da faixa utilizável (por exemplo -128, outro código de erro comum)
        // não é temperatura: se não passa em Valida, não é leitura.
        return !Valida(v);
    }

    /// <summary>
    /// Por que este sensor de temperatura não tem leitura, em pt-BR e com os números na mensagem.
    /// </summary>
    public static string MotivoSemLeitura(double? valor, double? minimo, double? maximo)
    {
        if (valor is null || double.IsNaN(valor.Value))
        {
            return "o sensor não devolveu nenhum valor";
        }

        var textoMin = minimo is { } mn ? mn.ToString("0.#", FormatacaoBr.Cultura) : "—";
        var textoMax = maximo is { } mx ? mx.ToString("0.#", FormatacaoBr.Cultura) : "—";
        var textoValor = valor.Value.ToString("0.#", FormatacaoBr.Cultura);

        if (double.IsInfinity(valor.Value))
        {
            return "o sensor devolveu um valor infinito, que não é uma temperatura";
        }

        if (valor.Value == 0)
        {
            return $"devolveu 0 °C (mín. {textoMin}, máx. {textoMax}); 0 é sentinela de 'sem leitura', não temperatura";
        }

        if (valor.Value == 255)
        {
            return "devolveu 255 °C, sentinela de 'sem leitura'";
        }

        if (valor.Value is -127 or -128)
        {
            return $"devolveu {textoValor} °C, código de erro do sensor (sem leitura)";
        }

        return $"valor fora da faixa utilizável ({textoValor} °C; esperado entre {MinimoValidoCelsius:0} e {MaximoValidoCelsius:0} °C)";
    }
}
