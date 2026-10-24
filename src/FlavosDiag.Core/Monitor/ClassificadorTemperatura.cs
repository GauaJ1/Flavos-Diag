using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Core.Monitor;

/// <summary>
/// Extrai a temperatura de CPU e de GPU de uma leitura de sensores.
/// </summary>
/// <remarks>
/// <para>
/// A escolha é por <see cref="TipoHardware"/> e só depois por nome. Antes era por
/// <c>Contains</c> no rótulo, e isso escolhia errado: a GPU também expõe um sensor chamado
/// <c>Core</c>, então a temperatura da placa podia ser lida como se fosse do processador.
/// </para>
/// <para>
/// O nome continua valendo como prioridade (o sensor do die <c>Tctl/Tdie</c> na CPU e
/// <c>GPU Core</c> na placa), e <see cref="TemperaturaFisica.Valida"/> continua sendo a segunda
/// barreira para qualquer valor que chegue aqui de outra fonte.
/// </para>
/// </remarks>
public static class ClassificadorTemperatura
{
    public const double MinimoValidoCelsius = TemperaturaFisica.MinimoValidoCelsius;

    public const double MaximoValidoCelsius = TemperaturaFisica.MaximoValidoCelsius;

    /// <summary>
    /// Sensores que representam a CPU, em ordem de preferência. Nomes verificados no coletor desta
    /// máquina; incluídos também os equivalentes usuais de outras plataformas para o mesmo papel.
    /// </summary>
    private static readonly string[] NomesCpu =
    {
        "Core (Tctl/Tdie)",
        "CPU Package",
        "Core Average",
        "CPU Core",
        "Package",
    };

    /// <summary>Sensores que representam a GPU, em ordem de preferência.</summary>
    private static readonly string[] NomesGpu =
    {
        "GPU Core",
        "GPU VR SoC",
        "GPU Hot Spot",
        "GPU Temperature",
    };

    /// <summary>Faixa utilizável de temperatura de componente em operação.</summary>
    public static bool Valida(double? celsius) => TemperaturaFisica.Valida(celsius);

    /// <summary>
    /// Escolhe a temperatura da CPU a partir das leituras de sensores, considerando apenas o que
    /// veio do componente do tipo CPU.
    /// </summary>
    public static double? DaCpu(AmostraSensores amostra, out string? causa)
    {
        ArgumentNullException.ThrowIfNull(amostra);
        return Escolher(amostra, TipoHardware.Cpu, NomesCpu, "CPU", out causa);
    }

    /// <summary>Mesma ideia para a GPU.</summary>
    public static double? DaGpu(AmostraSensores amostra, out string? causa)
    {
        ArgumentNullException.ThrowIfNull(amostra);
        return Escolher(amostra, TipoHardware.Gpu, NomesGpu, "GPU", out causa);
    }

    /// <summary>
    /// Escolha a partir de pares simples (nome, valor), sem informação de hardware. Mantida para
    /// chamadores que já têm só o nome; aqui não há como separar CPU de GPU pelo tipo.
    /// </summary>
    /// <remarks>
    /// Prefira a sobrecarga que recebe <see cref="AmostraSensores"/>: sem o tipo do hardware, um
    /// sensor chamado <c>Core</c> da GPU pode ser lido como temperatura do processador — que é
    /// exatamente o defeito que a seleção por tipo corrige. Não usar na proteção térmica.
    /// </remarks>
    [Obsolete("Use a sobrecarga que recebe AmostraSensores: sem TipoHardware não dá para separar CPU de GPU.")]
    public static double? DaCpu(IEnumerable<(string Nome, double Valor)> sensores, out string? causa)
    {
        ArgumentNullException.ThrowIfNull(sensores);
        return EscolherPorNome(sensores, NomesCpu, "CPU", out causa);
    }

    /// <summary>Mesma ideia para a GPU.</summary>
    [Obsolete("Use a sobrecarga que recebe AmostraSensores: sem TipoHardware não dá para separar CPU de GPU.")]
    public static double? DaGpu(IEnumerable<(string Nome, double Valor)> sensores, out string? causa)
    {
        ArgumentNullException.ThrowIfNull(sensores);
        return EscolherPorNome(sensores, NomesGpu, "GPU", out causa);
    }

    private static double? Escolher(
        AmostraSensores amostra,
        TipoHardware tipo,
        string[] nomes,
        string rotulo,
        out string? causa)
    {
        causa = null;

        var doTipo = amostra.Valores
            .Where(v => v.Categoria == CategoriaSensor.Temperatura &&
                        v.TipoHardware == tipo &&
                        v.Qualidade == QualidadeSensor.Ok)
            .Select(v => (Nome: v.NomeSensor ?? v.Rotulo, v.Valor))
            .ToList();

        if (doTipo.Count > 0)
        {
            var escolhido = EscolherPorNome(doTipo, nomes, rotulo, out causa);
            if (escolhido is not null)
            {
                return escolhido;
            }
        }

        // Nada com leitura: existe sensor reconhecido desse tipo mas sem valor?
        var semLeitura = amostra.SemLeitura
            .Where(s => s.Categoria == CategoriaSensor.Temperatura && s.TipoHardware == tipo)
            .ToList();

        if (semLeitura.Count > 0)
        {
            var detalhes = string.Join("; ", semLeitura.Select(s => $"{s.NomeSensor} {s.Motivo}"));
            causa = $"a {rotulo} tem {semLeitura.Count} sensor(es) de temperatura reconhecido(s), mas sem leitura: {detalhes}";
            return null;
        }

        causa ??= $"o sistema não expôs sensor de temperatura para a {rotulo}";
        return null;
    }

    private static double? EscolherPorNome(
        IEnumerable<(string Nome, double Valor)> sensores,
        string[] nomes,
        string rotulo,
        out string? causa)
    {
        causa = null;
        var candidatos = sensores.Where(s => Combina(s.Nome, nomes)).ToList();
        if (candidatos.Count == 0)
        {
            causa = $"o sistema não expôs sensor de temperatura para a {rotulo}";
            return null;
        }

        foreach (var c in OrdenarPorPreferencia(candidatos, nomes))
        {
            if (TemperaturaFisica.Valida(c.Valor))
            {
                return Math.Round(c.Valor, 1);
            }
        }

        var lidos = string.Join(", ", candidatos.Select(c => $"{c.Nome} = {c.Valor:0.#} °C"));
        causa = $"o sensor da {rotulo} devolveu valor inválido ({lidos}); 0 e 255 significam 'sem leitura'";
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
