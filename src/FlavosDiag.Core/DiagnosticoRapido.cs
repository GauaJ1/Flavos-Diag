using System.Diagnostics;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Core.Abstractions;

namespace FlavosDiag.Core.Diagnostics;

public sealed record EtapaDiagnostico(string Nome, TimeSpan Duracao, bool Concluida, string? Limitacao);

public sealed record ResultadoDiagnosticoRapido
{
    public required DateTimeOffset IniciadoEm { get; init; }

    public required TimeSpan Duracao { get; init; }

    public required InventarioSistema? Inventario { get; init; }

    public required IReadOnlyList<DiscoInfo> Discos { get; init; }

    public required InfoBateriaDetalhe? Bateria { get; init; }

    public required AmostraSensores? UltimaAmostra { get; init; }

    public required IReadOnlyList<AmostraSensores> AmostrasSensores { get; init; }

    public required InfoRede? Rede { get; init; }

    public required IReadOnlyList<EtapaDiagnostico> Etapas { get; init; }

    public required IReadOnlyList<ResultadoCategoria> Categorias { get; init; }

    public required ResultadoAgregacao Agregacao { get; init; }

    public required IReadOnlyList<DadoAusente> Limitacoes { get; init; }

    public required bool Cancelado { get; init; }
}

/// <summary>
/// Diagnóstico rápido (E11): inventário → saúde de disco → bateria → sensores por 60 s → rede básica.
/// Cada etapa tem tempo limite próprio e nunca derruba as demais.
/// </summary>
public sealed class DiagnosticoRapido
{
    private readonly IInventarioProvider _inventario;
    private readonly IDiscoProvider _discos;
    private readonly IBateriaProvider _bateria;
    private readonly ISensorProviderFactory _sensores;
    private readonly IRedeProvider _rede;

    public DiagnosticoRapido(
        IInventarioProvider inventario,
        IDiscoProvider discos,
        IBateriaProvider bateria,
        ISensorProviderFactory sensores,
        IRedeProvider rede)
    {
        _inventario = inventario;
        _discos = discos;
        _bateria = bateria;
        _sensores = sensores;
        _rede = rede;
    }

    public static readonly TimeSpan DuracaoSensores = TimeSpan.FromSeconds(60);

    public async Task<ResultadoDiagnosticoRapido> ExecutarAsync(
        double limiteCpuCelsius,
        IProgress<string>? progresso = null,
        CancellationToken cancelamento = default)
    {
        var inicio = DateTimeOffset.Now;
        var relogio = Stopwatch.StartNew();
        var etapas = new List<EtapaDiagnostico>();
        var limitacoes = new List<DadoAusente>();

        InventarioSistema? inventario = null;
        IReadOnlyList<DiscoInfo> discos = Array.Empty<DiscoInfo>();
        InfoBateriaDetalhe? bateria = null;
        InfoRede? rede = null;
        var amostras = new List<AmostraSensores>();

        // 1. Inventário
        progresso?.Report("Lendo inventário de hardware…");
        etapas.Add(await EtapaAsync("Inventário", TimeSpan.FromSeconds(90), async ct =>
        {
            inventario = await _inventario.ColetarAsync(ct);
        }, limitacoes, "Inventário de hardware", cancelamento));

        if (inventario is not null)
        {
            limitacoes.AddRange(inventario.Limitacoes);
        }

        // 2. Saúde de disco
        progresso?.Report("Verificando saúde dos discos…");
        etapas.Add(await EtapaAsync("Saúde do disco", TimeSpan.FromSeconds(45), async ct =>
        {
            discos = await _discos.ListarAsync(ct);
        }, limitacoes, "Saúde do disco", cancelamento));

        foreach (var d in discos)
        {
            limitacoes.AddRange(d.Saude.Limitacoes.Select(l => new DadoAusente(
                $"Disco {d.Numero}: {l.Item}",
                l.Motivo,
                l.Fonte)));
        }

        // 3. Bateria
        progresso?.Report("Lendo bateria…");
        etapas.Add(await EtapaAsync("Bateria", TimeSpan.FromSeconds(30), async ct =>
        {
            bateria = await _bateria.LerAsync(ct);
        }, limitacoes, "Bateria", cancelamento));

        if (bateria is not null)
        {
            limitacoes.AddRange(bateria.Limitacoes);
        }

        // 4. Sensores por 60 s
        progresso?.Report("Coletando sensores por 60 s…");
        etapas.Add(await EtapaAsync("Sensores (60 s)", DuracaoSensores + TimeSpan.FromSeconds(20), async ct =>
        {
            await using var sessao = _sensores.Criar();
            var abertura = await sessao.AbrirAsync(ct);
            if (abertura.Ausente)
            {
                amostras.Add(new AmostraSensores
                {
                    Quando = DateTimeOffset.Now,
                    Valores = Array.Empty<ValorSensor>(),
                    CausaAusencia = abertura.Causa,
                });
                return;
            }

            var fim = DateTimeOffset.Now + DuracaoSensores;
            while (DateTimeOffset.Now < fim)
            {
                ct.ThrowIfCancellationRequested();
                amostras.Add(sessao.Ler());
                await Task.Delay(1000, ct);
            }
        }, limitacoes, "Leitura de sensores", cancelamento));

        // 5. Rede básica (sem servidor de velocidade)
        progresso?.Report("Testando rede…");
        etapas.Add(await EtapaAsync("Rede básica", TimeSpan.FromSeconds(45), async ct =>
        {
            rede = await _rede.LerAsync(Array.Empty<string>(), null, ct);
        }, limitacoes, "Rede", cancelamento));

        if (rede is not null)
        {
            limitacoes.AddRange(rede.Limitacoes);
        }

        var resumos = AmostraSensoresUtil.Resumo(amostras);
        var categorias = new List<ResultadoCategoria>();

        if (inventario is not null)
        {
            categorias.Add(Categoria(Regras.CategoriaSistema, Regras.Sistema(inventario)));
            categorias.Add(Categoria(Regras.CategoriaMemoria, Regras.Memoria(inventario.Memoria)));
            categorias.Add(Categoria(Regras.CategoriaCpu, Regras.Cpu(inventario, null, resumos, limiteCpuCelsius)));
            categorias.Add(Categoria(Regras.CategoriaGpu, Regras.Gpu(inventario.Gpus, null)));
        }
        else
        {
            foreach (var c in new[] { Regras.CategoriaSistema, Regras.CategoriaMemoria, Regras.CategoriaCpu, Regras.CategoriaGpu })
            {
                categorias.Add(new ResultadoCategoria { Categoria = c, Veredito = Veredito.Inconclusivo, Resumo = "Inventário não pôde ser coletado." });
            }
        }

        categorias.Add(Categoria(Regras.CategoriaDisco, Regras.Disco(discos)));
        categorias.Add(Categoria(Regras.CategoriaSensores, Regras.Sensores(resumos, limiteCpuCelsius, amostras)));
        if (bateria is not null)
        {
            categorias.Add(Categoria(Regras.CategoriaBateria, Regras.Bateria(bateria)));
        }

        if (rede is not null)
        {
            categorias.Add(Categoria(Regras.CategoriaRede, Regras.Rede(rede)));
        }

        return new ResultadoDiagnosticoRapido
        {
            IniciadoEm = inicio,
            Duracao = relogio.Elapsed,
            Inventario = inventario,
            Discos = discos,
            Bateria = bateria,
            UltimaAmostra = amostras.LastOrDefault(),
            AmostrasSensores = amostras,
            Rede = rede,
            Etapas = etapas,
            Categorias = categorias,
            Agregacao = VereditoConsolidador.Agregar(categorias),
            Limitacoes = limitacoes,
            Cancelado = cancelamento.IsCancellationRequested,
        };
    }

