using System.Management;

namespace FlavosDiag.Providers.Wmi;

/// <summary>Linha crua de WMI: pares nome-da-propriedade → valor (string/número/bool/DataTime).</summary>
public sealed class WmiLinha : Dictionary<string, object?>, IEquatable<WmiLinha>
{
    public WmiLinha()
    {
    }

    public WmiLinha(IDictionary<string, object?> origem) : base(origem)
    {
    }

    public bool Equals(WmiLinha? other) => other is not null && ReferenceEquals(this, other);

    public override bool Equals(object? obj) => Equals(obj as WmiLinha);

    public override int GetHashCode() => base.GetHashCode();
}

/// <summary>
/// Fonte de dados WMI/CIM. Interface permite substituir por fixtures nos testes.
/// </summary>
public interface IWmiFonte
{
    /// <summary>Executa a consulta e devolve as linhas. Lança <see cref="WmiIndisponivelException"/> em falha de namespace/serviço.</summary>
    Task<IReadOnlyList<WmiLinha>> ConsultarAsync(string consulta, string? namespaceWmi = "root\\cimv2", CancellationToken cancelamento = default);
}

public sealed class WmiIndisponivelException : Exception
{
    public WmiIndisponivelException(string mensagem, Exception? interna = null) : base(mensagem, interna)
    {
    }
}

/// <summary>Implementação real via System.Management (WMI). Somente leitura.</summary>
public sealed class WmiFonte : IWmiFonte
{
    public Task<IReadOnlyList<WmiLinha>> ConsultarAsync(string consulta, string? namespaceWmi = "root\\cimv2", CancellationToken cancelamento = default)
    {
        return Task.Run<IReadOnlyList<WmiLinha>>(() =>
        {
            var linhas = new List<WmiLinha>();
            try
            {
                var escopo = new ManagementScope(namespaceWmi is null ? @"root\cimv2" : namespaceWmi);
                escopo.Connect();
                using var searcher = new ManagementObjectSearcher(escopo, new ObjectQuery(consulta));
                searcher.Options.Timeout = TimeSpan.FromSeconds(15);
                searcher.Options.ReturnImmediately = true;
                using var resultados = searcher.Get();
                foreach (ManagementObject objeto in resultados)
                {
                    cancelamento.ThrowIfCancellationRequested();
                    var linha = new WmiLinha();
                    foreach (PropertyData prop in objeto.Properties)
                    {
                        linha[prop.Name] = prop.Value switch
                        {
                            null => null,
                            string s => s,
                            _ => prop.Value,
                        };
                    }

                    linhas.Add(linha);
                    objeto.Dispose();
                }
            }
            catch (ManagementException ex)
            {
                throw new WmiIndisponivelException("Consulta WMI falhou: " + ex.Message, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new WmiIndisponivelException("Sem permissão para ler WMI: " + ex.Message, ex);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                throw new WmiIndisponivelException("Serviço WMI indisponível: " + ex.Message, ex);
            }

            return linhas;
        }, cancelamento);
    }
}

/// <summary>Leitura tolerante de valores das linhas WMI.</summary>
public static class WmiValores
{
    public static string? Texto(this WmiLinha linha, params string[] nomes)
    {
        foreach (var n in nomes)
        {
            if (linha.TryGetValue(n, out var v) && v is not null)
            {
                var s = v switch
                {
                    string str => str,
                    DateTime d => d.ToString("yyyyMMddHHmmss.ffffff+000"),
                    _ => v.ToString(),
                };
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s.Trim();
                }
            }
        }

        return null;
    }

    public static long? Inteiro(this WmiLinha linha, params string[] nomes)
    {
        foreach (var n in nomes)
        {
            if (!linha.TryGetValue(n, out var v) || v is null)
            {
                continue;
            }

            try
            {
                return v switch
                {
                    ulong u => (long)Math.Min(u, long.MaxValue),
                    uint ui => ui,
                    ushort us => us,
                    int i => i,
                    long l => l,
                    short sh => sh,
                    byte b => b,
                    string s when long.TryParse(s, out var parsed) => parsed,
                    _ => Convert.ToInt64(v),
                };
            }
            catch (Exception)
            {
                // valor não numérico: segue tentando o próximo nome
            }
        }

        return null;
    }

    public static bool? Booleano(this WmiLinha linha, params string[] nomes)
    {
        foreach (var n in nomes)
        {
            if (!linha.TryGetValue(n, out var v) || v is null)
            {
                continue;
            }

            if (v is bool b)
            {
                return b;
            }

            if (v is string s && bool.TryParse(s, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    public static DateTime? DataWmi(this WmiLinha linha, params string[] nomes)
    {
        foreach (var n in nomes)
        {
            if (!linha.TryGetValue(n, out var v) || v is null)
            {
                continue;
            }

            if (v is string s && s.Length >= 8 && s[..8].All(char.IsDigit))
            {
                if (DateTime.TryParseExact(s[..8], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d))
                {
                    return d;
                }
            }
        }

        return null;
    }
}
