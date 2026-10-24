using System.Text.Json;
using System.Text;
using FlavosDiag.Core;
using FlavosDiag.Core.Diagnostics;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Providers.Fakes;
using FlavosDiag.Reports;
using FlavosDiag.Reports.Json;
using FlavosDiag.Reports.Pdf;

namespace FlavosDiag.Tests;

/// <summary>Testes da Fase 1: relatório JSON/PDF derivado de um diagnóstico rápido com dados falsos.</summary>
public class RelatorioTests
{
    private static readonly DadosTecnico Tecnico = new()
    {
        Nome = "Técnico de Teste",
        Empresa = "Assistência Flavos",
        Cnpj = "00.000.000/0001-00",
        Contato = "(00) 0000-0000",
    };

    private static ResultadoDiagnosticoRapido DiagnosticoDeTeste(bool cancelado = false)
    {
        var inventario = Fixtures.Inventario();
        var discos = Fixtures.Discos();
        var bateria = Fixtures.Bateria();
        var rede = Fixtures.Rede();
        var amostras = new List<AmostraSensores>
        {
            new()
            {
                Quando = DateTimeOffset.Now,
                Valores = new[]
                {
                    new ValorSensor { Id = "cpu.temp", Rotulo = "Temperatura do pacote", Categoria = CategoriaSensor.Temperatura, Unidade = "°C", Valor = 55 },
                },
            },
            new()
            {
                Quando = DateTimeOffset.Now.AddSeconds(1),
                Valores = new[]
                {
                    new ValorSensor { Id = "cpu.temp", Rotulo = "Temperatura do pacote", Categoria = CategoriaSensor.Temperatura, Unidade = "°C", Valor = 61 },
                },
            },
        };

        var resumos = AmostraSensoresUtil.Resumo(amostras);
        var categorias = new List<ResultadoCategoria>
        {
            new() { Categoria = Regras.CategoriaSistema, Veredito = Regras.Sistema(inventario).Veredito, Resumo = Regras.Sistema(inventario).Resumo },
            new() { Categoria = Regras.CategoriaDisco, Veredito = Regras.Disco(discos).Veredito, Resumo = Regras.Disco(discos).Resumo },
            new() { Categoria = Regras.CategoriaBateria, Veredito = Veredito.NaoSeAplica, Resumo = "Computador de mesa sem bateria." },
        };

        return new ResultadoDiagnosticoRapido
        {
            IniciadoEm = DateTimeOffset.Now.AddMinutes(-3),
            Duracao = TimeSpan.FromMinutes(3),
            Inventario = inventario,
            Discos = discos,
            Bateria = bateria,
            UltimaAmostra = amostras.Last(),
            AmostrasSensores = amostras,
            Rede = rede,
            Etapas = new[] { new EtapaDiagnostico("Inventário", TimeSpan.FromSeconds(2), true, null) },
            Categorias = categorias,
            Agregacao = VereditoConsolidador.Agregar(categorias),
            Limitacoes = new[] { new DadoAusente("Temperatura da GPU", "Sem sensor de GPU acessível.") },
            Cancelado = cancelado,
        };
    }

