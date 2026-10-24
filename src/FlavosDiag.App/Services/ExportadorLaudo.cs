using System.IO;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Reports.Json;
using FlavosDiag.Reports.Pdf;

namespace FlavosDiag.App.Services;

/// <summary>
/// Gera os dois arquivos do laudo (JSON + PDF) numa pasta escolhida pelo técnico.
/// Nunca sobrescreve um laudo anterior: se o nome já existir, acrescenta um sufixo.
/// </summary>
public sealed class ExportadorLaudo
{
    private readonly IGeradorLaudoPdf _pdf;

    public ExportadorLaudo(IGeradorLaudoPdf? pdf = null)
    {
        _pdf = pdf ?? new GeradorLaudoPdf();
    }

    public sealed record ResultadoExportacao(string PastaJson, string ArquivoPdf, string NomeBase);

    /// <summary>Grava o laudo em <paramref name="pasta"/> e devolve os caminhos reais utilizados.</summary>
    public async Task<ResultadoExportacao> ExportarAsync(Laudo laudo, string pasta, CancellationToken cancelamento = default)
    {
        if (string.IsNullOrWhiteSpace(pasta))
        {
            throw new ArgumentException("Escolha uma pasta para salvar o laudo.", nameof(pasta));
        }

        Directory.CreateDirectory(pasta);
        var nome = NomeArquivoSeguro(laudo);
        nome = NomeLivre(pasta, nome);

        var json = Path.Combine(pasta, nome + ".json");
        var pdf = Path.Combine(pasta, nome + ".pdf");

        cancelamento.ThrowIfCancellationRequested();
        await LaudoJson.GravarAsync(laudo, json, cancelamento).ConfigureAwait(false);

        cancelamento.ThrowIfCancellationRequested();
        _pdf.GerarArquivo(laudo, pdf);

        if (!File.Exists(json) || new FileInfo(json).Length == 0)
        {
            throw new InvalidOperationException("O arquivo JSON do laudo não foi gravado.");
        }

        if (!File.Exists(pdf) || new FileInfo(pdf).Length == 0)
        {
            throw new InvalidOperationException("O arquivo PDF do laudo não foi gravado.");
        }

        return new ResultadoExportacao(json, pdf, nome);
    }

    public static string NomeArquivoSeguro(Laudo laudo)
    {
        var os = string.IsNullOrWhiteSpace(laudo.NumeroOs) ? "sem-os" : laudo.NumeroOs;
        var bruto = $"laudo-{os}-{laudo.GeradoEm.LocalDateTime:yyyyMMdd-HHmm}";
        var invalidos = Path.GetInvalidFileNameChars();
        var limpo = new string(bruto.Select(c => invalidos.Contains(c) ? '-' : c).ToArray()).Trim();
        return limpo.Length > 96 ? limpo[..96] : limpo;
    }

    private static string NomeLivre(string pasta, string nome)
    {
        var candidato = nome;
        var n = 2;
        while (File.Exists(Path.Combine(pasta, candidato + ".json")) || File.Exists(Path.Combine(pasta, candidato + ".pdf")))
        {
            candidato = $"{nome}-{n}";
            n++;
            if (n > 99)
            {
                throw new IOException("Já existem muitos laudos com o mesmo nome nesta pasta.");
            }
        }

        return candidato;
    }
}
