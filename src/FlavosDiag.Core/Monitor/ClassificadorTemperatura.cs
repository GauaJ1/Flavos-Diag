namespace FlavosDiag.Core.Monitor;

/// <summary>
/// Extrai a temperatura de CPU e de GPU de uma leitura de sensores.
/// </summary>
/// <remarks>
/// <para>
/// Verificado nesta máquina (AMD Ryzen 5 8500G + Radeon 740M), pelo próprio coletor do app:
/// a CPU expõe <c>Core (Tctl/Tdie)</c> e a GPU expõe <c>GPU Core</c> e <c>GPU VR SoC</c>. O nome do
/// sensor é o único dado estável para distinguir os dois: o rótulo do hardware já vem como
/// "AMD Ryzen …", que não serve para classificar.
/// </para>
/// <para>
/// Nessa mesma máquina o sensor da CPU devolve <b>0</b> — o sentinela clássico de "não consegui ler".
/// Tratar esse 0 como temperatura faria o gráfico desenhar uma linha reta em 0 °C, que parece uma
/// leitura fria legítima. Aqui ele vira ausência com causa, e o gráfico mostra "sem leitura".
/// </para>
/// </remarks>
public static class ClassificadorTemperatura
{
    public const double MinimoValidoCelsius = 1;

    public const double MaximoValidoCelsius = 120;

    /// <summary>
    /// Sensores que representam a CPU. Nomes verificados no coletor desta máquina; incluídos também
    /// os equivalentes usuais de outras plataformas para o mesmo papel.
    /// </summary>
    private static readonly string[] NomesCpu =
    {
        "Core (Tctl/Tdie)",
        "CPU Package",
        "Core Average",
        "CPU Core",
        "Package",
    };

    /// <summary>Sensores que representam a GPU.</summary>
    private static readonly string[] NomesGpu =
    {
        "GPU Core",
        "GPU VR SoC",
        "GPU Hot Spot",
        "GPU Temperature",
    };

    /// <summary>
    /// A leitura é utilizável? 0 e 255 são sentinelas de "sem dado"; negativos e valores acima de
    /// 120 °C não são temperatura de componente em operação.
    /// </summary>
    public static bool Valida(double? celsius) =>
        celsius is { } v && !double.IsNaN(v) && v >= MinimoValidoCelsius && v <= MaximoValidoCelsius;

    /// <summary>
    /// Escolhe a temperatura da CPU. Prefere o sensor do die (Tctl/Tdie); se ele não for válido,
    /// tenta os demais. Devolve <c>null</c> com o motivo em <paramref name="causa"/> quando nenhum
    /// sensor da CPU entregou valor utilizável.
    /// </summary>
    public static double? DaCpu(IEnumerable<(string Nome, double Valor)> sensores, out string? causa)
    {
        causa = null;
        var candidatos = sensores.Where(s => Combina(s.Nome, NomesCpu)).ToList();
        if (candidatos.Count == 0)
        {
            causa = "o sistema não expôs sensor de temperatura para a CPU";
            return null;
        }

        foreach (var c in OrdenarPorPreferencia(candidatos, NomesCpu))
        {
            if (Valida(c.Valor))
            {
                return Math.Round(c.Valor, 1);
            }
        }

        var lidos = string.Join(", ", candidatos.Select(c => $"{c.Nome} = {c.Valor:0.#} °C"));
        causa = $"o sensor da CPU devolveu valor inválido ({lidos}); 0 e 255 significam 'sem leitura'";
        return null;
    }

    /// <summary>Mesma ideia para a GPU: prefere <c>GPU Core</c>.</summary>
    public static double? DaGpu(IEnumerable<(string Nome, double Valor)> sensores, out string? causa)
    {
        causa = null;
        var candidatos = sensores.Where(s => Combina(s.Nome, NomesGpu)).ToList();
        if (candidatos.Count == 0)
        {
            causa = "o sistema não expôs sensor de temperatura para a GPU";
            return null;
        }

        foreach (var c in OrdenarPorPreferencia(candidatos, NomesGpu))
        {
            if (Valida(c.Valor))
            {
                return Math.Round(c.Valor, 1);
            }
        }

        var lidos = string.Join(", ", candidatos.Select(c => $"{c.Nome} = {c.Valor:0.#} °C"));
        causa = $"o sensor da GPU devolveu valor inválido ({lidos})";
        return null;
    }

    private static bool Combina(string nome, string[] alvos) =>
        alvos.Any(a => nome.Contains(a, StringComparison.OrdinalIgnoreCase));

    /// <summary>Ordem de preferência conforme a lista: o primeiro nome da lista tem prioridade.</summary>
    private static IEnumerable<(string Nome, double Valor)> OrdenarPorPreferencia(
        List<(string Nome, double Valor)> candidatos,
        string[] ordem) =>
        candidatos.OrderBy(c =>
        {
            for (var i = 0; i < ordem.Length; i++)
            {
                if (c.Nome.Contains(ordem[i], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return ordem.Length;
        });
}
