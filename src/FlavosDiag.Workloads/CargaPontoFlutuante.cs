namespace FlavosDiag.Workloads;

/// <summary>
/// Mistura de ponto flutuante e inteiro: avaliação de polinômios (Horner) e funções trigonométricas
/// em cadeia, intercaladas com conversões inteiras. Determinístico.
/// </summary>
public sealed class CargaPontoFlutuante : ICargaCpu
{
    private double[] _x0 = Array.Empty<double>();
    private double[] _x1 = Array.Empty<double>();
    private const int Iteracoes = 12;

    public string Id => "ponto-flutuante";

    public string Nome => "Mistura de ponto flutuante e inteiro";

    public void Preparar(int tamanhoTrabalho, ulong semente)
    {
        var n = Math.Clamp(tamanhoTrabalho, 1024, 1 << 20);
        _x0 = new double[n];
        _x1 = new double[n];
        var rnd = new Xorshift(semente);
        for (var i = 0; i < n; i++)
        {
            _x0[i] = Math.Sin(rnd.NextFloat());
            _x1[i] = Math.Cos(rnd.NextFloat() * 3.0);
        }
    }

    public ResultadoBloco ExecutarBloco(int tamanhoTrabalho)
    {
        double acumulado = 0;
        long inteiros = 0;
        var c0 = 0.0173;
        var c1 = -0.0411;
        var c2 = 0.00907;

        for (var qualquer = 0; qualquer < _x0.Length; qualquer++)
        {
            var x = _x0[qualquer];
            var y = _x1[qualquer];
            for (var it = 0; it < Iteracoes; it++)
            {
                // Horner de grau 3
                var p = ((c2 * x + c1) * x + c0) * x + 0.5;
                y = Math.Sin(y * 0.5 + p);
                x = Math.Cos(x * 0.25 - y);
            }

            acumulado += x * y;
            inteiros ^= (long)(x * 1e6) + ((long)(y * 1e6) << 7);
        }

        var bits = BitConverter.DoubleToInt64Bits(acumulado);
        return new ResultadoBloco(_x0.Length * Iteracoes, (ulong)bits ^ (ulong)inteiros, true);
    }
}
