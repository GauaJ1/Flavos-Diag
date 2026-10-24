using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlavosDiag.Core.Benchmark;

public sealed record RegistroHistorico
{
    public required string ChaveMaquina { get; init; }

    public required string Descricao { get; init; }

    public required DateTimeOffset Quando { get; init; }

    public required int VersaoAlgoritmo { get; init; }

    public required double? PontuacaoSingle { get; init; }

    public required double? PontuacaoMulti { get; init; }

    public required double? PontuacaoGpu { get; init; }

    public string? Observacao { get; init; }
}

public sealed record ComparacaoHistorico
{
    public required RegistroHistorico? Antes { get; init; }

    public required RegistroHistorico Depois { get; init; }

    public required double? VariacaoSinglePercentual { get; init; }

    public required double? VariacaoMultiPercentual { get; init; }

    public required double? VariacaoGpuPercentual { get; init; }

    public required string Classificacao { get; init; }
}

/// <summary>
/// Histórico de pontuações por máquina. Sem rede; JSON local.
/// Comparação antes/depois só entre a mesma versão de algoritmo (decisão D19).
/// </summary>
public sealed class HistoricoBenchmark
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<RegistroHistorico> _registros;
    private readonly string? _caminho;

    public HistoricoBenchmark(IEnumerable<RegistroHistorico>? registros = null, string? caminhoArquivo = null)
    {
        _registros = registros?.ToList() ?? new List<RegistroHistorico>();
        _caminho = caminhoArquivo;
    }

    public IReadOnlyList<RegistroHistorico> Registros => _registros;

    public static HistoricoBenchmark Carregar(string caminhoArquivo)
    {
        if (!File.Exists(caminhoArquivo))
        {
            return new HistoricoBenchmark(caminhoArquivo: caminhoArquivo);
        }

        try
        {
            var lista = JsonSerializer.Deserialize<List<RegistroHistorico>>(File.ReadAllText(caminhoArquivo), Opcoes);
            return new HistoricoBenchmark(lista, caminhoArquivo);
        }
        catch (JsonException)
        {
            // Arquivo corrompido não pode derrubar o app; começa vazio preservando o caminho.
            return new HistoricoBenchmark(caminhoArquivo: caminhoArquivo);
        }
    }

    public void Adicionar(RegistroHistorico registro)
    {
        _registros.Add(registro);
        Salvar();
    }

    public void Salvar()
    {
        if (string.IsNullOrWhiteSpace(_caminho))
        {
            return;
        }

        var pasta = Path.GetDirectoryName(_caminho);
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
        }

        var temp = _caminho + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_registros, Opcoes));
        File.Move(temp, _caminho, overwrite: true);
    }

    public IReadOnlyList<RegistroHistorico> PorMaquina(string chaveMaquina) =>
        _registros.Where(r => r.ChaveMaquina == chaveMaquina).OrderByDescending(r => r.Quando).ToList();

    /// <summary>Compara o novo registro com o último anterior da mesma máquina e mesma versão.</summary>
    public ComparacaoHistorico CompararComAnterior(string chaveMaquina, RegistroHistorico novo)
    {
        var anterior = _registros
            .Where(r => r.ChaveMaquina == chaveMaquina && r.VersaoAlgoritmo == novo.VersaoAlgoritmo && r.Quando < novo.Quando)
            .OrderByDescending(r => r.Quando)
            .FirstOrDefault();

        var varS = anterior?.PontuacaoSingle is double a && novo.PontuacaoSingle is double b
            ? PontuacaoCpu.VariacaoPercentual(a, b)
            : null;
        var varM = anterior?.PontuacaoMulti is double c && novo.PontuacaoMulti is double d
            ? PontuacaoCpu.VariacaoPercentual(c, d)
            : null;
        var varG = anterior?.PontuacaoGpu is double e && novo.PontuacaoGpu is double f
            ? PontuacaoCpu.VariacaoPercentual(e, f)
            : null;

        return new ComparacaoHistorico
        {
            Antes = anterior,
            Depois = novo,
            VariacaoSinglePercentual = varS,
            VariacaoMultiPercentual = varM,
            VariacaoGpuPercentual = varG,
            Classificacao = PontuacaoCpu.ClassificarVariacao(varM ?? varS),
        };
    }
}
