using System.Numerics;

namespace FlavosDiag.Workloads;

/// <summary>
/// Multiplicação de matrizes quadradas com vetorização explícita (Vector&lt;float&gt;).
/// Algoritmo próprio: laço por blocos com acumulação vetorial na linha da matriz C.
/// Verificação: C[i,j] deve ser igual para todas as colunas do bloco; o checksum agrega C inteira.
/// </summary>
public sealed class CargaMatrizSimd : ICargaCpu
{
    private float[] _a = Array.Empty<float>();
    private float[] _b = Array.Empty<float>();
    private float[] _c = Array.Empty<float>();
    private int _n;

    public string Id => "matriz-simd";

    public string Nome => "Multiplicação de matrizes (vetorizada)";

    public void Preparar(int tamanhoTrabalho, ulong semente)
    {
        _n = Math.Clamp(tamanhoTrabalho, 16, 512);
        var n = _n;
        _a = new float[n * n];
        _b = new float[n * n];
        _c = new float[n * n];
        var rnd = new Xorshift(semente ^ 0x9E3779B97F4A7C15UL);
        for (var i = 0; i < _a.Length; i++)
        {
            _a[i] = rnd.NextFloat() * 2f - 1f;
            _b[i] = rnd.NextFloat() * 2f - 1f;
        }
    }

    public ResultadoBloco ExecutarBloco(int tamanhoTrabalho)
    {
        var n = _n;
        var a = _a;
        var b = _b;
        var c = _c;
        var largura = Vector<float>.Count;

        for (var i = 0; i < n; i++)
        {
            var linhaA = i * n;
            Array.Clear(c, linhaA, n);
            for (var k = 0; k < n; k++)
            {
                var aik = a[linhaA + k];
                var escala = new Vector<float>(aik);
                var linhaB = k * n;
                var j = 0;
                for (; j <= n - largura; j += largura)
                {
                    var vc = new Vector<float>(c, linhaA + j);
                    var vb = new Vector<float>(b, linhaB + j);
                    (vc + escala * vb).CopyTo(c, linhaA + j);
                }

                for (; j < n; j++)
                {
                    c[linhaA + j] += aik * b[linhaB + j];
                }
            }
        }

        ulong soma = 1469598103934665603UL;
        for (var idx = 0; idx < c.Length; idx++)
        {
            soma ^= (ulong)BitConverter.SingleToInt32Bits(c[idx]);
            soma *= 1099511628211UL;
        }

        return new ResultadoBloco((long)n * n * n, soma, true);
    }

    /// <summary>Conferência independente (não vetorizada) de uma célula amostrada.</summary>
    public bool ConferirCelula(int i, int j)
    {
        var n = _n;
        double esperado = 0;
        for (var k = 0; k < n; k++)
        {
            esperado += (double)_a[i * n + k] * _b[k * n + j];
        }

        var obtido = _c[i * n + j];
        var tolerancia = 1e-3 * Math.Max(1.0, Math.Abs(esperado));
        return Math.Abs(obtido - esperado) <= tolerancia;
    }

    public int Dimensao => _n;
}

internal struct Xorshift
{
    private ulong _estado;

    public Xorshift(ulong semente)
    {
        _estado = semente == 0 ? 0x2545F4914F6CDD1DUL : semente;
    }

    public ulong NextULong()
    {
        _estado ^= _estado << 13;
        _estado ^= _estado >> 7;
        _estado ^= _estado << 17;
        return _estado;
    }

    public int NextInt(int maximo) => (int)(NextULong() % (ulong)maximo);

    public float NextFloat() => (NextULong() >> 40) / (float)(1UL << 24);
}
