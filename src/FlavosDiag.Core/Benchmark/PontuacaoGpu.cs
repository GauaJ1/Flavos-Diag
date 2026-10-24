namespace FlavosDiag.Core.Benchmark;

/// <summary>Pontuação de GPU própria do Flavos Diag: frames equivalentes por segundo ponderados pela carga da cena.</summary>
public static class PontuacaoGpu
{
    public const int VersaoAtual = 1;

    public const string Aviso = "Pontuação própria do Flavos Diag (GPU); não comparável com outros programas.";

    /// <param name="pixels">Pixels renderizados.</param>
    /// <param name="iteracoes">Iterações do shader por pixel.</param>
    /// <param name="segundos">Duração da medição.</param>
    public static double Pontos(long pixels, int iteracoes, double segundos)
    {
        if (segundos <= 0)
        {
            return 0;
        }

        // Trabalho normalizado por pixel e iteração: 1 milhão de pixels × 100 iterações = 1 unidade.
        var trabalho = pixels * (iteracoes / 100.0) / 1_000_000.0;
        return trabalho / segundos;
    }
}
