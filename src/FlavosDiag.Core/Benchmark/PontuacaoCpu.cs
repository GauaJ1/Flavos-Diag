namespace FlavosDiag.Core.Benchmark;

/// <summary>
/// Pontuação PRÓPRIA do Flavos Diag. Não comparável com outros programas.
/// Unidade: milhares de unidades de trabalho concluídas por segundo em um trabalho fixo
/// definido pela versão do algoritmo. A fórmula é nossa: pontos = trabalho/segundo / 1000.
/// Mudanças de fórmula exigem nova <see cref="VersaoAtual"/> — comparações só são válidas entre a mesma versão.
/// </summary>
public static class PontuacaoCpu
{
    public const int VersaoAtual = 1;

    public const string Aviso = "Pontuação própria do Flavos Diag; não comparável com outros programas.";

    public static double Pontos(double unidadesPorSegundo) => unidadesPorSegundo / 1000.0;

    public static double Mediana(IEnumerable<double> amostras)
    {
        var lista = amostras.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
        if (lista.Count == 0)
        {
            return double.NaN;
        }

        var meio = lista.Count / 2;
        return lista.Count % 2 == 1 ? lista[meio] : (lista[meio - 1] + lista[meio]) / 2.0;
    }

    /// <summary>Comparação antes/depois em % (positivo = melhora).</summary>
    public static double? VariacaoPercentual(double antes, double depois) =>
        antes <= 0 || double.IsNaN(antes) || double.IsNaN(depois) ? null : (depois - antes) / antes * 100.0;

    public static string ClassificarVariacao(double? variacao) => variacao switch
    {
        null => "Sem base de comparação",
        var v when v >= 5 => "Melhora",
        var v when v <= -5 => "Piora",
        _ => "Estável (variação dentro da margem de 5 %)",
    };
}
