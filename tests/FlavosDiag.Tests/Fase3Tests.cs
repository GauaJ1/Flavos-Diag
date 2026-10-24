using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Fakes;
using FlavosDiag.Workloads;

namespace FlavosDiag.Tests;

/// <summary>
/// Testes da Fase 3. Nenhum teste aqui estressa hardware de verdade: o estresse usa sensores fake e
/// durações de milissegundos, conforme a regra de não rodar estresse real durante o desenvolvimento.
/// </summary>
public static class Fase3Tests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos() => new (string, Func<Task>)[]
    {
        ("Estresse: para no limite de temperatura", ParaNoLimiteDeTemperatura),
        ("Estresse: sem sensor limita a 2 minutos", SemSensorLimitaDoisMinutos),
        ("Estresse: parada pelo usuário é registrada", ParadaPeloUsuarioRegistrada),
        ("Estresse: duração concluída registra motivo", DuracaoConcluida),
        ("Estresse: limites de tempo são saneados", LimitesSaneados),
        ("Estresse: limite padrão é 90 °C", LimitePadraoNoventa),
        ("Estresse: sem sensor não inventa temperatura", SemSensorNaoInventaTemperatura),
        ("Estresse: sem sensor nunca afirma ausência de throttling", SemSensorNaoAfirmaThrottling),
        ("Benchmark: mediana ignora ordem", MedianaIgnoraOrdem),
        ("Benchmark: pontuação é unidades por segundo dividido por mil", PontuacaoNormalizada),
    };

    private static async Task ParaNoLimiteDeTemperatura()
    {
        var sensores = new FakeSensorProvider(FakeSensorProvider.Rampa(80, 10)); // 60, 80, 100...
        await sensores.AbrirAsync();
        var runner = new CargaCpuRunner();

        var resultado = await runner.EstressarAsync(
            new ConfiguracaoEstresse
            {
                Perfil = PerfilCarga.Leve,
                DuracaoMaxima = TimeSpan.FromSeconds(30),
                LimiteTemperaturaCelsius = 90,
            },
            nucleosFisicos: 2,
            threadsLogicas: 2,
            leituraSensores: () => sensores.Ler(),
            progresso: null,
            cancelamento: CancellationToken.None);

        Assert.Equal(MotivoParada.Temperatura, resultado.MotivoParada);
        Assert.Contains("90", resultado.MotivoParadaTexto);
        Assert.True(resultado.DuracaoReal < TimeSpan.FromSeconds(25), "parou bem antes da duração máxima");
        Assert.True(resultado.TemperaturaMaximaCelsius.Disponivel, "houve leitura de temperatura");
        Assert.True(resultado.TemperaturaMaximaCelsius.Valor >= 90, "a máxima atingiu o limite");
    }

    private static async Task SemSensorLimitaDoisMinutos()
    {
        var runner = new CargaCpuRunner();
        var resultado = await runner.EstressarAsync(
            new ConfiguracaoEstresse
            {
                Perfil = PerfilCarga.Leve,
                DuracaoMaxima = TimeSpan.FromSeconds(30),
                LimiteTemperaturaCelsius = 90,
            },
            nucleosFisicos: 1,
            threadsLogicas: 1,
            leituraSensores: () => null,
            progresso: null,
            cancelamento: new CancellationTokenSource(TimeSpan.FromMilliseconds(300)).Token);

        // Sem sensor e pedido acima de 2 min: a duração efetiva vira o teto de 2 minutos.
        Assert.NotNull(resultado.AvisoSemSensor);
        Assert.Contains("2 minutos", resultado.AvisoSemSensor!);
        Assert.False(resultado.TemperaturaMaximaCelsius.Disponivel);
    }

    private static async Task ParadaPeloUsuarioRegistrada()
    {
        var sensores = new FakeSensorProvider(FakeSensorProvider.Rampa(40, 0.1));
        await sensores.AbrirAsync();
        var runner = new CargaCpuRunner();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var resultado = await runner.EstressarAsync(
            new ConfiguracaoEstresse { Perfil = PerfilCarga.Leve, DuracaoMaxima = TimeSpan.FromSeconds(20) },
            nucleosFisicos: 1,
            threadsLogicas: 1,
            leituraSensores: () => sensores.Ler(),
            progresso: null,
            cancelamento: cts.Token);

        Assert.Equal(MotivoParada.CanceladoPeloUsuario, resultado.MotivoParada);
        Assert.True(resultado.DuracaoReal < TimeSpan.FromSeconds(15), "parou rápido");
    }

    private static async Task DuracaoConcluida()
    {
        var sensores = new FakeSensorProvider(FakeSensorProvider.Rampa(40, 0.01));
        await sensores.AbrirAsync();
        var runner = new CargaCpuRunner();

        var resultado = await runner.EstressarAsync(
            new ConfiguracaoEstresse { Perfil = PerfilCarga.Leve, DuracaoMaxima = TimeSpan.FromMilliseconds(400) },
            nucleosFisicos: 1,
            threadsLogicas: 1,
            leituraSensores: () => sensores.Ler(),
            progresso: null,
            cancelamento: CancellationToken.None);

        Assert.Equal(MotivoParada.ConcluidoPorTempo, resultado.MotivoParada);
        Assert.True(resultado.CiclosTotais > 0, "houve trabalho");
        Assert.Equal(0, resultado.ErrosDeCalculo);
    }

    private static async Task LimitesSaneados()
    {
        var runner = new CargaCpuRunner();

        // Duração zero/negativa não pode gerar "infinito": vira 1 minuto.
        var resultado = await runner.EstressarAsync(
            new ConfiguracaoEstresse { Perfil = PerfilCarga.Leve, DuracaoMaxima = TimeSpan.Zero },
            nucleosFisicos: 1,
            threadsLogicas: 1,
            leituraSensores: () => null,
            progresso: null,
            cancelamento: new CancellationTokenSource(TimeSpan.FromMilliseconds(150)).Token);

        Assert.True(resultado.DuracaoSolicitada > TimeSpan.Zero, "duração saneada");
        Assert.True(resultado.Workers >= 1, "ao menos um worker");
    }

    private static async Task LimitePadraoNoventa()
    {
        var runner = new CargaCpuRunner();
        var resultado = await runner.EstressarAsync(
            new ConfiguracaoEstresse { Perfil = PerfilCarga.Leve, DuracaoMaxima = TimeSpan.FromMilliseconds(150) },
            nucleosFisicos: 1,
            threadsLogicas: 1,
            leituraSensores: () => null,
            progresso: null,
            cancelamento: CancellationToken.None);

        // O padrão deixou de ser um literal de 90 °C: subiu para 95 °C na D81 e vem de
        // LimitesTermicos. O teste confere que o runner aplica o limite canônico, não um número próprio.
        Assert.Equal(FlavosDiag.Core.Monitor.LimitesTermicos.VereditoCpuCelsius, resultado.LimiteTemperaturaCelsius);
        Assert.Equal(FlavosDiag.Core.Monitor.LimitesTermicos.VereditoCpuCelsius, CargaCpuRunner.LimiteTemperaturaPadraoCelsius);
        Assert.Equal(95.0, CargaCpuRunner.LimiteTemperaturaPadraoCelsius);
    }

    private static async Task SemSensorNaoInventaTemperatura()
    {
        var runner = new CargaCpuRunner();
        var resultado = await runner.EstressarAsync(
            new ConfiguracaoEstresse { Perfil = PerfilCarga.Leve, DuracaoMaxima = TimeSpan.FromMilliseconds(150) },
            nucleosFisicos: 1,
            threadsLogicas: 1,
            leituraSensores: () => null,
            progresso: null,
            cancelamento: CancellationToken.None);

        Assert.False(resultado.TemperaturaMaximaCelsius.Disponivel, "sem sensor => sem leitura");
        Assert.Contains("temperatura", resultado.TemperaturaMaximaCelsius.Causa!, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(resultado.TemperaturaMaximaCelsius.Causa));
    }

    private static async Task SemSensorNaoAfirmaThrottling()
    {
        var runner = new CargaCpuRunner();
        var resultado = await runner.EstressarAsync(
            new ConfiguracaoEstresse { Perfil = PerfilCarga.Leve, DuracaoMaxima = TimeSpan.FromMilliseconds(150) },
            nucleosFisicos: 1,
            threadsLogicas: 1,
            leituraSensores: () => null,
            progresso: null,
            cancelamento: CancellationToken.None);

        // A leitura de throttling precisa ser "sem leitura", nunca "false = não houve throttling".
        Assert.False(resultado.ThrottlingDetectado.Disponivel);
        Assert.False(string.IsNullOrWhiteSpace(resultado.ThrottlingDetectado.Causa));
    }

    private static Task MedianaIgnoraOrdem()
    {
        Assert.Equal(5.0, CargaCpuRunner.Mediana(new[] { 9.0, 1.0, 5.0, 3.0, 7.0 }));
        Assert.Equal(4.0, CargaCpuRunner.Mediana(new[] { 1.0, 3.0, 5.0, 7.0 }));
        Assert.Equal(0.0, CargaCpuRunner.Mediana(Array.Empty<double>()));
        return Task.CompletedTask;
    }

    private static Task PontuacaoNormalizada()
    {
        Assert.Equal(1.0, CargaCpuRunner.Pontuacao(1000));
        Assert.Equal(2.5, CargaCpuRunner.Pontuacao(2500));
        Assert.Equal(0.0, CargaCpuRunner.Pontuacao(0));
        return Task.CompletedTask;
    }
}
