using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Discos;
using FlavosDiag.Providers.Nativo;
using FlavosDiag.Tests.fixtures;

namespace FlavosDiag.Tests;

/// <summary>
/// Testes da validação de temperatura de disco: a regra é uma só (<see cref="Temperatura"/>) e vale para
/// as três fontes — SMART ATA (0xC2/0xBE), log NVMe e contadores de confiabilidade do Windows.
/// O ponto central: sentinela (0, 255) ou valor fora de faixa significa "sem leitura", <b>nunca</b> um
/// valor real e <b>nunca</b> um aviso "Atenção" no laudo.
/// </summary>
public static class TemperaturaTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos() => new (string, Func<Task>)[]
    {
        ("Temperatura: 0 é ausência, não 0 °C", ZeroEhAusente),
        ("Temperatura: 255 (0xFF) é ausência", SentinelaFFEhAusente),
        ("Temperatura: 121 está fora da faixa e é ausência", AcimaDaFaixaEhAusente),
        ("Temperatura: negativos e 256 também são ausência", NegativoEAcimaDoByteSaoAusentes),
        ("Temperatura: 1 °C é leitura válida", MinimoEhValido),
        ("Temperatura: 120 °C é leitura válida", MaximoEhValido),
        ("Temperatura: 0 e 255 são os sentinelas conhecidos", SentinelasReconhecidos),
        ("NVMe: Kelvin que mapeia para <= 0 °C vira null", NvmeKelvinSemLeitura),
        ("NVMe: Kelvin 0xFFFF (sentinela) vira null", NvmeKelvinSentinela),
        ("NVMe: Kelvin fora da faixa vira null", NvmeKelvinForaDaFaixa),
        ("NVMe: Kelvin que mapeia 1 e 120 °C conta como leitura", NvmeKelvinValido),
        ("SMART: 0xC2 com 0/255/121 não gera nenhum aviso", SmartSentinelaSemAviso),
        ("SMART: 0xBE com 0/255 também não gera aviso", SmartAlternativoSentinelaSemAviso),
        ("SMART: atributo inválido continua listado, com causa em pt-BR", SmartAtributoListadoComCausa),
        ("SMART: 0xC2 inválido nunca é publicado como leitura", SmartPrincipalTemPrecedencia),
        ("SMART: 0xBE válido é usado quando 0xC2 não tem leitura", SmartAlternativoValidoComoFallback),
        ("SMART: temperatura válida continua aparecendo como leitura", SmartValidoContinuaLeitura),
        ("Disco: NVMe sem temperatura não gera aviso e a leitura fica ausente", DiscoNvmeSemAviso),
    };

    // ------------------------------------------------------------------ regra única

    private static Task ZeroEhAusente()
    {
        var leitura = Temperatura.InterpretarTemperatura(0, "fixture");
        Assert.False(leitura.Disponivel, "0 é sentinela, não leitura");
        Assert.Equal(EstadoLeitura.NaoSuportado, leitura.Estado);
        Assert.False(string.IsNullOrWhiteSpace(leitura.Causa), "a ausência precisa de causa");
        Assert.Contains("não informou uma temperatura válida", leitura.Causa!);
        Assert.False(Temperatura.TemperaturaValida(0));
        return Task.CompletedTask;
    }

    private static Task SentinelaFFEhAusente()
    {
        var leitura = Temperatura.InterpretarTemperatura(255, "fixture");
        Assert.False(leitura.Disponivel, "255 é sentinela, não leitura");
        Assert.Equal(EstadoLeitura.NaoSuportado, leitura.Estado);
        Assert.Contains("255", leitura.Causa!);
        Assert.False(Temperatura.TemperaturaValida(255));
        return Task.CompletedTask;
    }

    private static Task AcimaDaFaixaEhAusente()
    {
        var leitura = Temperatura.InterpretarTemperatura(121, "fixture");
        Assert.False(leitura.Disponivel, "121 °C está fora da faixa aceita");
        Assert.Contains("fora de", leitura.Causa!);
        Assert.False(Temperatura.TemperaturaValida(121));
        return Task.CompletedTask;
    }

    private static Task NegativoEAcimaDoByteSaoAusentes()
    {
        Assert.False(Temperatura.TemperaturaValida(-1));
        Assert.False(Temperatura.TemperaturaValida(-273));
        Assert.False(Temperatura.TemperaturaValida(256));
        Assert.False(Temperatura.TemperaturaValida(int.MaxValue));
        Assert.False(Temperatura.InterpretarTemperatura(-40).Disponivel);
        return Task.CompletedTask;
    }

    private static Task MinimoEhValido()
    {
        var leitura = Temperatura.InterpretarTemperatura(1, "fixture");
        Assert.True(leitura.Disponivel, "1 °C é uma leitura física plausível");
        Assert.Equal(1, leitura.Valor);
        Assert.Null(leitura.Causa);
        return Task.CompletedTask;
    }

    private static Task MaximoEhValido()
    {
        var leitura = Temperatura.InterpretarTemperatura(120, "fixture");
        Assert.True(leitura.Disponivel, "120 °C é o limite superior aceito");
        Assert.Equal(120, leitura.Valor);
        Assert.True(Temperatura.TemperaturaValida(120));
        return Task.CompletedTask;
    }

    private static Task SentinelasReconhecidos()
    {
        Assert.True(Temperatura.EhSentinela(0));
        Assert.True(Temperatura.EhSentinela(255));
        Assert.False(Temperatura.EhSentinela(120));
        Assert.False(Temperatura.EhSentinela(1));
        Assert.Equal(0, Temperatura.SentinelaZero);
        Assert.Equal(255, Temperatura.SentinelaFantasma);
        Assert.Equal(1, Temperatura.MinimoCelsius);
        Assert.Equal(120, Temperatura.MaximoCelsius);
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ NVMe

    private static Task NvmeKelvinSemLeitura()
    {
        // 273 K = 0 °C e abaixo disso não é leitura; o campo zerado precisa virar null, não 0.
        Assert.Null(Temperatura.DeKelvinParaCelsius(0));
        Assert.Null(Temperatura.DeKelvinParaCelsius(1));
        Assert.Null(Temperatura.DeKelvinParaCelsius(273));
        Assert.Null(SmartNvme.Interpretar(BlocoNvme(kelvin: 0))!.TemperaturaCelsius);
        Assert.Null(SmartNvme.Interpretar(BlocoNvme(kelvin: 273))!.TemperaturaCelsius);
        return Task.CompletedTask;
    }

    private static Task NvmeKelvinSentinela()
    {
        Assert.Null(Temperatura.DeKelvinParaCelsius(0xFFFF));
        Assert.Null(SmartNvme.Interpretar(BlocoNvme(kelvin: 0xFFFF))!.TemperaturaCelsius);
        return Task.CompletedTask;
    }

    private static Task NvmeKelvinForaDaFaixa()
    {
        // 393 K = 120 °C é aceito; 394 K = 121 °C já está fora.
        Assert.Equal(120, Temperatura.DeKelvinParaCelsius(393));
        Assert.Null(Temperatura.DeKelvinParaCelsius(394));
        Assert.Null(SmartNvme.Interpretar(BlocoNvme(kelvin: 600))!.TemperaturaCelsius);
        return Task.CompletedTask;
    }

    private static Task NvmeKelvinValido()
    {
        Assert.Equal(1, Temperatura.DeKelvinParaCelsius(274));
        Assert.Equal(120, Temperatura.DeKelvinParaCelsius(393));
        Assert.Equal(45, SmartNvme.Interpretar(BlocoNvme(kelvin: 318))!.TemperaturaCelsius);
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ SMART ATA

    private static Task SmartSentinelaSemAviso()
    {
        foreach (var bruto in new long[] { 0, 255, 121, 256, -1 })
        {
            var atributos = SmartAta.Interpretar(BlocoSmart(temperaturaC2: bruto));
            var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(Saude(atributos));

            Assert.Empty(achados);
            Assert.True(temLeitura, "os outros atributos do bloco ainda são leitura (horas, setores)");

            var sensor = atributos.Single(a => a.Id == SmartAta.IdTemperatura);
            Assert.True(sensor.LeituraIndisponivel, $"bruto {bruto} precisa estar marcado como indisponível");
            // Nada de "<valor> °C" na interpretação: só a causa cita a faixa e o bruto recebido.
            Assert.False(
                sensor.Interpretacao.StartsWith(bruto.ToString(), StringComparison.Ordinal),
                "a interpretação não pode começar com o valor como se fosse medição");
        }

        return Task.CompletedTask;
    }

    private static Task SmartAlternativoSentinelaSemAviso()
    {
        foreach (var bruto in new long[] { 0, 255 })
        {
            var atributos = SmartAta.Interpretar(BlocoSmart(temperaturaC2: null, temperaturaBe: bruto));
            var sensor = atributos.Single(a => a.Id == SmartAta.IdTemperaturaAlternativa);
            Assert.True(sensor.LeituraIndisponivel, $"bruto {bruto} do 0xBE precisa estar marcado como indisponível");

            var (achados, _) = AvaliadorSaudeDisco.Avaliar(Saude(atributos));
            Assert.Empty(achados);

            var leitura = SmartAta.LerTemperatura(atributos);
            Assert.False(leitura.Disponivel, "sentinela no 0xBE não pode virar leitura");
        }

        return Task.CompletedTask;
    }

    private static Task SmartAtributoListadoComCausa()
    {
        var atributos = SmartAta.Interpretar(BlocoSmart(temperaturaC2: 0));
        var sensor = atributos.Single(a => a.Id == SmartAta.IdTemperatura);

        // O atributo continua listado (o técnico vê que o sensor existe)…
        Assert.Equal("Temperatura", sensor.Nome);
        Assert.Equal(0, sensor.Bruto);

        // …mas a interpretação declara a ausência em pt-BR, com a causa e o valor recebido.
        Assert.Contains("indisponível", sensor.Interpretacao, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0, 255", sensor.Interpretacao);
        Assert.Contains("não informou uma temperatura válida", sensor.Interpretacao);

        var leitura = SmartAta.LerTemperatura(atributos);
        Assert.False(leitura.Disponivel);
        Assert.False(string.IsNullOrWhiteSpace(leitura.Causa));
        Assert.Contains("temperatura", leitura.Causa!, StringComparison.OrdinalIgnoreCase);
        return Task.CompletedTask;
    }

    private static Task SmartPrincipalTemPrecedencia()
    {
        // Disco típico em que 0xC2 traz a temperatura real e 0xBE traz só o mínimo (30) no bruto.
        var atributos = SmartAta.Interpretar(BlocoSmart(temperaturaC2: 42, temperaturaBe: 30));
        var leitura = SmartAta.LerTemperatura(atributos);
        Assert.True(leitura.Disponivel);
        Assert.Equal(42, leitura.Valor);

        // Com o 0xC2 em sentinela e o 0xBE válido, prevalece a leitura existente — nunca o sentinela.
        var comSentinela = SmartAta.Interpretar(BlocoSmart(temperaturaC2: 0, temperaturaBe: 51));
        var doAlternativo = SmartAta.LerTemperatura(comSentinela);
        Assert.True(doAlternativo.Disponivel, "sentinela no 0xC2 não derruba uma leitura válida do 0xBE");
        Assert.Equal(51, doAlternativo.Valor);

        // Já com o 0xC2 presente e inválido, o valor dele nunca é publicado como se fosse leitura.
        var soPrincipalInvalido = SmartAta.Interpretar(BlocoSmart(temperaturaC2: 255));
        var semLeitura = SmartAta.LerTemperatura(soPrincipalInvalido);
        Assert.False(semLeitura.Disponivel);
        Assert.Contains("não informou uma temperatura válida", semLeitura.Causa!);

        // E o 0xBE inválido nunca vira leitura do disco.
        var alternativaInvalida = SmartAta.Interpretar(BlocoSmart(temperaturaC2: null, temperaturaBe: 0));
        var leituraAlternativa = SmartAta.LerTemperatura(alternativaInvalida);
        Assert.False(leituraAlternativa.Disponivel);
        Assert.False(string.IsNullOrWhiteSpace(leituraAlternativa.Causa));

        return Task.CompletedTask;
    }

    private static Task SmartAlternativoValidoComoFallback()
    {
        var atributos = SmartAta.Interpretar(BlocoSmart(temperaturaC2: null, temperaturaBe: 51));
        var leitura = SmartAta.LerTemperatura(atributos);
        Assert.True(leitura.Disponivel, "sem 0xC2, o 0xBE válido serve");
        Assert.Equal(51, leitura.Valor);
        return Task.CompletedTask;
    }

    private static Task SmartValidoContinuaLeitura()
    {
        foreach (var bruto in new long[] { 1, 45, 120 })
        {
            var atributos = SmartAta.Interpretar(BlocoSmart(temperaturaC2: bruto));
            var sensor = atributos.Single(a => a.Id == SmartAta.IdTemperatura);
            Assert.False(sensor.LeituraIndisponivel);
            Assert.Contains($"{bruto} °C", sensor.Interpretacao);

            var leitura = SmartAta.LerTemperatura(atributos);
            Assert.True(leitura.Disponivel);
            Assert.Equal((int)bruto, leitura.Valor);
        }

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ disco completo

    private static Task DiscoNvmeSemAviso()
    {
        // Log NVMe com o campo Kelvin zerado (controlador que não informa temperatura).
        var log = SmartNvme.Interpretar(BlocoNvme(kelvin: 0))!;
        Assert.Null(log.TemperaturaCelsius);

        var saude = new SaudeDisco
        {
            Fontes = new[] { FonteSaudeDisco.Nvme },
            AtributosAta = Array.Empty<AtributoSmart>(),
            Nvme = new SaudeNvme
            {
                AvisoCritico = Leitura<int>.Ok(log.AvisoCritico, "NVMe"),
                TemperaturaCelsius = Temperatura.InterpretarTemperatura(log.TemperaturaCelsius ?? 0, "NVMe log 0x02"),
                EspacoDisponivelPercentual = Leitura<int>.Ok(log.EspacoDisponivelPercentual, "NVMe"),
                LimiarEspacoDisponivel = Leitura<int>.Ok(log.LimiarEspacoPercentual, "NVMe"),
                PercentualVidaUsada = Leitura<double>.Ok(log.PercentualVidaUsada, "NVMe"),
                DadosEscritosGb = Leitura<long>.Ok(log.DadosEscritosGb, "NVMe"),
                DadosLidosGb = Leitura<long>.Ok(log.DadosLidosGb, "NVMe"),
                HorasLigado = Leitura<long>.Ok(log.HorasLigado, "NVMe"),
                CiclosLigadoDesligado = Leitura<long>.Ok(log.CiclosLigarDesligar, "NVMe"),
                ErrosIntegridadeMidia = Leitura<long>.Ok(log.ErrosIntegridadeMidia, "NVMe"),
            },
            Confiabilidade = null,
            Limitacoes = Array.Empty<DadoAusente>(),
        };

        Assert.False(saude.Nvme.TemperaturaCelsius.Disponivel, "sem Kelvin aproveitável não há temperatura");
        Assert.False(string.IsNullOrWhiteSpace(saude.Nvme.TemperaturaCelsius.Causa));

        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(saude);
        Assert.True(temLeitura, "o log NVMe continua sendo leitura (vida, horas, erros)");
        Assert.DoesNotContain(achados, a => a.Titulo.Contains("emperatura", StringComparison.Ordinal));
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ fixtures em memória

    /// <summary>
    /// Bloco SMART mínimo com os atributos de temperatura pedidos. <c>null</c> = atributo ausente.
    /// Base: um SSD sem setores problemáticos, para que qualquer achado venha só da temperatura.
    /// </summary>
    private static byte[] BlocoSmart(long? temperaturaC2, long? temperaturaBe = null)
    {
        var dados = FixturesSmart.SsdSaudavel();
        if (temperaturaC2 is { } c2)
        {
            EscreverAtributo(dados, indice: 2, id: SmartAta.IdTemperatura, bruto: c2);
        }
        else
        {
            ZerarAtributo(dados, indice: 2);
        }

        if (temperaturaBe is { } be)
        {
            EscreverAtributo(dados, indice: 6, id: SmartAta.IdTemperaturaAlternativa, bruto: be);
        }

        return dados;
    }

    /// <summary>Log NVMe de 512 bytes com a temperatura em Kelvin informada (resto do log saudável).</summary>
    private static byte[] BlocoNvme(int kelvin)
    {
        var dados = FixturesSmart.NvmeSaudavel();
        BitConverter.GetBytes((ushort)kelvin).CopyTo(dados, 1);
        return dados;
    }

    private static void EscreverAtributo(byte[] dados, int indice, int id, long bruto)
    {
        var baseOff = 2 + (indice * 12);
        dados[baseOff] = (byte)id;
        dados[baseOff + 3] = 100;
        dados[baseOff + 4] = 100;
        dados[baseOff + 5] = (byte)(bruto & 0xFF);
        dados[baseOff + 6] = (byte)((bruto >> 8) & 0xFF);
        dados[baseOff + 7] = (byte)((bruto >> 16) & 0xFF);
        dados[baseOff + 10] = (byte)((bruto >> 24) & 0xFF);
        dados[baseOff + 11] = (byte)((bruto >> 32) & 0xFF);
    }

    /// <summary>Marca o slot como vazio (id 0): <see cref="SmartAta.Interpretar"/> ignora esses slots.</summary>
    private static void ZerarAtributo(byte[] dados, int indice)
    {
        var baseOff = 2 + (indice * 12);
        for (var i = 0; i < 12; i++)
        {
            dados[baseOff + i] = 0;
        }
    }

    private static SaudeDisco Saude(IReadOnlyList<AtributoSmart> atributos) => new()
    {
        Fontes = atributos.Count > 0 ? new[] { FonteSaudeDisco.SmartAta } : Array.Empty<FonteSaudeDisco>(),
        AtributosAta = atributos,
        Nvme = null,
        Confiabilidade = null,
        Limitacoes = Array.Empty<DadoAusente>(),
    };
}
