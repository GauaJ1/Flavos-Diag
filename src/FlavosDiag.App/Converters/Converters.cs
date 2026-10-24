using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.App.Converters;

/// <summary>Converte o veredito em pincel semântico (OK/Atenção/Inconclusivo/Falha).</summary>
public sealed class VereditoParaPincel : IValueConverter
{
    public bool Fundo { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var chave = value switch
        {
            Veredito.Ok => "FlavosOk",
            Veredito.Atencao => "FlavosAtencao",
            Veredito.Falha => "FlavosFalha",
            _ => "FlavosInconclusivo",
        };
        chave += Fundo ? "Fundo" : string.Empty;
        if (Application.Current?.TryFindResource(chave) is Brush pincel)
        {
            return pincel;
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Veredito → rótulo em pt-BR.</summary>
public sealed class VereditoParaTexto : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Veredito v ? v.Rotulo() : "Inconclusivo";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool → Visibility (true = Visible). Inverta com ConverterParameter="inverter".</summary>
public sealed class BoolParaVisibilidade : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true;
        if (parameter as string == "inverter")
        {
            b = !b;
        }

        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>null/vazio → Collapsed; caso contrário, Visible.</summary>
public sealed class TextoParaVisibilidade : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Coleção vazia → Visible (mostra o estado vazio) quando ConverterParameter="vazio".</summary>
public sealed class ColecaoVaziaParaVisibilidade : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var vazia = value is System.Collections.ICollection c ? c.Count == 0 : value is null;
        var inverter = parameter as string == "inverter";
        return (vazia ^ inverter) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bytes → texto legível (KB/MB/GB/TB) em pt-BR.</summary>
public sealed class BytesParaTexto : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is long l ? FlavosDiag.Core.FormatacaoBr.Bytes(l)
        : value is int i ? FlavosDiag.Core.FormatacaoBr.Bytes(i)
        : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}