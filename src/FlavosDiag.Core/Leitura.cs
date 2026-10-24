namespace FlavosDiag.Core;

/// <summary>Estado de uma leitura de hardware. Regra B3: ausência nunca vira "OK".</summary>
public enum EstadoLeitura
{
    /// <summary>Ainda não coletado. Proibido em laudo.</summary>
    NaoLido = 0,
    /// <summary>Valor coletado com sucesso.</summary>
    Disponivel = 1,
    /// <summary>O hardware/máquina simplesmente não expõe o dado.</summary>
    NaoSuportado = 2,
    /// <summary>O dado existe, mas exige elevação (administrador) ou driver.</summary>
    SemPermissao = 3,
    /// <summary>O coletor falhou (exceção, timeout, resposta inválida). <see cref="Leitura{T}.Causa"/> diz o motivo.</summary>
    Erro = 4,
    /// <summary>O coletor excedeu o tempo limite.</summary>
    Timeout = 5,
}

/// <summary>
/// Um valor lido do hardware com estado explícito e causa de ausência.
/// Não existe "valor padrão implícito": quem consome decide o que fazer com a ausência.
/// </summary>
public sealed record Leitura<T>
{
    private Leitura(EstadoLeitura estado, T? valor, string? causa, string? fonte)
    {
        Estado = estado;
        Valor = valor;
        Causa = causa;
        Fonte = fonte;
    }

    public EstadoLeitura Estado { get; }

    public T? Valor { get; }

    /// <summary>Explicação em pt-BR da ausência. Nulo quando disponível.</summary>
    public string? Causa { get; }

    /// <summary>Origem técnica do dado (ex.: "WMI Win32_Processor"). Usada nas limitações do laudo.</summary>
    public string? Fonte { get; }

    public bool Disponivel => Estado == EstadoLeitura.Disponivel;

    public bool Ausente => !Disponivel;

    public T ValorOu(T fallback) => Disponivel && Valor is not null ? Valor : fallback;

    public static Leitura<T> Ok(T valor, string? fonte = null) =>
        new(EstadoLeitura.Disponivel, valor, null, fonte);

    public static Leitura<T> NaoSuportado(string causa, string? fonte = null) =>
        new(EstadoLeitura.NaoSuportado, default, causa, fonte);

    public static Leitura<T> SemPermissao(string causa, string? fonte = null) =>
        new(EstadoLeitura.SemPermissao, default, causa, fonte);

    public static Leitura<T> Falha(string causa, string? fonte = null) =>
        new(EstadoLeitura.Erro, default, causa, fonte);

    public static Leitura<T> TempoEsgotado(string causa, string? fonte = null) =>
        new(EstadoLeitura.Timeout, default, causa, fonte);

    public static Leitura<T> NaoLido(string? causa = null) =>
        new(EstadoLeitura.NaoLido, default, causa ?? "Não coletado", null);

    /// <summary>Converte outro tipo preservando o estado.</summary>
    public Leitura<TOut> Map<TOut>(Func<T, TOut> conversor) =>
        Disponivel && Valor is not null
            ? Leitura<TOut>.Ok(conversor(Valor), Fonte)
            : new Leitura<TOut>(Estado, default, Causa, Fonte);

    public override string ToString() =>
        Disponivel ? $"{Valor}" : $"[{Estado}] {Causa}";

    /// <summary>Atalho: leitura disponível a partir de um valor.</summary>
    public static implicit operator Leitura<T>(T valor) => Ok(valor);
}

/// <summary>Lista agregada de dados ausentes — vira "Limitações" e "o que não deu para medir".</summary>
public sealed record DadoAusente(string Item, string Motivo, string? Fonte = null);

/// <summary>Coleta um conjunto de limitações durante a coleta.</summary>
public sealed class Limitacoes
{
    private readonly List<DadoAusente> _itens = new();

    public IReadOnlyList<DadoAusente> Itens => _itens;

    public Limitacoes Adicionar(string item, string motivo, string? fonte = null)
    {
        _itens.Add(new DadoAusente(item, motivo, fonte));
        return this;
    }

    public Limitacoes AdicionarSeAusente<T>(string item, Leitura<T> leitura)
    {
        if (leitura.Ausente)
        {
            _itens.Add(new DadoAusente(item, leitura.Causa ?? "Não disponível", leitura.Fonte));
        }

        return this;
    }
}
