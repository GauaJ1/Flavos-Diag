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
    /// O sensor devolveu uma sentinela de "não consegui ler"? Caso clássico verificado nesta
    /// máquina: <c>Core (Tctl/Tdie)</c> com <c>Value = 0</c>, <c>Min = 0</c> e <c>Max = 0</c> —
    /// nunca variou, então não é um zero medido, é ausência de leitura.
    /// </summary>
    public static bool EhSentinela(double? valor, double? minimo, double? maximo)
    {
        if (valor is not { } v || double.IsNaN(v))
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
        if (!Valida(v))
        {
            return true;
        }

        // Sensor que nunca variou: a fonte não está medindo nada.
        if (minimo is { } min && maximo is { } max && min == max)
        {
            return true;
        }

        return false;
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

        if (valor.Value == 0)
        {
            return $"devolveu 0 °C (mín. {textoMin}, máx. {textoMax}); 0 é sentinela de 'sem leitura', não temperatura";
        }

        if (minimo is { } mi && maximo is { } ma && mi == ma)
        {
            return $"o valor não varia (mín. {textoMin}, máx. {textoMax}); a fonte não está medindo";
        }

        if (valor.Value == 255)
        {
            return "devolveu 255 °C, sentinela de 'sem leitura'";
        }

        return $"valor fora da faixa utilizável ({textoValor} °C; esperado entre {MinimoValidoCelsius:0} e {MaximoValidoCelsius:0} °C)";
    }
}
