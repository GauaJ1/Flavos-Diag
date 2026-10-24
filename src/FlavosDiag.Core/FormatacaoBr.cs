using System.Globalization;

namespace FlavosDiag.Core;

/// <summary>Formatação pt-BR (interface) e ISO 8601 (dados).</summary>
public static class FormatacaoBr
{
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public const string FormatoData = "dd/MM/yyyy";
    public const string FormatoDataHora = "dd/MM/yyyy HH:mm:ss";
    public const string FormatoIso = "yyyy-MM-ddTHH:mm:sszzz";

    public static string Data(DateTime d) => d.ToString(FormatoData, Cultura);

    public static string DataHora(DateTime d) => d.ToString(FormatoDataHora, Cultura);

    public static string Hora(DateTime d) => d.ToString("HH:mm:ss", Cultura);

    /// <summary>ISO 8601 com offset (JSON e CSV).</summary>
    public static string Iso(DateTimeOffset d) => d.ToString(FormatoIso, CultureInfo.InvariantCulture);

    public static string Bytes(long bytes)
    {
        string[] unidades = { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = bytes;
        int i = 0;
        while (Math.Abs(v) >= 1024 && i < unidades.Length - 1)
        {
            v /= 1024;
            i++;
        }

        var casas = i >= 3 ? 1 : 0;
        return v.ToString("N" + casas, Cultura) + " " + unidades[i];
    }

    public static string Gib(double gib) => gib.ToString("N1", Cultura) + " GiB";

    public static string MegaBytesPorSegundo(double mbps) => mbps.ToString("N1", Cultura) + " MB/s";

    public static string VelocidadeLink(long bitsPorSegundo) =>
        bitsPorSegundo >= 1_000_000_000
            ? (bitsPorSegundo / 1_000_000_000.0).ToString("N1", Cultura) + " Gb/s"
            : (bitsPorSegundo / 1_000_000.0).ToString("N0", Cultura) + " Mb/s";

    public static string Percentual(double valor, int casas = 0) =>
        valor.ToString("N" + casas, Cultura) + " %";

    public static string Temperatura(double celsius) => celsius.ToString("N0", Cultura) + " °C";

    public static string Mhz(double mhz) => mhz.ToString("N0", Cultura) + " MHz";

    public static string Watts(double w) => w.ToString("N0", Cultura) + " W";

    public static string Duracao(TimeSpan t)
    {
        if (t.TotalHours >= 1)
        {
            return $"{(int)t.TotalHours} h {t.Minutes:00} min {t.Seconds:00} s";
        }

        if (t.TotalMinutes >= 1)
        {
            return $"{t.Minutes} min {t.Seconds:00} s";
        }

        return $"{t.TotalSeconds.ToString("N1", Cultura)} s";
    }

    /// <summary>"Não disponível" com motivo entre parênteses, para a interface.</summary>
    public static string NaoDisponivel(string? motivo = null) =>
        string.IsNullOrWhiteSpace(motivo) ? "Não disponível" : $"Não disponível — {motivo}";
}
