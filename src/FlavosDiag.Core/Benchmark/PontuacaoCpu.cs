namespace FlavosDiag.Core.Benchmark;

/// <summary>
/// Pontuação PRÓPRIA do Flavos Diag. Não comparável com outros programas.
/// </summary>
/// <remarks>
/// <para>
/// Unidade: operações de trabalho concluídas por segundo, divididas por
/// <see cref="OperacoesPorPonto"/> para virar um índice adimensional (<b>1 ponto</b> a cada 1000
/// operações por segundo numa única thread). A fórmula é nossa. Mudanças de fórmula exigem nova
/// <see cref="VersaoAtual"/> — comparações só são válidas entre a mesma versão.
/// </para>
/// <para>
/// Por que versão 2: na versão 1 o modo multi-thread somava ciclos de quatro cargas com pesos de
/// trabalho muito diferentes (matriz 192 elementos, crivo 2.000.000, hash 262.144, ponto flutuante
/// 131.072), enquanto o modo single-thread media apenas a primeira carga. As duas medições não
/// mediam a mesma coisa, então a relação "single × núcleos ≈ multi" nunca fechava. A partir da
/// versão 2 ambas usam a mesma carga e a mesma unidade, e a escala é normalizada para o índice
/// de 1000 pontos.
/// </para>
/// </remarks>
public static class PontuacaoCpu
{
    /// <summary>Versão do algoritmo de pontuação. Muda quando a escala ou a fórmula mudam.</summary>
    public const int VersaoAtual = 2;

    /// <summary>
    /// Operações por segundo equivalentes a <b>1 ponto</b> numa thread. Uma máquina doméstica
    /// conclui alguns milhões de operações por segundo, o que põe o resultado na ordem de milhares
    /// de pontos — faixa em que o número é legível e não parece inventado.
    /// </summary>
    public const double OperacoesPorPonto = 1_000;

    /// <summary>
    /// Dispersão relativa a partir da qual a medição é considerada instável e merece ressalva.
    /// Acima disso, normalmente havia outro programa consumindo a máquina durante o teste.
    /// </summary>
    public const double DispersaoAceitavel = 0.10;

    public const string Aviso = "Pontuação própria do Flavos Diag; não comparável com outros programas.";

    /// <summary>
    /// Converte operações por segundo em pontos. Uma medição inválida (negativa, NaN, infinita)
    /// vira 0 em vez de um número inventado; a tela trata 0 como "sem medição".
    /// </summary>
    public static double Pontos(double operacoesPorSegundo)
    {
        if (double.IsNaN(operacoesPorSegundo) || double.IsInfinity(operacoesPorSegundo) || operacoesPorSegundo <= 0)
        {
            return 0;
        }

        return operacoesPorSegundo / OperacoesPorPonto;
    }

    /// <summary>
    /// Eficiência de escala: quanto do ganho linear a máquina entregou ao passar de 1 para N threads.
    /// 1,0 = escala perfeita; 0,5 = cada núcleo extra rendeu metade do esperado. Devolve <c>null</c>
    /// quando não há medição válida — a tela mostra a causa em vez de um número.
    /// </summary>
    public static double? EficienciaEscala(double pontosSingle, double pontosMulti, int threads)
    {
        if (threads <= 1 || pontosSingle <= 0 || pontosMulti <= 0)
        {
            return null;
        }

        return Math.Round(pontosMulti / (pontosSingle * threads), 3);
    }

    /// <summary>
    /// Mediana, sem depender da ordem de chegada. Uma amostra perdida por interferência do sistema
    /// não puxa o resultado, diferente do que aconteceria com a média.
    /// </summary>
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

    /// <summary>
    /// Dispersão relativa das amostras: (máx − mín) / mediana.
    /// </summary>
    public static double Dispersao(IEnumerable<double> amostras)
    {
        var lista = amostras.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
        if (lista.Count < 2)
        {
            return 0;
        }

        var mediana = Mediana(lista);
        if (double.IsNaN(mediana) || mediana <= 0)
        {
            return 0;
        }

        return (lista.Max() - lista.Min()) / mediana;
    }

    /// <summary>
    /// Ressalva quando a medição saiu instável. <c>null</c> significa que não há o que avisar.
    /// </summary>
    public static string? AvisoDeInstabilidade(double dispersaoSingle, double dispersaoMulti)
    {
        var maior = Math.Max(dispersaoSingle, dispersaoMulti);
        if (maior <= DispersaoAceitavel || double.IsNaN(maior))
        {
            return null;
        }

        var porcento = (maior * 100).ToString("0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        return $"As amostras variaram {porcento}% entre si. Feche outros programas e repita para comparar.";
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
