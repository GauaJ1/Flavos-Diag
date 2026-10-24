using FlavosDiag.Core.Benchmark;

namespace FlavosDiag.Tests;

/// <summary>
/// Coerência da pontuação do benchmark. O defeito que estes testes travam: na versão 1 o modo
/// multi-thread somava ciclos de quatro cargas com pesos diferentes, enquanto o single-thread media
/// só a primeira — os dois números não eram comparáveis entre si.
/// </summary>
public static class PontuacaoTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        yield return ("Pontuação: escala é normalizada em 1000 pontos", EscalaNormalizada);
        yield return ("Pontuação: medição inválida vira 0, nunca NaN", InvalidaViraZero);
        yield return ("Pontuação: escala perfeita devolve eficiência 1", EscalaPerfeita);
        yield return ("Pontuação: metade do ganho devolve eficiência 0,5", MetadeDoGanho);
        yield return ("Pontuação: eficiência é nula sem medição válida", EficienciaSemMedicao);
        yield return ("Pontuação: dispersão acima de 10% gera ressalva", DispersaoGeraAviso);
        yield return ("Pontuação: medição firme não gera ressalva", MedicaoFirmeSemAviso);
        yield return ("Pontuação: a versão do algoritmo subiu para 2", VersaoDois);
    }

    /// <summary>Comparação com tolerância: pontuação é número de ponto flutuante.</summary>
    private static bool Proximo(double esperado, double obtido) => Math.Abs(esperado - obtido) < 0.01;

    private static Task EscalaNormalizada()
    {
        // Contrato da escala: OperacoesPorPonto operações por segundo valem 1 ponto, e a pontuação
        // é proporcional ao trabalho concluído.
        Assert.True(Proximo(1.0, PontuacaoCpu.Pontos(PontuacaoCpu.OperacoesPorPonto)), "a referência vale 1 ponto");
        Assert.True(Proximo(2.0, PontuacaoCpu.Pontos(2 * PontuacaoCpu.OperacoesPorPonto)), "dobro de operações, dobro de pontos");
        Assert.True(Proximo(0.5, PontuacaoCpu.Pontos(PontuacaoCpu.OperacoesPorPonto / 2)), "metade das operações, metade dos pontos");
        Assert.True(Proximo(1000.0, PontuacaoCpu.Pontos(1000 * PontuacaoCpu.OperacoesPorPonto)), "mil referências valem 1000 pontos");
        return Task.CompletedTask;
    }

    private static Task InvalidaViraZero()
    {
        Assert.True(Proximo(0.0, PontuacaoCpu.Pontos(0)), "zero operações são zero pontos");
        Assert.True(Proximo(0.0, PontuacaoCpu.Pontos(-5)), "negativo não pode virar pontuação");
        Assert.True(Proximo(0.0, PontuacaoCpu.Pontos(double.NaN)), "NaN não pode escapar para a tela");
        Assert.True(Proximo(0.0, PontuacaoCpu.Pontos(double.PositiveInfinity)), "infinito não é pontuação");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Com 8 threads e escala perfeita, o multi precisa valer 8× o single. É esta relação que
    /// a versão 1 violava.
    /// </summary>
    private static Task EscalaPerfeita()
    {
        var eficiencia = PontuacaoCpu.EficienciaEscala(pontosSingle: 1000, pontosMulti: 8000, threads: 8);
        Assert.True(eficiencia is not null, "deveria haver eficiência calculável");
        Assert.True(Proximo(1.0, eficiencia!.Value), $"escala perfeita deveria dar 1,0; deu {eficiencia}");
        return Task.CompletedTask;
    }

    private static Task MetadeDoGanho()
    {
        // 8 threads entregando só 4× o single = metade do ganho linear.
        var eficiencia = PontuacaoCpu.EficienciaEscala(pontosSingle: 1000, pontosMulti: 4000, threads: 8);
        Assert.True(eficiencia is not null, "deveria haver eficiência calculável");
        Assert.True(Proximo(0.5, eficiencia!.Value), $"meia escala deveria dar 0,5; deu {eficiencia}");
        return Task.CompletedTask;
    }

    private static Task EficienciaSemMedicao()
    {
        Assert.True(PontuacaoCpu.EficienciaEscala(0, 4000, 8) is null, "sem single não há eficiência");
        Assert.True(PontuacaoCpu.EficienciaEscala(1000, 0, 8) is null, "sem multi não há eficiência");
        Assert.True(PontuacaoCpu.EficienciaEscala(1000, 8000, 1) is null, "uma thread não tem escala");
        return Task.CompletedTask;
    }

    private static Task DispersaoGeraAviso()
    {
        // Amostras muito distantes: (2000 - 800) / 1400 = 0,857.
        var dispersao = PontuacaoCpu.Dispersao(new[] { 800.0, 1400.0, 2000.0 });
        Assert.True(dispersao > PontuacaoCpu.DispersaoAceitavel, $"dispersão deveria passar do aceitável; foi {dispersao}");

        var aviso = PontuacaoCpu.AvisoDeInstabilidade(dispersao, 0);
        Assert.True(aviso is not null, "instabilidade precisa gerar ressalva");
        Assert.True(aviso!.Contains("repita"), "a ressalva precisa dizer o que fazer");
        return Task.CompletedTask;
    }

    private static Task MedicaoFirmeSemAviso()
    {
        var dispersao = PontuacaoCpu.Dispersao(new[] { 1000.0, 1010.0, 1005.0 });
        Assert.True(dispersao <= PontuacaoCpu.DispersaoAceitavel, $"variação pequena não é instabilidade; foi {dispersao}");
        Assert.True(PontuacaoCpu.AvisoDeInstabilidade(dispersao, dispersao) is null, "medição firme não gera ressalva");
        return Task.CompletedTask;
    }

    private static Task VersaoDois()
    {
        Assert.True(PontuacaoCpu.VersaoAtual == 2, "a mudança de escala exige versão nova para não comparar histórico antigo");
        return Task.CompletedTask;
    }
}
