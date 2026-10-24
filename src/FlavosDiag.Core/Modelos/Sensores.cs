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

/// <summary>
/// Que tipo de componente expôs o sensor. Serve para escolher a temperatura certa por componente
/// em vez de adivinhar pelo nome: uma GPU também tem um sensor chamado "Core", e escolher por texto
/// fazia a temperatura da placa de vídeo ser lida como se fosse do processador.
/// </summary>
public enum TipoHardware
{
    Outro = 0,
    Cpu = 1,
    Gpu = 2,
    Placa = 3,
    SuperIo = 4,
    Memoria = 5,
    Armazenamento = 6,
}

/// <summary>
/// Quão confiável é o valor de um sensor.
/// </summary>
/// <remarks>
/// <see cref="Ok"/> é o único estado que pode alimentar um veredito ou uma parada de segurança.
/// <see cref="SemLeitura"/> significa que o sensor existe e foi reconhecido, mas não entregou um
/// número utilizável — é o caso do <c>Core (Tctl/Tdie)</c> que devolve 0 quando o driver de leitura
/// não está disponível. Sem esse estado, um 0 atravessaria o catálogo e o laudo como se fosse uma
/// medição de 0 °C.
/// </remarks>
public enum QualidadeSensor
{
    Ok = 0,
    SemLeitura = 1,
    Suspeito = 2,
}

public sealed record ValorSensor
{
    public required string Id { get; init; }

    public required CategoriaSensor Categoria { get; init; }

    public required string Rotulo { get; init; }

    public required double Valor { get; init; }

    public required string Unidade { get; init; }

    /// <summary>Tipo do componente que expôs o sensor. Padrão <see cref="TipoHardware.Outro"/> mantém compatibilidade.</summary>
    public TipoHardware TipoHardware { get; init; } = TipoHardware.Outro;

    /// <summary>Nome do componente, ex.: "AMD Ryzen 5 8500G".</summary>
    public string? NomeHardware { get; init; }

    /// <summary>Nome do sensor dentro do componente, ex.: "Core (Tctl/Tdie)".</summary>
    public string? NomeSensor { get; init; }

    /// <summary>Confiança do valor. Padrão <see cref="QualidadeSensor.Ok"/> mantém compatibilidade.</summary>
    public QualidadeSensor Qualidade { get; init; } = QualidadeSensor.Ok;
}

/// <summary>
/// Sensor que o coletor reconheceu mas não entregou valor utilizável. Fica separado dos valores
/// para que a interface e o laudo possam dizer "existe, mas não foi possível ler" em vez de omitir
/// o sensor e deixar o técnico pensar que o equipamento não tem o sensor.
/// </summary>
public sealed record SensorSemLeitura
{
    public required string Id { get; init; }

    public required CategoriaSensor Categoria { get; init; }

    public required TipoHardware TipoHardware { get; init; }

    public required string NomeHardware { get; init; }

    public required string NomeSensor { get; init; }

    public string? Unidade { get; init; }

    /// <summary>Valor bruto devolvido pela fonte (pode ser sentinela, como 0 ou 255).</summary>
    public double? ValorBruto { get; init; }

    public double? Minimo { get; init; }

    public double? Maximo { get; init; }

    /// <summary>Por que não há leitura, em pt-BR.</summary>
    public required string Motivo { get; init; }
}

public sealed record AmostraSensores
{
    public required DateTimeOffset Quando { get; init; }

    public required IReadOnlyList<ValorSensor> Valores { get; init; }

    /// <summary>Por que não houve leitura (quando a lista está vazia).</summary>
    public string? CausaAusencia { get; init; }

    /// <summary>
    /// Sensores reconhecidos sem leitura. Padrão vazio para não quebrar chamadores antigos.
    /// </summary>
    public IReadOnlyList<SensorSemLeitura> SemLeitura { get; init; } = Array.Empty<SensorSemLeitura>();
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

    /// <summary>Tipo do componente; padrão <see cref="TipoHardware.Outro"/> mantém compatibilidade.</summary>
    public TipoHardware TipoHardware { get; init; } = TipoHardware.Outro;

    /// <summary>Confiança da leitura; padrão <see cref="QualidadeSensor.Ok"/> mantém compatibilidade.</summary>
    public QualidadeSensor Qualidade { get; init; } = QualidadeSensor.Ok;
}
