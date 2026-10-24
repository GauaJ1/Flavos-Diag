using System.Diagnostics;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Workloads;

/// <summary>
/// Executa as cargas originais em N workers. Faz a verificação de cálculo reexecutando cada bloco
/// (mesmo seed/tamanho => mesmo checksum) quando <paramref name="verificar"/> está ativo.
/// Calcular o checksum duas vezes dobra o custo; por isso a verificação roda em blocos por worker
/// de forma amostrada (decisão: verificar sempre o resultado de cada bloco do ciclo atual porque
/// todas as cargas são determinísticas e o bloco já é recalculado no ciclo seguinte).
/// </summary>
public sealed class ExecutorCarga
{
    private readonly IReadOnlyList<Func<ICargaCpu>> _fabrica;

    public ExecutorCarga(IReadOnlyList<Func<ICargaCpu>>? fabrica = null)
    {
        _fabrica = fabrica ?? MontadorCarga.Fabrica();
    }

    public sealed record Progresso(TimeSpan Decorrido, long Ciclos, int Erros, double CpuPercentual);

    /// <summary>
    /// Roda a carga até <paramref name="duracao"/> ou até <c>deveParar()</c> retornar true.
    /// </summary>
    public Task<ResumoExecucao> ExecutarAsync(
        int workers,
        PerfilCarga perfil,
        TimeSpan duracao,
        Func<bool> deveParar,
        IProgress<Progresso>? progresso = null,
        CancellationToken cancelamento = default)
    {
        return Task.Run(() =>
        {
            var parar = new ManualResetEventSlim(false);
            long ciclos = 0;
            var erros = 0;
            var relogio = Stopwatch.StartNew();

            var threads = new Thread[workers];
            for (var w = 0; w < workers; w++)
            {
                var indice = w;
                threads[w] = new Thread(() =>
                {
                    var cargas = _fabrica.Select(f => f()).ToList();
                    foreach (var c in cargas)
                    {
                        c.Preparar(MontadorCarga.Tamanho(perfil, c.Id), MontadorCarga.SementePadrao + (ulong)indice);
                    }

                    // Checksum de referência por carga (computado antes do início da contagem de ciclos).
                    var referencia = cargas.Select(c => c.ExecutarBloco(MontadorCarga.Tamanho(perfil, c.Id))).ToList();
                    for (var i = 0; i < cargas.Count; i++)
                    {
                        cargas[i].Preparar(MontadorCarga.Tamanho(perfil, cargas[i].Id), MontadorCarga.SementePadrao + (ulong)indice);
                    }

                    while (!parar.IsSet)
                    {
                        for (var i = 0; i < cargas.Count; i++)
                        {
                            var r = cargas[i].ExecutarBloco(MontadorCarga.Tamanho(perfil, cargas[i].Id));
                            if (r.Checksum != referencia[i].Checksum)
                            {
                                Interlocked.Increment(ref erros);
                            }
                        }

                        Interlocked.Add(ref ciclos, cargas.Count);
                        if (relogio.Elapsed >= duracao || deveParar())
                        {
                            parar.Set();
                            break;
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = $"flavos-carga-{w}",
                };
                threads[w].Start();
            }

            if (progresso is not null)
            {
                while (relogio.Elapsed < duracao && !parar.Wait(500))
                {
                    if (deveParar())
                    {
                        parar.Set();
                    }

                    progresso.Report(new Progresso(relogio.Elapsed, Interlocked.Read(ref ciclos), erros, 0));
                    if (cancelamento.IsCancellationRequested)
                    {
                        parar.Set();
                    }
                }
            }
            else
            {
                // Mesmo sem progresso, o predicado de parada precisa ser avaliado periodicamente:
                // é ele que amostra a temperatura e protege o equipamento.
                // Espera até a duração ou até o cancelamento, e então encerra normalmente:
                // cancelar é uma parada legítima (o motivo é registrado), não uma exceção.
                var restante = duracao - relogio.Elapsed;
                while (restante > TimeSpan.Zero)
                {
                    if (cancelamento.IsCancellationRequested)
                    {
                        parar.Set();
                        break;
                    }

                    var passo = restante > TimeSpan.FromMilliseconds(100) ? TimeSpan.FromMilliseconds(100) : restante;
                    if (parar.Wait(passo))
                    {
                        break;
                    }

                    if (deveParar())
                    {
                        parar.Set();
                        break;
                    }

                    restante = duracao - relogio.Elapsed;
                }
            }

            parar.Set();
            foreach (var t in threads)
            {
                t.Join(TimeSpan.FromSeconds(10));
            }

            return new ResumoExecucao(relogio.Elapsed, Interlocked.Read(ref ciclos), erros);
        });
    }
}

public sealed record ResumoExecucao(TimeSpan DuracaoEfetiva, long Ciclos, int Erros);

/// <summary>Medição de benchmark: executa a carga por N segundos e devolve unidades/segundo.</summary>
public static class MedidorSingle
{
    public static (double UnidadesPorSegundo, ulong Checksum) Medir(ICargaCpu carga, int tamanho, TimeSpan janela)
    {
        carga.Preparar(tamanho, MontadorCarga.SementePadrao);
        var referencia = carga.ExecutarBloco(tamanho);
        carga.Preparar(tamanho, MontadorCarga.SementePadrao);

        long unidades = 0;
        var ok = true;
        var sw = Stopwatch.StartNew();
        do
        {
            var r = carga.ExecutarBloco(tamanho);
            ok &= r.Checksum == referencia.Checksum;
            unidades += r.Unidades;
        }
        while (sw.Elapsed < janela);

        var segundos = sw.Elapsed.TotalSeconds;
        return (unidades / segundos, ok ? referencia.Checksum : 0);
    }
}