    [Fact]
    public void Laudo_json_respeita_o_esquema_versionado()
    {
        var laudo = LaudoBuilder.A_partirDiagnostico(DiagnosticoDeTeste(), Tecnico, "OS-1234", "0.5.0", mascararSeriais: true, modoDemonstracao: false);
        var json = LaudoJson.Serializar(laudo);

        Assert.Contains("\"versao\": \"flavos-diag/laudo/1\"", json);
        Assert.Contains("\"numeroOs\": \"OS-1234\"", json);
        Assert.Contains("\"vereditoGeral\"", json);
        Assert.Contains("\"flavos", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não comparável", json, StringComparison.OrdinalIgnoreCase);

        var dto = LaudoJson.Ler(json);
        Assert.Equal("flavos-diag/laudo/1", dto.Versao);
        Assert.Equal("OS-1234", dto.NumeroOs);
        Assert.Equal(Tecnico.Nome, dto.Tecnico.Nome);
        Assert.NotEmpty(dto.Categorias);
    }

    [Fact]
    public void Serial_do_equipamento_sai_mascarado_por_padrao()
    {
        var inv = Fixtures.Inventario();
        var serial = inv.PlacaMae.NumeroSerie.Valor;
        var laudo = LaudoBuilder.A_partirDiagnostico(DiagnosticoDeTeste(), Tecnico, "OS-1", "0.5.0", mascararSeriais: true, modoDemonstracao: false);
        var json = LaudoJson.Serializar(laudo);

        Assert.True(laudo.SeriaisMascarados);
        if (!string.IsNullOrWhiteSpace(serial) && serial!.Length >= 8)
        {
            Assert.DoesNotContain(serial, json);
        }

        Assert.Contains(Mascaramento.Mascara, json);
    }

    [Fact]
    public void Laudo_registra_modo_demonstracao()
    {
        var normal = LaudoJson.Serializar(LaudoBuilder.A_partirDiagnostico(DiagnosticoDeTeste(), Tecnico, "OS-1", "0.5.0", true, false));
        var demo = LaudoJson.Serializar(LaudoBuilder.A_partirDiagnostico(DiagnosticoDeTeste(), Tecnico, "OS-1", "0.5.0", true, true));

        Assert.DoesNotContain("modoDemonstracao", normal);
        Assert.Contains("\"modoDemonstracao\": true", demo);
    }

    [Fact]
    public void Aviso_de_pontuacao_esta_sempre_presente_no_json()
    {
        var json = LaudoJson.Serializar(LaudoBuilder.A_partirDiagnostico(DiagnosticoDeTeste(), Tecnico, "OS-1", "0.5.0", true, false));
        Assert.Contains(Laudo.AvisoPontuacao[..20], json);
    }

    [Fact]
    public void Datas_saem_em_iso_8601_com_offset()
    {
        var laudo = LaudoBuilder.A_partirDiagnostico(DiagnosticoDeTeste(), Tecnico, "OS-1", "0.5.0", true, false);
        var json = LaudoJson.Serializar(laudo);
        var inicio = json.IndexOf("\"geradoEm\": \"", StringComparison.Ordinal) + 13;
        var fim = json.IndexOf('"', inicio);
        var data = json[inicio..fim];

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$", data);
    }

    [Fact]
    public void Laudo_com_dados_ausentes_nao_inventa_valores()
    {
        var diagnostico = DiagnosticoDeTeste() with { Inventario = null, Discos = Array.Empty<DiscoInfo>(), Bateria = null, Rede = null };
        var categorias = new[]
        {
            new ResultadoCategoria { Categoria = Regras.CategoriaSistema, Veredito = Veredito.Inconclusivo, Resumo = "Inventário não coletado." },
        };
        diagnostico = diagnostico with { Categorias = categorias, Agregacao = VereditoConsolidador.Agregar(categorias) };

        var laudo = LaudoBuilder.A_partirDiagnostico(diagnostico, Tecnico, "OS-2", "0.5.0", true, false);
        Assert.Equal(Veredito.Inconclusivo, laudo.VereditoGeral);
        Assert.Equal("Não lido", laudo.Equipamento.Cpu);
        Assert.Equal("Nenhum disco lido", laudo.Equipamento.Discos);
    }

    [Fact]
    public void Pdf_e_gerado_com_assinatura_valida()
    {
        var laudo = LaudoBuilder.A_partirDiagnostico(DiagnosticoDeTeste(), Tecnico, "OS-9", "0.5.0", true, false);
        var pdf = new GeradorLaudoPdf().Gerar(laudo);

        Assert.True(pdf.Length > 2000, "PDF muito pequeno: " + pdf.Length + " bytes");
        var cabecalho = Encoding.ASCII.GetString(pdf, 0, 5);
        Assert.Equal("%PDF-", cabecalho);
    }

    [Fact]
    public void Grafico_do_laudo_vira_svg_com_os_valores_da_serie()
    {
        var series = LaudoBuilder.ConstruirSeries(DiagnosticoDeTeste().AmostrasSensores);
        var serie = Assert.Single(series);
        Assert.Equal(2, serie.Valores.Count);

        var svg = GeradorLaudoPdf.BuildSvg(serie);
        Assert.StartsWith("<svg", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public async Task Laudo_em_arquivo_json_grava_utf8_sem_bom()
    {
        var caminho = Path.Combine(Path.GetTempPath(), "flavos-teste-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var laudo = LaudoBuilder.A_partirDiagnostico(DiagnosticoDeTeste(), Tecnico, "OS-3", "0.5.0", true, false);
            await LaudoJson.GravarAsync(laudo, caminho);

            var bytes = File.ReadAllBytes(caminho);
            Assert.False(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "arquivo com BOM");
            var dto = LaudoJson.Ler(File.ReadAllText(caminho));
            Assert.Equal("OS-3", dto.NumeroOs);
            Assert.True(bytes.Length > 100);
            Assert.True(JsonDocument.Parse(File.ReadAllText(caminho)).RootElement.TryGetProperty("categorias", out _));
        }
        finally
        {
            File.Delete(caminho);
        }
    }
}
