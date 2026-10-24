namespace FlavosDiag.Core.Modelos;

/// <summary>Sistema operacional (leitura informativa).</summary>
public sealed record InfoSistema
{
    public required Leitura<string> Nome { get; init; }

    public required Leitura<string> Versao { get; init; }

    public required Leitura<string> Build { get; init; }

    public required Leitura<string> Arquitetura { get; init; }

    public required Leitura<string> NomeComputador { get; init; }

    public required Leitura<string> Usuario { get; init; }

    public required Leitura<DateTime> DataInstalacao { get; init; }

    /// <summary>Apenas informativo. Nunca altera licença.</summary>
    public required Leitura<string> Ativacao { get; init; }
}

public sealed record InfoBios
{
    public required Leitura<string> Fabricante { get; init; }

    public required Leitura<string> Versao { get; init; }

    public required Leitura<DateTime> Data { get; init; }

    public required Leitura<string> Modo { get; init; }
}

public sealed record InfoPlacaMae
{
    public required Leitura<string> Fabricante { get; init; }

    public required Leitura<string> Modelo { get; init; }

    public required Leitura<string> Versao { get; init; }

    public required Leitura<string> NumeroSerie { get; init; }
}

public sealed record InfoCpu
{
    public required Leitura<string> Nome { get; init; }

    public required Leitura<string> Fabricante { get; init; }

    public required Leitura<int> NucleosFisicos { get; init; }

    public required Leitura<int> ThreadsLogicas { get; init; }

    public required Leitura<double> ClockBaseMhz { get; init; }

    public required Leitura<double> ClockMaximoMhz { get; init; }

    public required Leitura<double> CacheL2Kb { get; init; }

    public required Leitura<double> CacheL3Kb { get; init; }

    public required Leitura<string> Arquitetura { get; init; }
}

public sealed record ModuloMemoria
{
    public required Leitura<string> Local { get; init; }

    public required Leitura<long> CapacidadeBytes { get; init; }

    public required Leitura<string> Tipo { get; init; }

    public required Leitura<int> VelocidadeNominalMhz { get; init; }

    public required Leitura<int> VelocidadeConfiguradaMhz { get; init; }

    public required Leitura<string> Fabricante { get; init; }

    public required Leitura<string> NumeroPeca { get; init; }

    public required Leitura<string> NumeroSerie { get; init; }
}

public sealed record InfoMemoria
{
    public required Leitura<long> TotalBytes { get; init; }

    public required Leitura<int> SlotsUsados { get; init; }

    public required Leitura<int> SlotsTotais { get; init; }

    /// <summary>Timings (CAS/RAS/CL). Sem fonte confiável => ausente ("Não disponível").</summary>
    public required Leitura<string> Timings { get; init; }

    public required IReadOnlyList<ModuloMemoria> Modulos { get; init; }
}

public sealed record InfoGpu
{
    public required Leitura<string> Nome { get; init; }

    /// <summary>VRAM dedicada em bytes (DXGI).</summary>
    public required Leitura<long> VramDedicadaBytes { get; init; }

    public required Leitura<string> VersaoDriver { get; init; }

    public required Leitura<DateTime> DataDriver { get; init; }

    public required Leitura<string> Fabricante { get; init; }
}

public sealed record InfoMonitor
{
    public required Leitura<int> Largura { get; init; }

    public required Leitura<int> Altura { get; init; }

    public required Leitura<int> TaxaAtualizacaoHz { get; init; }

    public required Leitura<string> Nome { get; init; }
}

public sealed record InfoAdaptadorRede
{
    public required Leitura<string> Nome { get; init; }

    public required Leitura<string> Descricao { get; init; }

    public required Leitura<string> Mac { get; init; }

    public required Leitura<long> VelocidadeBitsPorSegundo { get; init; }

    public required Leitura<string> Tipo { get; init; }

    public required bool Ativo { get; init; }
}

public sealed record InfoBateria
{
    public required bool Presente { get; init; }

    public required Leitura<string> Nome { get; init; }

    public required Leitura<string> Fabricante { get; init; }
}

public sealed record DispositivoComProblema
{
    public required string Nome { get; init; }

    public required int CodigoErro { get; init; }

    public required string Descricao { get; init; }

    public string? Fabricante { get; init; }

    public string? DeviceId { get; init; }
}

/// <summary>Inventário completo. Campos que não puderam ser lidos ficam ausentes com causa.</summary>
public sealed record InventarioSistema
{
    public required InfoSistema Sistema { get; init; }

    public required InfoBios Bios { get; init; }

    public required InfoPlacaMae PlacaMae { get; init; }

    public required InfoCpu Cpu { get; init; }

    public required InfoMemoria Memoria { get; init; }

    public required IReadOnlyList<InfoGpu> Gpus { get; init; }

    public required IReadOnlyList<InfoMonitor> Monitores { get; init; }

    public required IReadOnlyList<InfoAdaptadorRede> Adaptadores { get; init; }

    public required InfoBateria Bateria { get; init; }

    public required IReadOnlyList<DispositivoComProblema> DispositivosComProblema { get; init; }

    public required DateTimeOffset ColetadoEm { get; init; }

    public required IReadOnlyList<DadoAusente> Limitacoes { get; init; }
}
