using FlavosDiag.Core;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.Providers.Nativo;

/// <summary>
/// Regra única de validação de temperatura de disco, usada por TODAS as fontes (SMART ATA, log NVMe e
/// contadores <c>MSFT_StorageReliabilityCounter</c> do Windows).
/// </summary>
/// <remarks>
/// O motivo de existir: as fontes de hardware usam <c>0</c> e <c>255</c> como sentinelas de "não informado"
/// e algumas devolvem lixo fora de qualquer faixa física. Se um 0 virasse leitura real, o laudo mostraria
/// "0 °C" como se o sensor tivesse medido — e um 255 viraria uma temperatura absurda que, dependendo da
/// regra, poderia até gerar aviso falso. Aqui a validação é decidida <b>em um só lugar</b>, para as três
/// fontes nunca divergirem: valor sentinela ou fora de <see cref="MinimoCelsius"/>–<see cref="MaximoCelsius"/>
/// significa <b>ausência com causa</b>, nunca um número inventado.
/// </remarks>
public static class Temperatura
{
    /// <summary>Sentinela "sem leitura" usada pelos sensores ATA e pelos contadores do Windows.</summary>
    public const int SentinelaZero = FaixaTemperatura.SentinelaZero;

    /// <summary>Sentinela <c>0xFF</c> (todos os bits ligados) usada por vários controladores ATA/USB.</summary>
    public const int SentinelaFantasma = FaixaTemperatura.SentinelaFantasma;

    /// <summary>Menor temperatura aceita como leitura real (°C). Zero é sentinela, não leitura.</summary>
    public const int MinimoCelsius = FaixaTemperatura.MinimoCelsius;

    /// <summary>Maior temperatura aceita como leitura real (°C). Acima disso o sensor está inválido.</summary>
    public const int MaximoCelsius = FaixaTemperatura.MaximoCelsius;

    /// <summary>Zero absoluto em Kelvin; abaixo disso o campo NVMe não é uma temperatura aproveitável.</summary>
    public const int ZeroAbsolutoKelvin = 273;

    /// <summary>
    /// Causa em pt-BR devolvida sempre que o sensor não informou uma temperatura aproveitável.
    /// Explica <b>por quê</b> o dado está ausente em vez de deixar o leitor achar que não foi medido.
    /// </summary>
    public const string CausaSentinelaOuForaDaFaixa =
        "o disco não informou uma temperatura válida (valor do sensor inválido: 0, 255 ou fora de 1–120 °C)";

    /// <summary>Indica se <paramref name="celsius"/> é uma leitura real, isto é, <c>0 &lt; valor &lt;= 120</c>.</summary>
    /// <remarks>
    /// A comparação cobre as três exclusões de uma vez: negativos, 0 (sentinela) e 255 (sentinela) já caem
    /// fora da faixa; os sentinelas ficam explícitos nos nomes das constantes para o próximo leitor.
    /// </remarks>
    public static bool TemperaturaValida(int celsius) => FaixaTemperatura.Valida(celsius);

    /// <summary>Indica se o valor bruto é um dos sentinelas conhecidos (<see cref="SentinelaZero"/> ou <see cref="SentinelaFantasma"/>).</summary>
    public static bool EhSentinela(int celsius) => FaixaTemperatura.EhSentinela(celsius);

    /// <summary>
    /// Converte um valor bruto do sensor em <see cref="Leitura{T}"/>: disponível quando é uma temperatura real,
    /// ausente (<c>NaoSuportado</c>) com causa em pt-BR quando é sentinela ou está fora da faixa.
    /// </summary>
    /// <param name="celsius">Valor já convertido para graus Celsius.</param>
    /// <param name="fonte">Origem técnica do dado (ex.: <c>NVMe log 0x02</c>).</param>
    public static Leitura<int> InterpretarTemperatura(int celsius, string? fonte = null) =>
        TemperaturaValida(celsius)
            ? Leitura<int>.Ok(celsius, fonte)
            : Leitura<int>.NaoSuportado(CausaSentinelaOuForaDaFaixa, fonte);

    /// <summary>
    /// Interpreta o campo 1–2 do log NVMe, que vem em Kelvin. Devolve <c>null</c> — nunca 0 — quando o campo
    /// não representa uma temperatura real: o log chega zerado em alguns controladores, e uma leitura de
    /// 273 K ou menos significaria 0 °C ou menos, ou seja, campo não preenchido.
    /// </summary>
    /// <param name="kelvin">Valor bruto de 16 bits (offset 1) do log SMART/Health do NVMe.</param>
    /// <returns>Temperatura em °C quando válida; <c>null</c> quando não há leitura aproveitável.</returns>
    public static int? DeKelvinParaCelsius(int kelvin)
    {
        // O campo NVMe é 16 bits: 0xFFFF é o sentinela de "não informado" da especificação.
        if (kelvin <= ZeroAbsolutoKelvin || kelvin == ushort.MaxValue)
        {
            return null;
        }

        var celsius = kelvin - ZeroAbsolutoKelvin;
        return TemperaturaValida(celsius) ? celsius : null;
    }
}
