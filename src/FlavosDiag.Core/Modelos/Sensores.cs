namespace FlavosDiag.Core.Modelos;

public enum CategoriaSensor
{
    Temperatura = 0,
    Clock = 1,
    Carga = 2,
    Potencia = 3,
    Tensao = 4,
    Ventoinha = 5,
    Outros = 6,
}

public sealed record ValorSensor
{
    public required string Id { get; init; }

    public required CategoriaSensor Categoria { get; init; }

    public required string Rotulo { get; init; }

    public required double Valor { get; init; }

    public required string Unidade { get; init; }
}

public sealed record AmostraSensores
{
    public required DateTimeOffset Quando { get; init; }

    public required IReadOnlyList<ValorSensor> Valores { get; init; }

    /// <summary>Por que não houve leitura (quando a lista está vazia).</summary>
    public string? CausaAusencia { get; init; }
}

public sealed record ResumoSensor
{
    public required string Id { get; init; }

    public required string Rotulo { get; init; }

    public required CategoriaSensor Categoria { get; init; }

    public required string Unidade { get; init; }

    public required double Minimo { get; init; }

    public required double Maximo { get; init; }

    public required double Media { get; init; }

    public required int Amostras { get; init; }
}
