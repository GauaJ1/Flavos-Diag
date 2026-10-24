namespace FlavosDiag.Providers.Wmi;

/// <summary>Registro de uma consulta WMI executada (apenas para diagnóstico interno e testes).</summary>
public sealed record ConsultaWmi(string Classe, DateTimeOffset Quando, long Milissegundos, bool Falhou, string? Erro);

/// <summary>
/// Envolve qualquer <see cref="IWmiFonte"/> e registra cada consulta (classe, tempo, falha).
/// Usado pelo Diagnóstico rápido para explicar, em "Limitações", o que não pôde ser lido e por quê.
/// </summary>
public sealed class WmiFonteAuditada : IWmiFonte
{
    private readonly IWmiFonte _interna;
    private readonly List<ConsultaWmi> _consultas = new();

    public WmiFonteAuditada(IWmiFonte interna)
    {
        _interna = interna;
    }

    public IReadOnlyList<ConsultaWmi> Consultas
    {
        get
        {
            lock (_consultas)
            {
                return _consultas.ToList();
            }
        }
    }

    public void Limpar()
    {
        lock (_consultas)
        {
            _consultas.Clear();
        }
    }

    public async Task<IReadOnlyList<WmiLinha>> ConsultarAsync(string consulta, string? namespaceWmi = "root\\cimv2", CancellationToken cancelamento = default)
    {
        var classe = ExtrairClasse(consulta);
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var linhas = await _interna.ConsultarAsync(consulta, namespaceWmi, cancelamento);
            lock (_consultas)
            {
                _consultas.Add(new ConsultaWmi(classe, DateTimeOffset.Now, relogio.ElapsedMilliseconds, false, null));
            }

            return linhas;
        }
        catch (Exception ex)
        {
            lock (_consultas)
            {
                _consultas.Add(new ConsultaWmi(classe, DateTimeOffset.Now, relogio.ElapsedMilliseconds, true, ex.Message));
            }

            throw;
        }
    }

    public static string ExtrairClasse(string consulta)
    {
        var indice = consulta.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (indice < 0)
        {
            return consulta;
        }

        var resto = consulta[(indice + 6)..].Trim();
        var fim = resto.IndexOfAny(new[] { ' ', '\r', '\n' });
        return fim < 0 ? resto : resto[..fim];
    }
}
