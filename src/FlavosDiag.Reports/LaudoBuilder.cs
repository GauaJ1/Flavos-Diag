using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Core.Diagnostics;

namespace FlavosDiag.Reports;

/// <summary>Monta o objeto <see cref="Laudo"/> a partir dos dados coletados. Não inventa valores: usa o que existe.</summary>
public static class LaudoBuilder
{
    public static Laudo A_partirDiagnostico(
        ResultadoDiagnosticoRapido diagnostico,
        DadosTecnico tecnico,
        string numeroOs,
        string versaoAplicativo,
        bool mascararSeriais,
        bool modoDemonstracao,
        IReadOnlyList<ItemChecklist>? checklist = null,
        double limiteTemperaturaCelsius = 90)
    {
        var inv = diagnostico.Inventario;
        var categorias = diagnostico.Categorias.ToList();
        if (checklist is { Count: > 0 })
        {
            var r = Regras.Checklist(checklist);
            categorias.Add(new ResultadoCategoria { Categoria = Regras.CategoriaChecklist, Veredito = r.Veredito, Resumo = r.Resumo });
        }

        var agregacao = VereditoConsolidador.Agregar(categorias);

        var evidencias = new List<Evidencia>();
        var recomendacoes = new List<Recomendacao>();
        var limitacoes = new List<string>();

        if (inv is not null)
        {
            evidencias.Add(new Evidencia
            {
                Titulo = "Processador",
                Valor = inv.Cpu.Nome.Disponivel ? inv.Cpu.Nome.Valor! : FormatacaoBr.NaoDisponivel(inv.Cpu.Nome.Causa),
                Fonte = "Win32_Processor",
            });
            evidencias.Add(new Evidencia
            {
                Titulo = "Memória instalada",
                Valor = inv.Memoria.TotalBytes.Disponivel
                    ? FormatacaoBr.Bytes(inv.Memoria.TotalBytes.Valor)
                    : FormatacaoBr.NaoDisponivel(inv.Memoria.TotalBytes.Causa),
                Fonte = "Win32_PhysicalMemory",
            });
            limitacoes.AddRange(inv.Limitacoes.Select(l => $"{l.Item}: {l.Motivo}"));
        }

        foreach (var d in diagnostico.Discos)
        {
            var r = Regras.DiscoIndividual(d);
            evidencias.Add(new Evidencia { Titulo = "Saúde do disco " + d.Numero, Valor = r.Veredito.Rotulo() + " — " + r.Resumo });
            evidencias.AddRange(r.Evidencias);
            recomendacoes.AddRange(r.Recomendacoes);
        }

        if (diagnostico.Bateria is { } b)
        {
            var r = Regras.Bateria(b);
            if (b.SaudePercentual.Disponivel)
            {
                evidencias.Add(new Evidencia { Titulo = "Saúde da bateria", Valor = FormatacaoBr.Percentual(b.SaudePercentual.Valor, 1) });
            }

            recomendacoes.AddRange(r.Recomendacoes);
        }

        limitacoes.AddRange(diagnostico.Limitacoes.Select(l => $"{l.Item}: {l.Motivo}"));

        var series = ConstruirSeries(diagnostico.AmostrasSensores);

        var equipamento = new ResumoEquipamento
        {
            Fabricante = inv?.PlacaMae.Fabricante.Valor ?? string.Empty,
            Modelo = inv?.PlacaMae.Modelo.Valor ?? string.Empty,
            NumeroSerie = inv?.PlacaMae.NumeroSerie.Valor ?? string.Empty,
            Cpu = inv?.Cpu.Nome.Valor ?? "Não lido",
            Memoria = inv?.Memoria.TotalBytes.Disponivel == true
                ? FormatacaoBr.Gib(inv.Memoria.TotalBytes.Valor / 1024.0 / 1024 / 1024)
                : "Não lido",
            SistemaOperacional = inv?.Sistema.Nome.Disponivel == true
                ? $"{inv.Sistema.Nome.Valor} (build {inv.Sistema.Build})"
                : "Não lido",
            Discos = diagnostico.Discos.Count == 0
                ? "Nenhum disco lido"
                : string.Join("; ", diagnostico.Discos.Select(d => d.Modelo.Valor ?? $"Disco {d.Numero}")),
        };

        return new Laudo
        {
            Versao = Laudo.VersaoEsquema,
            VersaoAplicativo = versaoAplicativo,
            GeradoEm = DateTimeOffset.Now,
            NumeroOs = numeroOs,
            Tecnico = tecnico,
            Equipamento = equipamento,
            Resumo = agregacao.Resumo,
            VereditoGeral = agregacao.Geral,
            Categorias = categorias,
            Evidencias = evidencias,
            Grafcos = series,
            Limitacoes = limitacoes.Distinct().ToList(),
            Recomendacoes = recomendacoes.DistinctBy(r => r.Acao).ToList(),
            Checklist = checklist ?? Array.Empty<ItemChecklist>(),
            AssinaturaTecnico = tecnico.Nome,
            SeriaisMascarados = mascararSeriais,
            ModoDemonstracao = modoDemonstracao,
        };
    }

    public static IReadOnlyList<SerieEvidencia> ConstruirSeries(IReadOnlyList<AmostraSensores> amostras)
    {
        return amostras
            .SelectMany(a => a.Valores)
            .GroupBy(v => v.Id)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.First().Categoria)
            .Take(2)
            .Select(g => new SerieEvidencia
            {
                Titulo = g.First().Rotulo,
                Unidade = g.First().Unidade,
                Valores = g.Select(v => v.Valor).ToList(),
            })
            .ToList();
    }
}
