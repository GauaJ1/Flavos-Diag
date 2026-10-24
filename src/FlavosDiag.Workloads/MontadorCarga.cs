using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Workloads;

/// <summary>Tamanho de trabalho por carga e por perfil (decisão própria: trabalho fixo por perfil).</summary>
public static class MontadorCarga
{
    public const ulong SementePadrao = 0x5EED_0F_FA_1_05UL;

    public static int Tamanho(PerfilCarga perfil, string cargaId) => cargaId switch
    {
        "matriz-simd" => perfil switch
        {
            PerfilCarga.Leve => 64,
            PerfilCarga.Medio => 128,
            _ => 192,
        },
        "crivo-primos" => perfil switch
        {
            PerfilCarga.Leve => 500_000,
            PerfilCarga.Medio => 1_000_000,
            _ => 2_000_000,
        },
        "hash-inteiro" => perfil switch
        {
            PerfilCarga.Leve => 1 << 14,
            PerfilCarga.Medio => 1 << 16,
            _ => 1 << 18,
        },
        _ => perfil switch
        {
            PerfilCarga.Leve => 1 << 13,
            PerfilCarga.Medio => 1 << 15,
            _ => 1 << 17,
        },
    };

    /// <summary>Todas as cargas originais. Cada worker recebe sua própria instância (buffers independentes).</summary>
    public static IReadOnlyList<Func<ICargaCpu>> Fabrica() => new Func<ICargaCpu>[]
    {
        () => new CargaMatrizSimd(),
        () => new CargaCrivoPrimos(),
        () => new CargaHashInteiro(),
        () => new CargaPontoFlutuante(),
    };

    /// <summary>Workers por perfil e número de núcleos/threads.</summary>
    public static int Workers(PerfilCarga perfil, int nucleosFisicos, int threadsLogicas)
    {
        var logicas = Math.Max(1, threadsLogicas);
        var fisicos = Math.Max(1, Math.Min(nucleosFisicos, logicas));
        return perfil switch
        {
            PerfilCarga.Leve => fisicos,
            _ => logicas,
        };
    }
}
