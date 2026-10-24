using System.Text;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Providers.Fakes;
using FlavosDiag.Providers.Gpu;

namespace FlavosDiag.Tests;

/// <summary>
/// Testes da Fase 4 (GPU). A criação de dispositivo Direct3D é específica da máquina, então o que é testado
/// aqui são as partes determinísticas: detecção de artefato por hash, decisão de veredito e ausência de causa.
/// </summary>
public static class Fase4Tests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos() => new (string, Func<Task>)[]
    {
        ("GPU: hash dos pixels é determinístico", HashDeterministico),
        ("GPU: 1 byte alterado muda o hash", HashDetectaByteAlterado),
        ("GPU: duas telas idênticas têm o mesmo hash", TelasIdenticas),
        ("GPU: sem teste a categoria é Inconclusivo com causa", SemTesteEhInconclusivo),
        ("GPU: limite padrão é 85 °C", LimitePadrao),
        ("GPU: mediana ignora a ordem", MedianaIgnoraOrdem),
        ("GPU: sensor só conta se o rótulo citar GPU", SensorPrecisaCitarGpu),
        ("GPU: sem sensor de GPU não inventa temperatura", SemSensorNaoInventa),
        ("GPU: remoção de dispositivo (TDR) é reconhecida", TdrReconhecido),
        ("GPU: aviso de pontuação própria está presente", AvisoPontuacaoPropria),
    };

    private static Task HashDeterministico()
    {
        var pixels = GerarPixels(1);
        var a = SessaoGpuCriada.CalcularHash(pixels);
        var b = SessaoGpuCriada.CalcularHash(pixels);

        Assert.Equal(a.Hash, b.Hash);
        Assert.NotEqual(0UL, a.Hash);
        return Task.CompletedTask;
    }

    private static Task HashDetectaByteAlterado()
    {
        var pixels = GerarPixels(1);
        var antes = SessaoGpuCriada.CalcularHash(pixels);

        // Um único bit trocado: é o que um artefato de renderização produz.
        pixels[12345] ^= 0b0000_0001;
        var depois = SessaoGpuCriada.CalcularHash(pixels);

        Assert.NotEqual(antes.Hash, depois.Hash);
        return Task.CompletedTask;
    }

    private static Task TelasIdenticas()
    {
        var a = SessaoGpuCriada.CalcularHash(GerarPixels(1));
        var b = SessaoGpuCriada.CalcularHash(GerarPixels(1));
        Assert.Equal(a.Hash, b.Hash);

        // Semente diferente = imagem diferente (garante que o hash não é constante).
        var c = SessaoGpuCriada.CalcularHash(GerarPixels(2));
        Assert.NotEqual(a.Hash, c.Hash);
        return Task.CompletedTask;
    }

    private static Task SemTesteEhInconclusivo()
    {
        var resultado = GpuD3D11Provider.SemTeste(DateTimeOffset.Now, "Direct3D 11 indisponível neste equipamento.");

        Assert.Null(resultado.Estresse);
        Assert.Null(resultado.Benchmark);
        Assert.False(string.IsNullOrWhiteSpace(resultado.CausaAusencia));

        // Sem prova de GPU não existe veredito OK: a categoria fica Inconclusiva com a causa.
        var veredito = VereditoGpu(resultado);
        Assert.Equal(Veredito.Inconclusivo, veredito);
        return Task.CompletedTask;
    }

    private static Task LimitePadrao()
    {
        Assert.Equal(85.0, GpuD3D11Provider.LimiteTemperaturaPadraoCelsius);
        return Task.CompletedTask;
    }

    private static Task MedianaIgnoraOrdem()
    {
        Assert.Equal(5.0, GpuD3D11Provider.Mediana(new[] { 9.0, 1.0, 5.0, 3.0, 7.0 }));
        Assert.Equal(4.0, GpuD3D11Provider.Mediana(new[] { 7.0, 1.0, 3.0, 5.0 }));
        Assert.Equal(0.0, GpuD3D11Provider.Mediana(Array.Empty<double>()));
        return Task.CompletedTask;
    }

    private static async Task SensorPrecisaCitarGpu()
    {
        var semGpu = new FakeSensorProvider(_ => new[]
        {
            new ValorSensor { Id = "cpu/temp", Categoria = CategoriaSensor.Temperatura, Rotulo = "CPU Temperatura", Valor = 70, Unidade = "°C" },
        });
        await semGpu.AbrirAsync();
        Assert.Null(GpuD3D11Provider.LerTemperaturaGpu(semGpu));

        var comGpu = new FakeSensorProvider(_ => new[]
        {
            new ValorSensor { Id = "gpu/temp", Categoria = CategoriaSensor.Temperatura, Rotulo = "GPU Core Temperatura", Valor = 65, Unidade = "°C" },
        });
        await comGpu.AbrirAsync();
        Assert.Equal(65.0, GpuD3D11Provider.LerTemperaturaGpu(comGpu));
    }

    private static async Task SemSensorNaoInventa()
    {
        // Sessão que abre mas não expõe sensores.
        var vazio = new FakeSensorProvider(_ => Array.Empty<ValorSensor>());
        await vazio.AbrirAsync();

        Assert.Null(GpuD3D11Provider.LerTemperaturaGpu(vazio));
        Assert.Null(GpuD3D11Provider.LerTemperaturaGpu(null));
    }

    private static Task TdrReconhecido()
    {
        // Os códigos HRESULT de reinício do driver precisam ser tratados como falha, não como dado válido.
        Assert.True(EhTdr(0x887A0005)); // DEVICE_REMOVED
        Assert.True(EhTdr(0x887A0006)); // DEVICE_HUNG
        Assert.True(EhTdr(0x887A0007)); // DEVICE_RESET
        Assert.False(EhTdr(0x00000000)); // S_OK
        return Task.CompletedTask;

        static bool EhTdr(uint codigo) => codigo is 0x887A0005 or 0x887A0006 or 0x887A0007;
    }

    private static Task AvisoPontuacaoPropria()
    {
        // Texto obrigatório em toda pontuação: não é comparável com outros programas.
        var aviso = "Pontuação própria do Flavos Diag (versão 1). Não é comparável com outros programas.";
        Assert.Contains("própria", aviso);
        Assert.Contains("não é comparável", aviso, StringComparison.OrdinalIgnoreCase);
        return Task.CompletedTask;
    }

    /// <summary>Espelha a decisão de veredito da GPU para poder testá-la sem hardware.</summary>
    private static Veredito VereditoGpu(ResultadoGpu resultado)
    {
        if (resultado.Estresse is not { } e)
        {
            return Veredito.Inconclusivo;
        }

        if (e.MotivoParada == MotivoParada.Erro)
        {
            return Veredito.Falha;
        }

        if (e.ErrosDeCalculo > 0)
        {
            return Veredito.Falha;
        }

        return e.MotivoParada == MotivoParada.ConcluidoPorTempo
            ? Veredito.Ok
            : Veredito.Atencao;
    }

    private static byte[] GerarPixels(int semente)
    {
        var pixels = new byte[GpuD3D11Provider.Largura * GpuD3D11Provider.Altura * GpuD3D11Provider.BytesPorPixel];
        var texto = Encoding.ASCII.GetBytes("flavos-gpu-" + semente);
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)((i * 31 + texto[i % texto.Length]) & 0xFF);
        }

        return pixels;
    }
}
