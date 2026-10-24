namespace FlavosDiag.Core.Modelos;

public sealed record InfoBateriaDetalhe
{
    public required bool Presente { get; init; }

    public required Leitura<string> Nome { get; init; }

    public required Leitura<int> CapacidadeProjetoMwh { get; init; }

    public required Leitura<int> CapacidadeAtualMwh { get; init; }

    public required Leitura<int> Ciclos { get; init; }

    public required Leitura<string> Fabricante { get; init; }

    public required Leitura<string> NumeroSerie { get; init; }

    public required Leitura<string> Estado { get; init; }

    /// <summary>Saúde em % = atual / projeto × 100.</summary>
    public required Leitura<double> SaudePercentual { get; init; }

    public required DateTimeOffset ColetadoEm { get; init; }

    public required IReadOnlyList<DadoAusente> Limitacoes { get; init; }
}

public sealed record ResultadoLatencia
{
    public required string Alvo { get; init; }

    public required bool IcMPRespondido { get; init; }

    public required bool TcpRespondido { get; init; }

    public required int Enviados { get; init; }

    public required int Recebidos { get; init; }

    public required double PerdaPercentual { get; init; }

    public required double LatenciaMediaMs { get; init; }

    public required double LatenciaMinimaMs { get; init; }

    public required double LatenciaMaximaMs { get; init; }
}

public sealed record ResultadoDns
{
    public required string NomeConsultado { get; init; }

    public required bool Resolvido { get; init; }

    public required IReadOnlyList<string> Enderecos { get; init; }

    public required double LatenciaMs { get; init; }

    public string? Erro { get; init; }
}

public sealed record StatusWifi
{
    public required Leitura<string> Ssid { get; init; }

    public required Leitura<int> QualidadePercentual { get; init; }

    public required Leitura<int> ForcaDbm { get; init; }
}

public sealed record ResultadoVelocidadeRede
{
    public required string Servidor { get; init; }

    public required double DownloadMbps { get; init; }

    public required long BytesRecebidos { get; init; }

    public required double Segundos { get; init; }

    public string? Erro { get; init; }
}

public sealed record InfoRede
{
    public required IReadOnlyList<InfoAdaptadorRede> Adaptadores { get; init; }

    public required Leitura<string> EnderecoIp { get; init; }

    public required Leitura<string> Gateway { get; init; }

    public required Leitura<string> ServidoresDns { get; init; }

    public required StatusWifi Wifi { get; init; }

    public required Leitura<ResultadoLatencia> LatenciaGateway { get; init; }

    public required IReadOnlyList<ResultadoLatencia> Alvos { get; init; }

    public required Leitura<ResultadoDns> Dns { get; init; }

    public required DateTimeOffset ColetadoEm { get; init; }

    public required IReadOnlyList<DadoAusente> Limitacoes { get; init; }
}
