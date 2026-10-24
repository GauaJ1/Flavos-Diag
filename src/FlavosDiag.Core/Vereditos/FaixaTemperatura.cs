namespace FlavosDiag.Core.Vereditos;

/// <summary>
/// Faixa em que uma leitura de temperatura faz sentido para hardware de computador.
/// <para>
/// 0 (0x00) e 255 (0xFF) são os sentinelas usados por dispositivos ATA/NVMe para dizer "não tenho
/// sensor" ou "não medi", e nenhum componente de um PC opera fora de 0–120 °C. Um valor fora da faixa
/// é ausência de leitura, não uma medida: tratá-lo como temperatura faria o equipamento ser reprovado
/// por um número que não existe.
/// </para>
/// <para>
/// A regra mora no Core porque tanto o veredito (aqui) quanto os coletores (Providers) precisam dela,
/// e o Core não pode depender dos Providers.
/// </para>
/// </summary>
public static class FaixaTemperatura
{
    /// <summary>Menor temperatura plausível, em °C.</summary>
    public const int MinimoCelsius = 1;

    /// <summary>Maior temperatura plausível, em °C.</summary>
    public const int MaximoCelsius = 120;

    /// <summary>Sentinela de "sem leitura" usado por alguns dispositivos.</summary>
    public const int SentinelaZero = 0;

    /// <summary>Sentinela de "sem leitura" usado por dispositivos ATA (atributo não lido).</summary>
    public const int SentinelaFantasma = 255;

    /// <summary>Indica se o valor é uma temperatura utilizável (1 a 120 °C).</summary>
    public static bool Valida(int celsius) => celsius >= MinimoCelsius && celsius <= MaximoCelsius;

    /// <summary>Indica se o valor é um dos sentinelas conhecidos de "sem leitura".</summary>
    public static bool EhSentinela(int celsius) => celsius is SentinelaZero or SentinelaFantasma;
}
