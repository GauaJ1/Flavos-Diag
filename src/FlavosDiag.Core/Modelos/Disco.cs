namespace FlavosDiag.Core.Modelos;

public enum TipoMidia
{
    Desconhecido = 0,
    Hdd = 1,
    Ssd = 2,
}

public enum FonteSaudeDisco
{
    Nenhuma = 0,
    SmartAta = 1,
    Nvme = 2,
    ConfiabilidadeWindows = 3,
    EstadoWindows = 4,
}

/// <summary>Atributo SMART (ATA), valores padronizados pela especificação pública.</summary>
public sealed record AtributoSmart
{
    public required int Id { get; init; }

    public required string Nome { get; init; }

    public required int Valor { get; init; }

    public required int Pior { get; init; }

    public required int Limite { get; init; }

    public required long Bruto { get; init; }

    /// <summary>Interpretação em linguagem simples.</summary>
    public required string Interpretacao { get; init; }
}

/// <summary>Log de saúde NVMe (subconjunto interpretado).</summary>
public sealed record SaudeNvme
{
    public required Leitura<int> AvisoCritico { get; init; }

    public required Leitura<int> TemperaturaCelsius { get; init; }

    public required Leitura<int> EspacoDisponivelPercentual { get; init; }

    public required Leitura<int> LimiarEspacoDisponivel { get; init; }

    public required Leitura<double> PercentualVidaUsada { get; init; }

    public required Leitura<long> DadosEscritosGb { get; init; }

    public required Leitura<long> DadosLidosGb { get; init; }

    public required Leitura<long> HorasLigado { get; init; }

    public required Leitura<long> CiclosLigadoDesligado { get; init; }

    public required Leitura<long> ErrosIntegridadeMidia { get; init; }
}

/// <summary>Contadores de confiabilidade do Windows (fallback).</summary>
public sealed record ConfiabilidadeWindows
{
    public required Leitura<long> ErrosLeituraNaoCorrigidos { get; init; }

    public required Leitura<int> DesgastePercentual { get; init; }

    public required Leitura<int> TemperaturaCelsius { get; init; }

    public required Leitura<long> HorasLigado { get; init; }
}

public sealed record SaudeDisco
{
    public required IReadOnlyList<FonteSaudeDisco> Fontes { get; init; }

    public required IReadOnlyList<AtributoSmart> AtributosAta { get; init; }

    public required SaudeNvme? Nvme { get; init; }

    public required ConfiabilidadeWindows? Confiabilidade { get; init; }

    /// <summary>
    /// Previsão de falha do Windows (IOCTL_STORAGE_PREDICT_FAILURE), usada quando o pass-through ATA
    /// não está disponível. <c>null</c> = o dispositivo não respondeu a essa consulta.
    /// </summary>
    public bool? PrevisaoFalhaWindows { get; init; }

    /// <summary>
    /// Estado de saúde declarado pela API de armazenamento do Windows (MSFT_PhysicalDisk.HealthStatus).
    /// <c>true</c> = saudável, <c>false</c> = aviso ou não saudável, <c>null</c> = não informado.
    /// </summary>
    public bool? SaudavelSegundoWindows { get; init; }

    public required IReadOnlyList<DadoAusente> Limitacoes { get; init; }
}

public sealed record DiscoInfo
{
    public required int Numero { get; init; }

    public required Leitura<string> Modelo { get; init; }

    public required Leitura<string> Firmware { get; init; }

    public required Leitura<string> Barramento { get; init; }

    public required Leitura<TipoMidia> Midia { get; init; }

    public required Leitura<long> TamanhoBytes { get; init; }

    public required Leitura<string> NumeroSerie { get; init; }

    public required SaudeDisco Saude { get; init; }
}

public sealed record ResultadoBenchmarkDisco
{
    public required DateTimeOffset ExecutadoEm { get; init; }

    public required string Pasta { get; init; }

    public required long TamanhoArquivoBytes { get; init; }

    public required double SeqLeituraMbps { get; init; }

    public required double SeqEscritaMbps { get; init; }

    public required double Aleatorio4kLeituraIops { get; init; }

    public required double LatenciaMediaMs { get; init; }

    public required double LatenciaP95Ms { get; init; }
}

public enum TipoBlocoSuperficie
{
    Ok = 0,
    Lento = 1,
    Erro = 2,
    NaoLido = 3,
}

public sealed record BlocoSuperficie
{
    public required long Offset { get; init; }

    public required int TamanhoBytes { get; init; }

    public required double TempoMs { get; init; }

    public required TipoBlocoSuperficie Tipo { get; init; }

    public string? Erro { get; init; }
}

public sealed record ResultadoVarreduraSuperficie
{
    public required DateTimeOffset ExecutadoEm { get; init; }

    public required string CaminhoArquivo { get; init; }

    public required long TotalBytes { get; init; }

    public required int BlocosLidos { get; init; }

    public required int BlocosLentos { get; init; }

    public required int BlocosComErro { get; init; }

    public required double TempoTotalSegundos { get; init; }

    public required bool Cancelada { get; init; }

    public required IReadOnlyList<BlocoSuperficie> Blocos { get; init; }
}

public sealed record ResultadoVerificacaoSistemaArquivos
{
    public required DateTimeOffset ExecutadoEm { get; init; }

    public required string Volume { get; init; }

    public required Leitura<int> CodigoSaida { get; init; }

    public required bool CanceladaOuInterrompida { get; init; }

    public required IReadOnlyList<string> MensagensRelevantes { get; init; }

    public required string TextoResumo { get; init; }
}
