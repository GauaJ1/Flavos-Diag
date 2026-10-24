namespace FlavosDiag.Core.Modelos;

public enum PerfilCarga
{
    Leve = 0,
    Medio = 1,
    Pesado = 2,
}

public enum MotivoParada
{
    ConcluidoPorTempo = 0,
    Temperatura = 1,
    CanceladoPeloUsuario = 2,
    SemSensorTemperaturaLimite2Min = 3,
    Erro = 4,
}

public sealed record ConfiguracaoEstresse
{
    public required PerfilCarga Perfil { get; init; }

    public required TimeSpan DuracaoMaxima { get; init; }

    /// <summary>Limite de temperatura em °C. Se nulo, usa o limite padrão da categoria.</summary>
    public double? LimiteTemperaturaCelsius { get; init; }

    public double? LimiteTemperaturaGpuCelsius { get; init; }
}

public sealed record ResultadoEstresse
{
    public required DateTimeOffset IniciadoEm { get; init; }

    public required TimeSpan DuracaoReal { get; init; }

    public required TimeSpan DuracaoSolicitada { get; init; }

    public required PerfilCarga Perfil { get; init; }

    public required MotivoParada MotivoParada { get; init; }

    public required string MotivoParadaTexto { get; init; }

    public required int Workers { get; init; }

    public required long CiclosTotais { get; init; }

    public required int ErrosDeCalculo { get; init; }

    public required Leitura<double> TemperaturaMaximaCelsius { get; init; }

    public required Leitura<double> TemperaturaMediaCelsius { get; init; }

    public required Leitura<double> ClockSustentadoMhz { get; init; }

    public required Leitura<bool> ThrottlingDetectado { get; init; }

    public double? LimiteTemperaturaCelsius { get; init; }

    public string? AvisoSemSensor { get; init; }
}

public sealed record ResultadoBenchmarkCpu
{
    public required DateTimeOffset ExecutadoEm { get; init; }

    public required int VersaoAlgoritmo { get; init; }

    public required double PontuacaoSingle { get; init; }

    public required double PontuacaoMulti { get; init; }

    public required IReadOnlyList<double> AmostrasSingle { get; init; }

    public required IReadOnlyList<double> AmostrasMulti { get; init; }

    public required int ThreadsMulti { get; init; }

    public required string TextoAviso { get; init; }
}

public sealed record ResultadoGpu
{
    public required DateTimeOffset ExecutadoEm { get; init; }

    public required string Adaptador { get; init; }

    public required ResultadoEstresse? Estresse { get; init; }

    public required ResultadoBenchmarkGpu? Benchmark { get; init; }
}

public sealed record ResultadoBenchmarkGpu
{
    public required DateTimeOffset ExecutadoEm { get; init; }

    public required int VersaoAlgoritmo { get; init; }

    public required double Pontuacao { get; init; }

    public required double FpsMedia { get; init; }

    public required IReadOnlyList<double> Amostras { get; init; }

    public required long PixelsDivergentes { get; init; }

    public required string TextoAviso { get; init; }
}

public sealed record ResultadoTesteMemoria
{
    public required DateTimeOffset ExecutadoEm { get; init; }

    public required long BytesTestados { get; init; }

    public required IReadOnlyList<string> Padroes { get; init; }

    public required long ErrosDetectados { get; init; }

    public required TimeSpan Duracao { get; init; }

    public required bool Cancelado { get; init; }

    public required string AvisoTexto { get; init; }
}
