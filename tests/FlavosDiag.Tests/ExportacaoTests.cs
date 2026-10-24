using FlavosDiag.App.Services;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Reports;
using FlavosDiag.Reports.Json;

namespace FlavosDiag.Tests;

/// <summary>
/// Exportação do laudo: os dois arquivos precisam existir, o JSON precisa ser relido pelo mesmo esquema
/// e nomes de série não podem vazar quando o mascaramento está ligado.
/// </summary>
public class ExportacaoTests
{
    private static readonly DadosTecnico Tecnico = new()
    {
        Nome = "Técnico de Teste",
        Empresa = "Assistência de Teste",
        Cnpj = "00.000.000/0001-00",
        Contato = "(00) 0000-0000",
    };

    private static Laudo LaudoDeTeste(bool mascarar)
    {
        var categorias = new[]
        {
            new ResultadoCategoria { Categoria = "Sistema", Veredito = Veredito.Ok, Resumo = "Inventário lido." },
            new ResultadoCategoria { Categoria = "Disco", Veredito = Veredito.Atencao, Resumo = "Setores realocados acima de zero." },
        };

        var diagnostico = new Core.Diagnostics.ResultadoDiagnosticoRapido
        {
            IniciadoEm = DateTimeOffset.Now,
            Duracao = TimeSpan.FromMinutes(1),
            Inventario = Providers.Fakes.Fixtures.Inventario(),
            Discos = Providers.Fakes.Fixtures.Discos(),
            Bateria = Providers.Fakes.Fixtures.Bateria(),
            UltimaAmostra = null,
            AmostrasSensores = Array.Empty<AmostraSensores>(),
            Rede = Providers.Fakes.Fixtures.Rede(),
            Etapas = Array.Empty<Core.Diagnostics.EtapaDiagnostico>(),
            Categorias = categorias,
            Agregacao = VereditoConsolidador.Agregar(categorias),
            Limitacoes = new[] { new DadoAusente("Temperatura da GPU", "Sem sensor exposto por este equipamento.") },
            Cancelado = false,
        };

        return LaudoBuilder.A_partirDiagnostico(diagnostico, Tecnico, "OS 4711/2026", "0.5.0", mascarar, false);
    }

    private static string NovaPasta()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "flavos-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    [Fact]
    public async Task Exportacao_grava_json_e_pdf_na_pasta_escolhida()
    {
        var pasta = NovaPasta();
        try
        {
            var laudo = LaudoDeTeste(mascarar: true);
            var resultado = await new ExportadorLaudo().ExportarAsync(laudo, pasta);

            Assert.True(File.Exists(resultado.PastaJson), "JSON não gravado: " + resultado.PastaJson);
            Assert.True(File.Exists(resultado.ArquivoPdf), "PDF não gravado: " + resultado.ArquivoPdf);
            Assert.Equal(pasta, Path.GetDirectoryName(resultado.PastaJson));
            Assert.Equal(pasta, Path.GetDirectoryName(resultado.ArquivoPdf));
            Assert.True(new FileInfo(resultado.ArquivoPdf).Length > 1000, "PDF pequeno demais");

            var dto = LaudoJson.Ler(File.ReadAllText(resultado.PastaJson));
            Assert.Equal("OS 4711/2026", dto.NumeroOs);
            Assert.True(dto.SeriaisMascarados);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Exportacao_nao_sobrescreve_laudo_anterior()
    {
        var pasta = NovaPasta();
        try
        {
            var laudo = LaudoDeTeste(mascarar: true);
            var exportador = new ExportadorLaudo();
            var primeiro = await exportador.ExportarAsync(laudo, pasta);
            var segundo = await exportador.ExportarAsync(laudo, pasta);

            Assert.NotEqual(primeiro.NomeBase, segundo.NomeBase);
            Assert.True(File.Exists(primeiro.ArquivoPdf));
            Assert.True(File.Exists(segundo.ArquivoPdf));
            Assert.Equal(2, Directory.GetFiles(pasta, "*.pdf").Length);
            Assert.Equal(2, Directory.GetFiles(pasta, "*.json").Length);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Exportacao_com_mascaramento_nao_escreve_serial_completo()
    {
        var pasta = NovaPasta();
        try
        {
            var laudo = LaudoDeTeste(mascarar: true);
            var serial = laudo.Equipamento.NumeroSerie;
            Assert.False(string.IsNullOrWhiteSpace(serial), "fixture sem número de série: o teste perderia o sentido");

            var resultado = await new ExportadorLaudo().ExportarAsync(laudo, pasta);
            var json = File.ReadAllText(resultado.PastaJson);
            Assert.DoesNotContain(serial, json);

            // O PDF é binário comprimido; o teste garante que ele existe e tem tamanho plausível.
            var pdf = File.ReadAllBytes(resultado.ArquivoPdf);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
            Assert.True(pdf.Length > 1000);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Exportacao_sem_pasta_falha_com_mensagem_clara()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new ExportadorLaudo().ExportarAsync(LaudoDeTeste(true), string.Empty));
    }

    [Fact]
    public void Nome_do_arquivo_nao_usa_caracteres_invalidos()
    {
        var nome = ExportadorLaudo.NomeArquivoSeguro(LaudoDeTeste(true));
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            Assert.DoesNotContain(c, nome);
        }

        Assert.StartsWith("laudo-", nome);
        Assert.True(nome.Length <= 96);
    }
}
