using System.Text.Json;
using System.Text.Json.Serialization;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.Reports.Json;

/// <summary>
/// Serialização do laudo em JSON versionado (esquema flavos-diag/laudo/1).
/// Regras: camelCase, ISO 8601, nulos omitidos. Números de série mascarados quando <see cref="Laudo.SeriaisMascarados"/>.
/// </summary>
public static class LaudoJson
{
    public static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower),
            new DateTimeOffsetIso(),
        },
    };

    public static string Serializar(Laudo laudo)
    {
        var dto = ParaDto(laudo);
        return JsonSerializer.Serialize(dto, Opcoes);
    }

    public static async Task GravarAsync(Laudo laudo, string caminho, CancellationToken ct = default)
    {
        var pasta = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
        }

        await File.WriteAllTextAsync(caminho, Serializar(laudo), new System.Text.UTF8Encoding(false), ct);
    }

    /// <summary>Lê um laudo do JSON (para reimpressão e testes de esquema). Lança <see cref="JsonException"/> se for inválido.</summary>
    public static LaudoDto Ler(string json) =>
        JsonSerializer.Deserialize<LaudoDto>(json, Opcoes)
        ?? throw new JsonException("JSON vazio.");

    public static LaudoDto ParaDto(Laudo l)
    {
        var mascarar = l.SeriaisMascarados;
        return new LaudoDto
        {
            Versao = l.Versao,
            Aplicativo = "Flavos Diag " + l.VersaoAplicativo,
            GeradoEm = l.GeradoEm,
            NumeroOs = l.NumeroOs,
            Tecnico = new TecnicoDto
            {
                Nome = l.Tecnico.Nome,
                Empresa = l.Tecnico.Empresa,
                Cnpj = l.Tecnico.Cnpj,
                Contato = l.Tecnico.Contato,
            },
            Equipamento = new EquipamentoDto
            {
                Fabricante = l.Equipamento.Fabricante,
                Modelo = l.Equipamento.Modelo,
                NumeroSerie = mascarar ? Mascaramento.Mascarar(l.Equipamento.NumeroSerie) : l.Equipamento.NumeroSerie,
                Cpu = l.Equipamento.Cpu,
                Memoria = l.Equipamento.Memoria,
                SistemaOperacional = l.Equipamento.SistemaOperacional,
                Discos = l.Equipamento.Discos,
            },
            Resumo = l.Resumo,
            VereditoGeral = l.VereditoGeral.Rotulo(),
            Categorias = l.Categorias.Select(c => new CategoriaDto
            {
                Nome = c.Categoria,
                Veredito = c.Veredito.Rotulo(),
                Resumo = c.Resumo,
            }).ToList(),
            Evidencias = l.Evidencias.Select(e => new EvidenciaDto { Titulo = e.Titulo, Valor = e.Valor, Fonte = e.Fonte }).ToList(),
            Graficos = l.Grafcos.Select(g => new GraficoDto
            {
                Titulo = g.Titulo,
                Unidade = g.Unidade,
                Valores = g.Valores.Select(v => Math.Round((double)v, 2)).ToList(),
            }).ToList(),
            Limitacoes = l.Limitacoes.ToList(),
            Recomendacoes = l.Recomendacoes.Select(r => new RecomendacaoDto
            {
                Acao = r.Acao,
                Prioridade = r.Prioridade switch
                {
                    PrioridadeRecomendacao.Urgente => "urgente",
                    PrioridadeRecomendacao.Recomendada => "recomendada",
                    _ => "informativa",
                },
                Justificativa = r.Justificativa,
            }).ToList(),
            Checklist = l.Checklist.Select(i => new ChecklistDto
            {
                Id = i.Id,
                Titulo = i.Titulo,
                Status = i.Status switch
                {
                    StatusChecklist.Ok => "ok",
                    StatusChecklist.Falha => "falha",
                    StatusChecklist.NaoSeAplica => "nao-se-aplica",
                    _ => "pendente",
                },
                Observacao = i.Observacao,
                Foto = i.CaminhoFoto is null ? null : Path.GetFileName(i.CaminhoFoto),
            }).ToList(),
            Assinatura = new AssinaturaDto { Tecnico = l.AssinaturaTecnico },
            SeriaisMascarados = l.SeriaisMascarados,
            AvisoPontuacao = Laudo.AvisoPontuacao,
            ModoDemonstracao = l.ModoDemonstracao ? true : null,
        };
    }

    private sealed class DateTimeOffsetIso : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(FormatacaoBr.Iso(value));
    }
}

// ---- DTOs (esquema público do laudo) ----
public sealed class LaudoDto
{
    public string Versao { get; set; } = string.Empty;
    public string Aplicativo { get; set; } = string.Empty;
    public DateTimeOffset GeradoEm { get; set; }
    public string NumeroOs { get; set; } = string.Empty;
    public TecnicoDto Tecnico { get; set; } = new();
    public EquipamentoDto Equipamento { get; set; } = new();
    public string Resumo { get; set; } = string.Empty;
    public string VereditoGeral { get; set; } = string.Empty;
    public List<CategoriaDto> Categorias { get; set; } = new();
    public List<EvidenciaDto> Evidencias { get; set; } = new();
    public List<GraficoDto> Graficos { get; set; } = new();
    public List<string> Limitacoes { get; set; } = new();
    public List<RecomendacaoDto> Recomendacoes { get; set; } = new();
    public List<ChecklistDto> Checklist { get; set; } = new();
    public AssinaturaDto Assinatura { get; set; } = new();
    public bool SeriaisMascarados { get; set; }
    public string? AvisoPontuacao { get; set; }
    public bool? ModoDemonstracao { get; set; }
}

public sealed class TecnicoDto
{
    public string Nome { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Contato { get; set; } = string.Empty;
}

public sealed class EquipamentoDto
{
    public string Fabricante { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string NumeroSerie { get; set; } = string.Empty;
    public string Cpu { get; set; } = string.Empty;
    public string Memoria { get; set; } = string.Empty;
    public string SistemaOperacional { get; set; } = string.Empty;
    public string Discos { get; set; } = string.Empty;
}

public sealed class CategoriaDto
{
    public string Nome { get; set; } = string.Empty;
    public string Veredito { get; set; } = string.Empty;
    public string Resumo { get; set; } = string.Empty;
}

public sealed class EvidenciaDto
{
    public string Titulo { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string? Fonte { get; set; }
}

public sealed class GraficoDto
{
    public string Titulo { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public List<double> Valores { get; set; } = new();
}

public sealed class RecomendacaoDto
{
    public string Acao { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;
    public string? Justificativa { get; set; }
}

public sealed class ChecklistDto
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public string? Foto { get; set; }
}

public sealed class AssinaturaDto
{
    public string Tecnico { get; set; } = string.Empty;
    public string? Hash { get; set; }
}