    private static ResultadoCategoria Categoria(string nome, RespostaRegra r) =>
        new() { Categoria = nome, Veredito = r.Veredito, Resumo = r.Resumo };

    private static async Task<EtapaDiagnostico> EtapaAsync(
        string nome,
        TimeSpan limite,
        Func<CancellationToken, Task> acao,
        List<DadoAusente> limitacoes,
        string itemLimite,
        CancellationToken externo)
    {
        var relogio = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externo);
        cts.CancelAfter(limite);
        try
        {
            await acao(cts.Token).WaitAsync(limite, externo);
            return new EtapaDiagnostico(nome, relogio.Elapsed, true, null);
        }
        catch (OperationCanceledException) when (!externo.IsCancellationRequested)
        {
            var motivo = $"{itemLimite}: etapa excedeu o tempo limite de {limite.TotalSeconds:N0} s.";
            limitacoes.Add(new DadoAusente(itemLimite, motivo));
            return new EtapaDiagnostico(nome, relogio.Elapsed, false, motivo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var motivo = $"{itemLimite}: falha ao coletar ({ex.Message}).";
            limitacoes.Add(new DadoAusente(itemLimite, motivo));
            return new EtapaDiagnostico(nome, relogio.Elapsed, false, motivo);
        }
    }
}

public static class AmostraSensoresUtil
{
    public static IReadOnlyList<ResumoSensor> Resumo(IReadOnlyList<AmostraSensores> amostras)
    {
        return amostras
            .SelectMany(a => a.Valores)
            .GroupBy(v => v.Id)
            .Select(g =>
            {
                var valores = g.Select(v => v.Valor).Where(v => !double.IsNaN(v)).ToList();
                if (valores.Count == 0)
                {
                    return null;
                }

                var primeiro = g.First();
                return new ResumoSensor
                {
                    Id = primeiro.Id,
                    Rotulo = primeiro.Rotulo,
                    Categoria = primeiro.Categoria,
                    Unidade = primeiro.Unidade,
                    Minimo = valores.Min(),
                    Maximo = valores.Max(),
                    Media = valores.Average(),
                    Amostras = valores.Count,
                };
            })
            .Where(r => r is not null)
            .Select(r => r!)
            .OrderBy(r => r.Categoria)
            .ThenBy(r => r.Rotulo)
            .ToList();
    }
}
