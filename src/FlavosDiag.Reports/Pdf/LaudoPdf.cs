using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FlavosDiag.Reports.Pdf;

/// <summary>Fachada de geraÃ§Ã£o do PDF do laudo.</summary>
public interface IGeradorLaudoPdf
{
    byte[] Gerar(Laudo laudo);

    void GerarArquivo(Laudo laudo, string caminho);
}

/// <summary>GeraÃ§Ã£o do PDF (1â€“2 pÃ¡ginas) com QuestPDF â€” identidade visual prÃ³pria do Flavos Diag.</summary>
public sealed class GeradorLaudoPdf : IGeradorLaudoPdf
{
    private static readonly string Petroleo = "#0F766E";
    private static readonly string Tinta = "#0F172A";
    private static readonly string Cinza = "#475569";
    private static readonly string Linha = "#E2E8F0";
    private const string NomeFonte = "Calibri";
    private const string NomeFonteFallback = "Arial";

    public GeradorLaudoPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;
    }

    private static string Cor(Veredito v) => v switch
    {
        Veredito.Ok => "#15803D",
        Veredito.Atencao => "#B45309",
        Veredito.Falha => "#B91C1C",
        Veredito.Inconclusivo => "#475569",
        _ => "#64748B",
    };

    public void GerarArquivo(Laudo laudo, string caminho)
    {
        var pasta = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
        }

        File.WriteAllBytes(caminho, Gerar(laudo));
    }

    public byte[] Gerar(Laudo laudo) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(t => t.FontFamily(NomeFonte).FontSize(9).FontColor(Tinta));

            page.Header().Element(c => Cabecalho(c, laudo));
            page.Content().Element(c => Conteudo(c, laudo));
            page.Footer().Element(c => Rodape(c, laudo));
        });
    }).GeneratePdf();

    private static void Cabecalho(IContainer c, Laudo laudo)
    {
        c.PaddingBottom(8).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(l =>
                {
                    l.Item().Text("FLAVOS DIAG").FontSize(16).Bold().FontColor(Petroleo).LetterSpacing(0.08f);
                    l.Item().Text("Laudo tÃ©cnico de diagnÃ³stico de hardware").FontSize(9).FontColor(Cinza);
                });
                row.ConstantItem(190).AlignRight().Column(r =>
                {
                    r.Item().Text($"OS: {Valor(laudo.NumeroOs)}").Bold();
                    r.Item().Text($"Emitido em {FormatacaoBr.DataHora(laudo.GeradoEm.LocalDateTime)}").FontColor(Cinza);
                    r.Item().Text($"Documento {laudo.Versao}").FontColor(Cinza).FontSize(7.5f);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Linha);
        });
    }

    private static void Conteudo(IContainer c, Laudo laudo)
    {
        c.Column(col =>
        {
            col.Spacing(8);

            // TÃ©cnico + equipamento
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(e => Bloco(e, "TÃ©cnico / empresa", new[]
                {
                    ("Nome", laudo.Tecnico.Nome),
                    ("Empresa", laudo.Tecnico.Empresa),
                    ("CNPJ", laudo.Tecnico.Cnpj),
                    ("Contato", laudo.Tecnico.Contato),
                }));
                row.ConstantItem(10);
                row.RelativeItem().Element(e => Bloco(e, "Equipamento", new[]
                {
                    ("Fabricante", laudo.Equipamento.Fabricante),
                    ("Modelo", laudo.Equipamento.Modelo),
                    ("NÃºmero de sÃ©rie", laudo.Equipamento.NumeroSerie),
                    ("Sistema", laudo.Equipamento.SistemaOperacional),
                }));
            });

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(e => Bloco(e, "Hardware", new[]
                {
                    ("Processador", laudo.Equipamento.Cpu),
                    ("MemÃ³ria", laudo.Equipamento.Memoria),
                    ("Discos", laudo.Equipamento.Discos),
                }));
                row.ConstantItem(10);
                row.RelativeItem().Element(e => ResumoVeredito(e, laudo));
            });

            // Categorias
            col.Item().Element(e => e.Background("#F8FAFC").Border(1).BorderColor(Linha).Padding(8).Column(t =>
            {
                t.Spacing(3);
                t.Item().Text("Veredito por categoria").Bold().FontSize(10);
                t.Item().Table(tab =>
                {
                    tab.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.ConstantColumn(66);
                        cols.RelativeColumn(5);
                    });
                    foreach (var cat in laudo.Categorias)
                    {
                        tab.Cell().PaddingVertical(1.5f).Text(cat.Categoria).SemiBold();
                        tab.Cell().PaddingVertical(1.5f).Text(cat.Veredito.Rotulo()).Bold().FontColor(Cor(cat.Veredito));
                        tab.Cell().PaddingVertical(1.5f).Text(cat.Resumo).FontSize(8);
                    }
                });
            }));

            // EvidÃªncias (atÃ© 12)
            if (laudo.Evidencias.Count > 0)
            {
                col.Item().Element(e => e.Column(t =>
                {
                    t.Item().Text("EvidÃªncias").Bold().FontSize(10);
                    t.Item().PaddingTop(2).Table(tab =>
                    {
                        tab.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(2);
                        });
                        foreach (var ev in laudo.Evidencias.Take(12))
                        {
                            tab.Cell().PaddingVertical(1).Text(ev.Titulo).FontSize(8).FontColor(Cinza);
                            tab.Cell().PaddingVertical(1).Text(ev.Valor).FontSize(8);
                            tab.Cell().PaddingVertical(1).Text(ev.Fonte ?? string.Empty).FontSize(7).FontColor(Cinza);
                        }
                    });
                }));
            }

            // GrÃ¡fico principal
            var serie = laudo.Grafcos.FirstOrDefault();
            if (serie is not null && serie.Valores.Count > 1)
            {
                col.Item().Text($"GrÃ¡fico: {serie.Titulo}").Bold().FontSize(10);
                col.Item().Height(70).Svg(BuildSvg(serie));
            }

            // LimitaÃ§Ãµes
            if (laudo.Limitacoes.Count > 0)
            {
                col.Item().Element(e => Lista(e, "LimitaÃ§Ãµes (o que nÃ£o foi possÃ­vel medir e por quÃª)", laudo.Limitacoes.Take(8)));
            }

            // RecomendaÃ§Ãµes
            if (laudo.Recomendacoes.Count > 0)
            {
                col.Item().Element(e => Lista(
                    e,
                    "RecomendaÃ§Ãµes",
                    laudo.Recomendacoes.Select(r => $"[{r.Prioridade.ToString().ToUpperInvariant()}] {r.Acao}{(r.Justificativa is null ? string.Empty : " â€” " + r.Justificativa)}")));
            }

            // Checklist
            if (laudo.Checklist.Count > 0)
            {
                col.Item().Column(t =>
                {
                    t.Item().Text("Checklist assistido").Bold().FontSize(10);
                    t.Item().PaddingTop(2).Table(tab =>
                    {
                        tab.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.ConstantColumn(62);
                            cols.RelativeColumn(4);
                        });
                        foreach (var item in laudo.Checklist)
                        {
                            var status = item.Status switch
                            {
                                StatusChecklist.Ok => "OK",
                                StatusChecklist.Falha => "Falha",
                                StatusChecklist.NaoSeAplica => "N/A",
                                _ => "Pendente",
                            };
                            tab.Cell().PaddingVertical(1).Text(item.Titulo).FontSize(8);
                            tab.Cell().PaddingVertical(1).Text(status).FontSize(8).SemiBold();
                            tab.Cell().PaddingVertical(1).Text(item.Observacao ?? string.Empty).FontSize(8).FontColor(Cinza);
                        }
                    });
                });
            }

            col.Item().PaddingTop(6).Text(Laudo.AvisoPontuacao).FontSize(7.5f).FontColor(Cinza);
        });
    }

    private static void ResumoVeredito(IContainer e, Laudo laudo)
    {
        e.Border(1).BorderColor(Cor(laudo.VereditoGeral)).Padding(8).Background("#FFFFFF").Column(col =>
        {
            col.Item().Text("Veredito geral").FontColor(Cinza).FontSize(8);
            col.Item().Text(laudo.VereditoGeral.Rotulo()).FontSize(18).Bold().FontColor(Cor(laudo.VereditoGeral));
            col.Item().PaddingTop(4).Text(laudo.Resumo).FontSize(8);
        });
    }

    private static void Bloco(IContainer e, string titulo, IEnumerable<(string Rotulo, string Valor)> linhas)
    {
        e.Border(1).BorderColor(Linha).Padding(8).Column(col =>
        {
            col.Item().Text(titulo).Bold().FontSize(9).FontColor(Petroleo);
            foreach (var (rotulo, valor) in linhas)
            {
                col.Item().Row(r =>
                {
                    r.ConstantItem(74).Text(rotulo).FontColor(Cinza).FontSize(8);
                    r.RelativeItem().Text(Valor(valor)).FontSize(8);
                });
            }
        });
    }

    private static void Lista(IContainer e, string titulo, IEnumerable<string> itens)
    {
        e.Column(col =>
        {
            col.Item().Text(titulo).Bold().FontSize(10);
            foreach (var i in itens)
            {
                col.Item().PaddingLeft(6).Text("â€¢ " + i).FontSize(8).FontColor(Cinza);
            }
        });
    }

    private static void Rodape(IContainer c, Laudo laudo)
    {
        c.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Linha);
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Column(l =>
                {
                    l.Item().Text("Assinatura do tÃ©cnico").FontSize(8).FontColor(Cinza);
                    l.Item().PaddingTop(14).Text(Valor(laudo.AssinaturaTecnico)).FontSize(9);
                    l.Item().LineHorizontal(0.5f).LineColor(Cinza);
                });
                row.ConstantItem(20);
                row.ConstantItem(150).AlignRight().Column(r =>
                {
                    r.Item().Text($"Flavos Diag {laudo.VersaoAplicativo}").FontSize(7).FontColor(Cinza);
                    r.Item().Text(laudo.SeriaisMascarados ? "NÃºmeros de sÃ©rie mascarados" : "NÃºmeros de sÃ©rie completos").FontSize(7).FontColor(Cinza);
                    if (laudo.ModoDemonstracao)
                    {
                        r.Item().Text("MODO DEMONSTRAÃ‡ÃƒO â€” dados fictÃ­cios").FontSize(7).FontColor("#B91C1C").Bold();
                    }
                });
            });
            col.Item().PaddingTop(4).AlignRight().Text(t =>
            {
                t.Span("Página ").FontSize(7).FontColor(Cinza);
                t.CurrentPageNumber().FontSize(7).FontColor(Cinza);
                t.Span(" de ").FontSize(7).FontColor(Cinza);
                t.TotalPages().FontSize(7).FontColor(Cinza);
            });
        });
    }

    /// <summary>SVG prÃ³prio (texto gerado pela aplicaÃ§Ã£o) com a sÃ©rie principal.</summary>
    public static string BuildSvg(SerieEvidencia serie)
    {
        const float w = 520f;
        const float h = 64f;
        var min = (float)serie.Valores.Min();
        var max = (float)serie.Valores.Max();
        if (Math.Abs(max - min) < 0.001f)
        {
            max = min + 1;
        }

        var cult = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.Append(string.Format(cult, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}\" height=\"{1}\" viewBox=\"0 0 {0} {1}\">", w, h));
        sb.Append(string.Format(cult, "<line x1=\"0\" y1=\"{0}\" x2=\"{1}\" y2=\"{0}\" stroke=\"{2}\" stroke-width=\"0.5\"/>", h - 1, w, Linha));
        var passo = w / Math.Max(1, serie.Valores.Count - 1);
        var pontos = new System.Text.StringBuilder();
        for (var i = 0; i < serie.Valores.Count; i++)
        {
            var x = i * passo;
            var y = h - 2 - (((float)serie.Valores[i] - min) / (max - min) * (h - 4));
            pontos.Append(string.Format(cult, "{0:0.##},{1:0.##} ", x, y));
        }

        sb.Append(string.Format(cult, "<polyline fill=\"none\" stroke=\"{0}\" stroke-width=\"1.4\" points=\"{1}\"/>", Petroleo, pontos.ToString().Trim()));
        sb.Append(string.Format(cult, "<text x=\"2\" y=\"10\" font-size=\"8\" fill=\"{0}\">{1}</text>", Cinza, System.Security.SecurityElement.Escape(serie.Unidade)));
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string Valor(string? t) => string.IsNullOrWhiteSpace(t) ? "â€”" : t;
}

