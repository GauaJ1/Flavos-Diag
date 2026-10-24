using FlavosDiag.Core.Monitor;

namespace FlavosDiag.Tests;

/// <summary>
/// Classificação de temperatura de CPU e GPU e o histórico circular do gráfico.
/// Os nomes de sensor usados aqui foram lidos do coletor real desta máquina.
/// </summary>
public static class MonitorSensoresTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        yield return ("Monitor: sensor da CPU com 0 vira ausência com causa", CpuSentinelZeroViraAusencia);
        yield return ("Monitor: sensor da CPU com 255 vira ausência", CpuSentinel255ViraAusencia);
        yield return ("Monitor: temperatura válida da CPU é lida", CpuValida);
        yield return ("Monitor: forno de 121 °C não é temperatura de componente", ForaDaFaixa);
        yield return ("Monitor: sem sensor da CPU há causa explícita", SemSensorCpu);
        yield return ("Monitor: GPU lê GPU Core e ignora valor inválido", GpuValida);
        yield return ("Monitor: a CPU prefere Tctl/Tdie quando há vários sensores", CpuPrefereTctl);
        yield return ("Monitor: histórico circular descarta o mais antigo quando enche", HistoricoCircular);
        yield return ("Monitor: janela do gráfico devolve só o intervalo pedido", Janela);
        yield return ("Monitor: amostra sem temperatura válida não tem leitura", AmostraSemLeitura);
        yield return ("Monitor: rótulo completo do provider casa com o sensor da GPU", GpuComRotuloDoProvider);
        yield return ("Monitor: rótulo completo do provider casa com o sensor da CPU", CpuComRotuloDoProvider);
    }

    /// <summary>
    /// Monta uma amostra com o tipo de hardware explícito — é assim que o provider real entrega os
    /// dados. Usar a sobrecarga por nome seria testar um caminho que o aplicativo não usa mais.
    /// </summary>
    private static FlavosDiag.Core.Modelos.AmostraSensores AmostraDe(
        FlavosDiag.Core.Modelos.TipoHardware tipo,
        params (string Nome, double Valor)[] sensores) => new()
        {
            Quando = DateTimeOffset.Now,
            Valores = sensores.Select(s => new FlavosDiag.Core.Modelos.ValorSensor
            {
                Id = tipo + "/" + s.Nome,
                Categoria = FlavosDiag.Core.Modelos.CategoriaSensor.Temperatura,
                Rotulo = s.Nome,
                Valor = s.Valor,
                Unidade = "°C",
                TipoHardware = tipo,
                NomeSensor = s.Nome,
                Qualidade = FlavosDiag.Core.Modelos.QualidadeSensor.Ok,
            }).ToList(),
        };

    /// <summary>
    /// O caso desta máquina: o Ryzen expõe "Core (Tctl/Tdie)" mas devolve 0 sem elevação.
    /// Zero é sentinela, não temperatura — se virasse ponto no gráfico, a curva ficaria em 0 °C
    /// e pareceria uma leitura legítima de processador frio.
    /// </summary>
    private static Task CpuSentinelZeroViraAusencia()
    {
        var temperatura = ClassificadorTemperatura.DaCpu(
            AmostraDe(FlavosDiag.Core.Modelos.TipoHardware.Cpu, ("Core (Tctl/Tdie)", 0.0)),
            out var causa);

        Assert.True(temperatura is null, "0 °C na CPU precisa virar ausência de leitura");
        Assert.True(causa is not null && causa.Contains("inválido"), "a causa precisa dizer que o valor era inválido");
        return Task.CompletedTask;
    }

    private static Task CpuSentinel255ViraAusencia()
    {
        var temperatura = ClassificadorTemperatura.DaCpu(AmostraDe(FlavosDiag.Core.Modelos.TipoHardware.Cpu, ("CPU Package", 255.0)), out var causa);
        Assert.True(temperatura is null, "255 °C é sentinela e não pode virar leitura");
        Assert.True(causa is not null, "precisa haver causa");
        return Task.CompletedTask;
    }

    private static Task CpuValida()
    {
        var temperatura = ClassificadorTemperatura.DaCpu(AmostraDe(FlavosDiag.Core.Modelos.TipoHardware.Cpu, ("Core (Tctl/Tdie)", 62.4)), out var causa);
        Assert.True(Equals(62.4, temperatura), "leitura válida precisa passar");
        Assert.True(causa is null, "sem ausência não há causa");
        return Task.CompletedTask;
    }

    private static Task ForaDaFaixa()
    {
        Assert.True(!ClassificadorTemperatura.Valida(121), "121 °C está fora da faixa");
        Assert.True(!ClassificadorTemperatura.Valida(-1), "temperatura negativa não vale");
        Assert.True(ClassificadorTemperatura.Valida(1), "1 °C é o limite inferior válido");
        Assert.True(ClassificadorTemperatura.Valida(120), "120 °C é o limite superior válido");
        Assert.True(!ClassificadorTemperatura.Valida(null), "nulo não é leitura");
        return Task.CompletedTask;
    }

    private static Task SemSensorCpu()
    {
        var temperatura = ClassificadorTemperatura.DaCpu(
            AmostraDe(FlavosDiag.Core.Modelos.TipoHardware.Cpu, ("GPU Core", 45.0)),
            out var causa);

        Assert.True(temperatura is null, "sem sensor da CPU não há leitura");
        Assert.True(causa is not null && causa.Contains("não expôs"), "a causa precisa dizer que não há sensor");
        return Task.CompletedTask;
    }

    private static Task GpuValida()
    {
        var amostra = AmostraDe(FlavosDiag.Core.Modelos.TipoHardware.Gpu, ("GPU Core", 45.0), ("GPU VR SoC", 0.0));
        var temperatura = ClassificadorTemperatura.DaGpu(amostra, out var causa);
        Assert.True(Equals(45.0, temperatura), "GPU Core válido precisa ser escolhido");
        Assert.True(causa is null, "sem ausência não há causa");
        return Task.CompletedTask;
    }

    /// <summary>
    /// O provider monta o rótulo como "&lt;hardware&gt; — &lt;sensor&gt;", por exemplo
    /// "AMD Radeon(TM) 740M Graphics — GPU Core". Um classificador que só aceitasse o nome curto
    /// funcionaria nos testes e falharia no app real, mostrando "sem leitura" com o sensor vivo.
    /// </summary>
    private static Task GpuComRotuloDoProvider()
    {
        var sensores = new[]
        {
            ("AMD Radeon(TM) 740M Graphics — GPU Core", 48.0),
            ("AMD Radeon(TM) 740M Graphics — GPU VR SoC", 47.0),
        };

        var temperatura = ClassificadorTemperatura.DaGpu(
            AmostraDe(FlavosDiag.Core.Modelos.TipoHardware.Gpu, sensores), out var causa);
        Assert.True(Equals(48.0, temperatura), "o rótulo composto precisa ser reconhecido");
        Assert.True(causa is null, "havendo leitura não há causa");
        return Task.CompletedTask;
    }

    private static Task CpuComRotuloDoProvider()
    {
        var sensores = new[]
        {
            ("AMD Ryzen 5 8500G w/ Radeon 740M Graphics — Core (Tctl/Tdie)", 0.0),
            ("AMD Ryzen 5 8500G w/ Radeon 740M Graphics — CPU Package", 62.0),
        };

        // Tctl = 0 é sentinela; o classificador precisa cair no próximo sensor válido.
        var temperatura = ClassificadorTemperatura.DaCpu(
            AmostraDe(FlavosDiag.Core.Modelos.TipoHardware.Cpu, sensores), out _);
        Assert.True(Equals(62.0, temperatura), "com Tctl inválido a leitura vem do pacote");
        return Task.CompletedTask;
    }

    private static Task CpuPrefereTctl()
    {
        // Ordem invertida de propósito: o sensor do die precisa ganhar mesmo vindo depois.
        var sensores = new[] { ("CPU Package", 55.0), ("Core (Tctl/Tdie)", 61.0) };
        var temperatura = ClassificadorTemperatura.DaCpu(
            AmostraDe(FlavosDiag.Core.Modelos.TipoHardware.Cpu, sensores), out _);
        Assert.True(Equals(61.0, temperatura), "o sensor do die tem prioridade");
        return Task.CompletedTask;
    }

    private static Task HistoricoCircular()
    {
        var historico = new HistoricoAmostras(capacidade: 3);
        for (var i = 1; i <= 5; i++)
        {
            historico.Adicionar(Amostra(segundos: i));
        }

        Assert.True(Equals(3, historico.Quantidade), "o histórico não cresce além da capacidade");
        var lista = historico.Listar();
        Assert.True(Equals(3.0, lista[0].Segundos), "a mais antiga retida precisa ser a de 3 s");
        Assert.True(Equals(5.0, lista[^1].Segundos), "a mais recente precisa ser a última");
        return Task.CompletedTask;
    }

    private static Task Janela()
    {
        var historico = new HistoricoAmostras(capacidade: 10);
        for (var i = 0; i <= 10; i++)
        {
            historico.Adicionar(Amostra(segundos: i));
        }

        var janela = historico.Janela(5);
        Assert.True(janela.Count >= 5, "a janela precisa conter o intervalo pedido");
        Assert.True(janela[0].Segundos >= 5, "nada mais antigo que a janela pode entrar");
        Assert.True(Equals(10.0, janela[^1].Segundos), "o ponto mais recente fica na janela");
        return Task.CompletedTask;
    }

    private static Task AmostraSemLeitura()
    {
        var amostra = new AmostraTemporal
        {
            Quando = DateTimeOffset.Now,
            Segundos = 0,
            TemperaturaCpuCelsius = null,
            TemperaturaGpuCelsius = null,
        };

        Assert.True(!amostra.TemAlgumaTemperatura, "amostra sem nenhuma temperatura válida não tem leitura");
        return Task.CompletedTask;
    }

    private static AmostraTemporal Amostra(double segundos) => new()
    {
        Quando = DateTimeOffset.Now,
        Segundos = segundos,
        TemperaturaCpuCelsius = 50,
    };
}
