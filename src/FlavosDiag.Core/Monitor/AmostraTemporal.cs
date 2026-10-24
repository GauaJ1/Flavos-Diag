namespace FlavosDiag.Core.Monitor;

/// <summary>
/// Uma leitura de sensores num instante. Temperatura é <c>null</c> quando o sensor não devolveu um
/// valor utilizável (0 e 255 são sentinelas; fora de 1–120 °C também não vale). Nulo nunca se
/// transforma em 0 num gráfico: a linha simplesmente não passa por ali.
/// </summary>
public sealed record AmostraTemporal
{
    public required DateTimeOffset Quando { get; init; }

    /// <summary>Segundos desde o início da sessão de medição.</summary>
    public required double Segundos { get; init; }

    public double? TemperaturaCpuCelsius { get; init; }

    public double? TemperaturaGpuCelsius { get; init; }

    public double? CargaCpuPercentual { get; init; }

    public double? ClockCpuMhz { get; init; }

    /// <summary>Motivo pelo qual a temperatura da CPU está ausente, quando aplicável.</summary>
    public string? CausaCpu { get; init; }

    public string? CausaGpu { get; init; }

    public bool TemAlgumaTemperatura => TemperaturaCpuCelsius is not null || TemperaturaGpuCelsius is not null;
}

/// <summary>
/// Histórico circular de amostras com tamanho fixo. Guardar tudo cresceria sem limite num teste
/// longo; o gráfico mostra uma janela de tempo e nada além disso precisa ficar na memória.
/// </summary>
public sealed class HistoricoAmostras
{
    private readonly AmostraTemporal[] _buffer;
    private int _inicio;
    private int _total;

    public HistoricoAmostras(int capacidade = 3600)
    {
        if (capacidade < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacidade), "A capacidade precisa ser positiva.");
        }

        _buffer = new AmostraTemporal[capacidade];
    }

    public int Capacidade => _buffer.Length;

    public int Quantidade => _total;

    public void Adicionar(AmostraTemporal amostra)
    {
        _buffer[(_inicio + _total) % _buffer.Length] = amostra;
        if (_total < _buffer.Length)
        {
            _total++;
        }
        else
        {
            // Cheio: o mais antigo sai e o início avança.
            _inicio = (_inicio + 1) % _buffer.Length;
        }
    }

    /// <summary>Amostras em ordem cronológica (mais antiga primeiro).</summary>
    public IReadOnlyList<AmostraTemporal> Listar()
    {
        var saida = new List<AmostraTemporal>(_total);
        for (var i = 0; i < _total; i++)
        {
            saida.Add(_buffer[(_inicio + i) % _buffer.Length]);
        }

        return saida;
    }

    /// <summary>Só as amostras dos últimos <paramref name="segundos"/> — é o que o gráfico desenha.</summary>
    public IReadOnlyList<AmostraTemporal> Janela(double segundos)
    {
        var todas = Listar();
        if (todas.Count == 0 || segundos <= 0)
        {
            return todas;
        }

        var limite = todas[^1].Segundos - segundos;
        return todas.Where(a => a.Segundos >= limite).ToList();
    }

    public void Limpar()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _inicio = 0;
        _total = 0;
    }
}
