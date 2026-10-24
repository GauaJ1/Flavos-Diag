namespace FlavosDiag.Workloads;

/// <summary>
/// Mistura de inteiros de 64 bits: FNV-1a sobre valores gerados por um gerador xorshift
/// e combinação com rotinas de mistura próprias. Determinístico.
/// </summary>
public sealed class CargaHashInteiro : ICargaCpu
{
    private ulong[] _dados = Array.Empty<ulong>();

    public string Id => "hash-inteiro";

    public string Nome => "Hashing e mistura de inteiros";

    public void Preparar(int tamanhoTrabalho, ulong semente)
    {
        var n = Math.Clamp(tamanhoTrabalho, 1024, 1 << 20);
        _dados = new ulong[n];
        var rnd = new Xorshift(semente);
        for (var i = 0; i < n; i++)
        {
            _dados[i] = rnd.NextULong();
        }
    }

    public ResultadoBloco ExecutarBloco(int tamanhoTrabalho)
    {
        ulong hash = 14695981039346656037UL;
        var dados = _dados;
        for (var i = 0; i < dados.Length; i++)
        {
            var v = dados[i] ^ (ulong)i;
            v ^= v >> 33;
            v *= 0xff51afd7ed558ccdUL;
            v ^= v >> 33;
            v *= 0xc4ceb9fe1a85ec53UL;
            v ^= v >> 33;
            hash ^= v;
            hash *= 1099511628211UL;
            hash ^= hash >> 29;
        }

        return new ResultadoBloco(dados.Length, hash, true);
    }
}
