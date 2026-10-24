using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.Core.Modelos;

public enum StatusChecklist
{
    Pendente = 0,
    Ok = 1,
    Falha = 2,
    NaoSeAplica = 3,
}

public sealed record ItemChecklist
{
    public required string Id { get; init; }

    public required string Titulo { get; init; }

    public required string Instrucao { get; init; }

    public required StatusChecklist Status { get; init; }

    public string? Observacao { get; init; }

    public string? CaminhoFoto { get; init; }
}

public sealed record DadosTecnico
{
    public string Nome { get; init; } = string.Empty;

    public string Empresa { get; init; } = string.Empty;

    public string Cnpj { get; init; } = string.Empty;

    public string Contato { get; init; } = string.Empty;
}

public sealed record Evidencia
{
    public required string Titulo { get; init; }

    public required string Valor { get; init; }

    public string? Fonte { get; init; }
}

public sealed record SerieEvidencia
{
    public required string Titulo { get; init; }

    public required string Unidade { get; init; }

    public required IReadOnlyList<double> Valores { get; init; }
}

public sealed record ResultadoCategoria
{
    public required string Categoria { get; init; }

    public required Veredito Veredito { get; init; }

    public required string Resumo { get; init; }
}

public sealed record Recomendacao
{
    public required string Acao { get; init; }

    public required PrioridadeRecomendacao Prioridade { get; init; }

    public string? Justificativa { get; init; }
}

public enum PrioridadeRecomendacao
{
    Informativa = 0,
    Recomendada = 1,
    Urgente = 2,
}

public sealed record Laudo
{
    public const string VersaoEsquema = "flavos-diag/laudo/1";

    public const string AvisoPontuacao = "Pontuação própria do Flavos Diag; não comparável com outros programas.";

    public required string Versao { get; init; } = VersaoEsquema;

    public required string VersaoAplicativo { get; init; }

    public required DateTimeOffset GeradoEm { get; init; }

    public required string NumeroOs { get; init; }

    public required DadosTecnico Tecnico { get; init; }

    public required ResumoEquipamento Equipamento { get; init; }

    public required string Resumo { get; init; }

    public required Veredito VereditoGeral { get; init; }

    public required IReadOnlyList<ResultadoCategoria> Categorias { get; init; }

    public required IReadOnlyList<Evidencia> Evidencias { get; init; }

    public required IReadOnlyList<SerieEvidencia> Grafcos { get; init; }

    public required IReadOnlyList<string> Limitacoes { get; init; }

    public required IReadOnlyList<Recomendacao> Recomendacoes { get; init; }

    public required IReadOnlyList<ItemChecklist> Checklist { get; init; }

    public required string AssinaturaTecnico { get; init; }

    public bool SeriaisMascarados { get; init; } = true;

    public bool ModoDemonstracao { get; init; }
}

public sealed record ResumoEquipamento
{
    public required string Fabricante { get; init; }

    public required string Modelo { get; init; }

    public required string NumeroSerie { get; init; }

    public required string Cpu { get; init; }

    public required string Memoria { get; init; }

    public required string SistemaOperacional { get; init; }

    public required string Discos { get; init; }
}
