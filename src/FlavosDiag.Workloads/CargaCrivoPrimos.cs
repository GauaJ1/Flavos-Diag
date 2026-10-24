namespace FlavosDiag.Workloads;

/// <summary>
/// Crivo de Eratóstenes segmentado (algoritmo clássico, implementação própria).
/// Verificação: soma e contagem dos primos no intervalo — determinística.
/// </summary>
public sealed class CargaCrivoPrimos : ICargaCpu
{
    private const int Segmento = 1 << 16;
    private int[] _primosBase = Array.Empty<int>();
    private readonly byte[] _marcado = new byte[Segmento];

    public string Id => "crivo-primos";

    public string Nome => "Crivo de primos segmentado";

    public void Preparar(int tamanhoTrabalho, ulong semente)
    {
        var limite = Math.Clamp(tamanhoTrabalho, 1_000, 4_000_000);
        var raiz = (int)Math.Sqrt(limite) + 1;
        var crivo = new bool[raiz + 1];
        var primos = new List<int>();
        for (var i = 2; i <= raiz; i++)
        {
            if (crivo[i])
            {
                continue;
            }

            primos.Add(i);
            for (var j = i * i; j <= raiz; j += i)
            {
                crivo[j] = true;
            }
        }

        _primosBase = primos.ToArray();
        _limite = limite;
    }

    private int _limite;

    public ResultadoBloco ExecutarBloco(int tamanhoTrabalho)
    {
        var contagem = 0L;
        var soma = 0UL;
        var marcado = _marcado;

        for (var inicio = 0; inicio < _limite; inicio += Segmento)
        {
            var fim = Math.Min(inicio + Segmento, _limite);
            Array.Clear(marcado, 0, fim - inicio);
            foreach (var p in _primosBase)
            {
                var multiplo = Math.Max(p * p, ((inicio + p - 1) / p) * p);
                for (var m = multiplo; m < fim; m += p)
                {
                    marcado[m - inicio] = 1;
                }
            }

            for (var v = Math.Max(inicio, 2); v < fim; v++)
            {
                if (marcado[v - inicio] == 0)
                {
                    contagem++;
                    soma += (ulong)v;
                }
            }
        }

        return new ResultadoBloco(contagem, soma, true);
    }
}
