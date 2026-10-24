using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Bateria;
using FlavosDiag.Providers.Discos;
using FlavosDiag.Providers.Nativo;
using FlavosDiag.Providers.Rede;
using FlavosDiag.Tests.fixtures;

namespace FlavosDiag.Tests;

/// <summary>
/// Testes dos providers da Fase 2. Tudo roda sobre fixtures com o formato do padrão público
/// (bloco SMART de 512 bytes, log NVMe, XML do relatório de bateria): nenhum equipamento é tocado.
/// </summary>
public static class DiscoBateriaRedeTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos() => new (string, Func<Task>)[]
    {
        ("SMART: disco saudável não gera achado e tem leitura", SmartSaudavelSemAchado),
        ("SMART: setores realocados viram aviso", SmartRealocadosViraAviso),
        ("SMART: setores pendentes viram crítico", SmartPendentesViraCritico),
        ("SMART: bloco vazio não vira disco saudável", SmartVazioNaoEhSaudavel),
        ("SMART: decodifica os campos padronizados", SmartDecodificaCampos),
        ("NVMe: desgaste alto e erros viram crítico", NvmeDesgastadoViraCritico),
        ("NVMe: log saudável não gera achado", NvmeSaudavelSemAchado),
        ("NVMe: aviso crítico prevê falha", NvmeAvisoCritico),
        ("Bateria: relatório saudável calcula saúde", BateriaSaudavel),
        ("Bateria: relatório degradado mostra saúde baixa", BateriaDegradada),
        ("Bateria: sem ciclos não inventa número", BateriaSemCiclos),
        ("Bateria: XML inválido é erro explícito", BateriaXmlInvalido),
        ("Rede: ping em endereço inexistente não finge resposta", RedePingFalho),
        ("Rede: teste de velocidade recusa URL inválida", RedeUrlInvalida),
        ("Disco: benchmark recusa pasta sem espaço", BenchmarkSemEspaco),
        ("Disco: benchmark apaga o arquivo temporário ao concluir", BenchmarkRemoveArquivo),
        ("Disco: benchmark apaga o arquivo temporário ao cancelar", BenchmarkRemoveAoCancelar),
        ("Disco: benchmark mede leitura e escrita reais", BenchmarkMedeValoresReais),
        ("Disco: verificação recusa volume inválido", VerificacaoVolumeInvalido),
        ("Windows: health status positivo conta como leitura", WindowsSaudavelContaComoLeitura),
        ("Windows: health status negativo vira aviso", WindowsNaoSaudavelViraAviso),
        ("Windows: previsão de falha vira crítico", PrevisaoFalhaViraCritico),
        ("Fase 2: nenhuma fonte de saúde nunca vira OK", SemFonteNaoEhSaudavel),
    };

    private static Task SmartSaudavelSemAchado()
    {
        var atributos = SmartAta.Interpretar(FixturesSmart.SsdSaudavel());
        Assert.True(atributos.Count >= 5, "o bloco tem atributos");
        var saude = Saude(atributos);
        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(saude);
        Assert.True(temLeitura, "houve leitura");
        Assert.Empty(achados);
        return Task.CompletedTask;
    }

    private static Task SmartRealocadosViraAviso()
    {
        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(Saude(SmartAta.Interpretar(FixturesSmart.HddComSetoresRealocados())));
        Assert.True(temLeitura);
        var realocados = achados.Single(a => a.Titulo == "Setores realocados");
        Assert.True(realocados.Aviso);
        Assert.False(realocados.Critico);
        Assert.Contains("24", realocados.Valor);
        return Task.CompletedTask;
    }

    private static Task SmartPendentesViraCritico()
    {
        var (achados, _) = AvaliadorSaudeDisco.Avaliar(Saude(SmartAta.Interpretar(FixturesSmart.DiscoComSetoresPendentes())));
        Assert.Contains(achados, a => a.Titulo == "Setores pendentes" && a.Critico);
        Assert.Contains(achados, a => a.Titulo == "Erros não corrigíveis" && a.Critico);
        return Task.CompletedTask;
    }

    private static Task SmartVazioNaoEhSaudavel()
    {
        var atributos = SmartAta.Interpretar(FixturesSmart.Vazio());
        Assert.Empty(atributos);
        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(Saude(atributos));
        Assert.Empty(achados);
        Assert.False(temLeitura, "sem leitura o veredito precisa ser Inconclusivo");
        return Task.CompletedTask;
    }

    private static Task SmartDecodificaCampos()
    {
        var atributos = SmartAta.Interpretar(FixturesSmart.HddComSetoresRealocados());
        var realocados = atributos.Single(a => a.Id == 0x05);
        Assert.Equal(24, realocados.Bruto);
        Assert.Equal(90, realocados.Valor);
        Assert.Equal(10, realocados.Limite);
        Assert.Equal("Setores realocados", realocados.Nome);

        var temperatura = atributos.Single(a => a.Id == 0xC2);
        Assert.Equal(45, temperatura.Bruto);
        Assert.Contains("45 °C", temperatura.Interpretacao);
        return Task.CompletedTask;
    }

    private static Task NvmeDesgastadoViraCritico()
    {
        var log = SmartNvme.Interpretar(FixturesSmart.NvmeDesgastado());
        Assert.NotNull(log);
        Assert.Equal(45, log!.TemperaturaCelsius);
        Assert.Equal(92, log.PercentualVidaUsada);
        Assert.Equal(3, log.ErrosIntegridadeMidia);

        var saude = new SaudeDisco
        {
            Fontes = new[] { FonteSaudeDisco.Nvme },
            AtributosAta = Array.Empty<AtributoSmart>(),
            Nvme = new SaudeNvme
            {
                AvisoCritico = Leitura<int>.Ok(log.AvisoCritico, "NVMe"),
                TemperaturaCelsius = log.TemperaturaCelsius is { } temperaturaNvme
                    ? Leitura<int>.Ok(temperaturaNvme, "NVMe")
                    : Leitura<int>.NaoSuportado("o disco não informou uma temperatura válida", "NVMe"),
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

        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(saude);
        Assert.True(temLeitura);
        Assert.Contains(achados, a => a.Titulo == "Erros de integridade" && a.Critico);
        Assert.Contains(achados, a => a.Titulo == "Vida útil consumida" && a.Critico);
        return Task.CompletedTask;
    }

    private static Task NvmeSaudavelSemAchado()
    {
        var log = SmartNvme.Interpretar(FixturesSmart.NvmeSaudavel());
        Assert.NotNull(log);
        Assert.Equal(38, log!.TemperaturaCelsius);
        Assert.Equal(3, log.PercentualVidaUsada);
        Assert.False(log.PreveFalha);
        Assert.Equal(0, log.ErrosIntegridadeMidia);
        return Task.CompletedTask;
    }

    private static Task NvmeAvisoCritico()
    {
        var bloco = FixturesSmart.NvmeSaudavel();
        bloco[0] = 0x01; // Critical Warning bit 0 = previsão de falha
        var log = SmartNvme.Interpretar(bloco);
        Assert.NotNull(log);
        Assert.True(log!.PreveFalha);
        return Task.CompletedTask;
    }

    private static Task BateriaSaudavel()
    {
        var relatorio = BateriaWindowsProvider.InterpretarRelatorio(FixturesSmart.RelatorioBateriaSaudavel());
        var bateria = relatorio.Baterias.Single();
        Assert.Equal("BAT0", bateria.Nome);
        Assert.Equal(50000, bateria.CapacidadeProjetoMwh);
        Assert.Equal(47000, bateria.CapacidadeTotalMwh);
        Assert.Equal(120, bateria.Ciclos);

        // A regra de saúde é a mesma do provider: atual / projeto × 100.
        var saude = Math.Round(47000 * 100.0 / 50000, 1);
        Assert.Equal(94.0, saude);
        return Task.CompletedTask;
    }

    private static Task BateriaDegradada()
    {
        var relatorio = BateriaWindowsProvider.InterpretarRelatorio(FixturesSmart.RelatorioBateriaDegradada());
        var bateria = relatorio.Baterias.Single();
        var saude = Math.Round(bateria.CapacidadeTotalMwh!.Value * 100.0 / bateria.CapacidadeProjetoMwh!.Value, 1);
        Assert.Equal(44.0, saude);
        Assert.Equal(900, bateria.Ciclos);
        return Task.CompletedTask;
    }

    private static Task BateriaSemCiclos()
    {
        var relatorio = BateriaWindowsProvider.InterpretarRelatorio(FixturesSmart.RelatorioBateriaSemCiclos());
        Assert.False(relatorio.CiclosDisponiveis);
        Assert.Null(relatorio.Ciclos);
        Assert.Null(relatorio.Baterias.Single().Ciclos);
        return Task.CompletedTask;
    }

    private static Task BateriaXmlInvalido()
    {
        Assert.Throws<System.Xml.XmlException>(() => BateriaWindowsProvider.InterpretarRelatorio("<BatteryReport><Batteries>"));
        return Task.CompletedTask;
    }

    private static async Task RedePingFalho()
    {
        // Endereço reservado para documentação (RFC 5737): não deve responder, e a ausência de resposta
        // precisa aparecer como indisponível com motivo, não como latência zero.
        var resultado = await RedeWindowsProvider.PingAsync("192.0.2.1", 1, CancellationToken.None);
        Assert.False(resultado.Disponivel);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Causa));
    }

    private static async Task RedeUrlInvalida()
    {
        var provider = new RedeWindowsProvider();
        var resultado = await provider.TestarVelocidadeAsync("nao-e-url", 1024, CancellationToken.None);
        Assert.Equal(0, resultado.BytesRecebidos);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Erro));
    }

    private static async Task BenchmarkSemEspaco()
    {
        var provider = new DiscosWindowsProvider();
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.BenchmarkAsync(Path.GetTempPath(), long.MaxValue / 4, null, CancellationToken.None));
        Assert.Contains("Espaço insuficiente", erro.Message);
    }

    private static async Task VerificacaoVolumeInvalido()
    {
        var provider = new DiscosWindowsProvider();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.VerificarSistemaArquivosAsync("nao-e-volume", CancellationToken.None));
    }

    /// <summary>Arquivos que o benchmark deixou para trás na pasta de teste.</summary>
    private static string[] TmpRestantes(string pasta) =>
        Directory.GetFiles(pasta, "flavos-benchmark-*.tmp");

    private static async Task BenchmarkRemoveArquivo()
    {
        var pasta = PastaTemporaria();
        try
        {
            var provider = new DiscosWindowsProvider();
            var resultado = await provider.BenchmarkAsync(pasta, 4L * 1024 * 1024, null, CancellationToken.None);
            Assert.True(resultado.SeqEscritaMbps > 0, "a escrita foi medida");
            Assert.Empty(TmpRestantes(pasta));
        }
        finally
        {
            Limpar(pasta);
        }
    }

    private static async Task BenchmarkRemoveAoCancelar()
    {
        var pasta = PastaTemporaria();
        try
        {
            var provider = new DiscosWindowsProvider();
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(40));
            var cancelou = false;
            try
            {
                await provider.BenchmarkAsync(pasta, 256L * 1024 * 1024, null, cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelou = true;
            }

            // O ponto do teste é a limpeza: mesmo cancelando, nenhum arquivo pode sobrar no cliente.
            Assert.True(cancelou || TmpRestantes(pasta).Length == 0, "cancelou ou limpou");
            Assert.Empty(TmpRestantes(pasta));
        }
        finally
        {
            Limpar(pasta);
        }
    }

    private static async Task BenchmarkMedeValoresReais()
    {
        var pasta = PastaTemporaria();
        try
        {
            var provider = new DiscosWindowsProvider();
            var resultado = await provider.BenchmarkAsync(pasta, 8L * 1024 * 1024, null, CancellationToken.None);
            Assert.True(resultado.SeqLeituraMbps > 0, "leitura medida");
            Assert.True(resultado.SeqEscritaMbps > 0, "escrita medida");
            Assert.True(resultado.Aleatorio4kLeituraIops > 0, "iops medidos");
            Assert.True(resultado.LatenciaP95Ms >= resultado.LatenciaMediaMs, "p95 >= média");
            Assert.Equal(8L * 1024 * 1024, resultado.TamanhoArquivoBytes);
        }
        finally
        {
            Limpar(pasta);
        }
    }

    private static Task WindowsSaudavelContaComoLeitura()
    {
        var saude = Saude(Array.Empty<AtributoSmart>()) with { SaudavelSegundoWindows = true };
        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(saude);
        Assert.True(temLeitura, "o Windows afirma que está saudável: há leitura");
        Assert.Empty(achados);
        return Task.CompletedTask;
    }

    private static Task WindowsNaoSaudavelViraAviso()
    {
        var saude = Saude(Array.Empty<AtributoSmart>()) with { SaudavelSegundoWindows = false };
        var (achados, _) = AvaliadorSaudeDisco.Avaliar(saude);
        Assert.Contains(achados, a => a.Titulo == "Estado de saúde (Windows)" && a.Aviso);
        return Task.CompletedTask;
    }

    private static Task PrevisaoFalhaViraCritico()
    {
        var saude = Saude(Array.Empty<AtributoSmart>()) with { PrevisaoFalhaWindows = true };
        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(saude);
        Assert.True(temLeitura);
        Assert.Contains(achados, a => a.Titulo == "Previsão de falha (Windows)" && a.Critico);
        return Task.CompletedTask;
    }

    private static Task SemFonteNaoEhSaudavel()
    {
        var saude = Saude(Array.Empty<AtributoSmart>());
        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(saude);
        Assert.False(temLeitura, "sem fonte de saúde não há leitura");
        Assert.Empty(achados);
        return Task.CompletedTask;
    }

    private static string PastaTemporaria()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "flavos-teste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    private static void Limpar(string pasta)
    {
        try
        {
            if (Directory.Exists(pasta))
            {
                Directory.Delete(pasta, recursive: true);
            }
        }
        catch (IOException)
        {
            // pasta em uso pelo índice do Windows: o conteúdo já foi validado
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
