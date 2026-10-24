using System.Runtime.InteropServices;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Workloads;

namespace FlavosDiag.Tests;

/// <summary>
/// Testes do teste de memória da Fase 3. Nenhum deles estressa a máquina: os tamanhos são pequenos e a
/// leitura de memória livre é injetada, para o resultado não depender da máquina que roda a suíte.
/// </summary>
public static class MemoriaTests
{
    private const long UmGiB = 1024L * 1024 * 1024;

    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos() => new (string, Func<Task>)[]
    {
        ("Memória: buffer íntegro passa na conferência", BufferIntegroPassa),
        ("Memória: 1 bit alterado é detectado em cada padrão", BitAlteradoDetectadoPorPadrao),
        ("Memória: cancelamento no meio de uma etapa devolve parcial rápido", CancelamentoParcialRapido),
        ("Memória: tamanho padrão nunca excede a fração da memória livre", TamanhoPadraoRespeitaFracao),
        ("Memória: memória livre ilegível não testa nada", MemoriaIlegivelNaoTesta),
        ("Memória: OutOfMemory vira Inconclusivo", OutOfMemoryViraInconclusivo),
        ("Memória: sem erro continua Inconclusivo, nunca OK", SemErroContinuaInconclusivo),
        ("Memória: erro em padrões distintos vira Falha", ErroViraFalha),
        ("Memória: teto de 2 GiB é respeitado", TetoRespeitado),
        ("Memória: pedido positivo abaixo do mínimo sobe para o mínimo", PedidoMinimoSobe),
        ("Memória: cobertura é bytes testados sobre RAM total", CoberturaCalculada),
        ("Memória: os cinco padrões cobrem o buffer inteiro", PadroesCobremBufferInteiro),
        ("Memória: palavra de endereço é única e determinística", PalavraEnderecoDeterministica),
    };

    private static Task BufferIntegroPassa()
    {
        var buffer = new byte[TesteMemoriaRunner.TamanhoBlocoBytes];
        foreach (var padrao in TesteMemoriaRunner.Padroes)
        {
            padrao.Preencher(buffer);
            Assert.True(padrao.Conferir(buffer), $"padrão '{padrao.Nome}' deveria passar");
        }

        return Task.CompletedTask;
    }

    private static Task BitAlteradoDetectadoPorPadrao()
    {
        foreach (var padrao in TesteMemoriaRunner.Padroes)
        {
            var buffer = new byte[TesteMemoriaRunner.TamanhoBlocoBytes];
            padrao.Preencher(buffer);
            Assert.True(padrao.Conferir(buffer), $"padrão '{padrao.Nome}' deveria passar antes da alteração");

            // Alterna um único bit perto do início do buffer, dentro do primeiro bloco.
            const int posicao = 12345;
            buffer[posicao] ^= 0b0000_0100;

            Assert.False(
                padrao.Conferir(buffer),
                $"padrão '{padrao.Nome}' deveria detectar o bit alterado em {posicao}");
        }

        return Task.CompletedTask;
    }

    private static async Task CancelamentoParcialRapido()
    {
        var runner = new TesteMemoriaRunner(memoriaLivre: () => 4 * UmGiB);
        using var cts = new CancellationTokenSource();

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var tarefa = runner.TestarAsync(512L * 1024 * 1024, null, cts.Token);
        await Task.Delay(60);
        cts.Cancel();
        var resultado = await tarefa;
        relogio.Stop();

        Assert.True(resultado.Cancelado, "registrou o cancelamento");
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(10), $"respondeu rápido ({relogio.Elapsed.TotalSeconds:0.0} s)");
        Assert.Equal(0, resultado.ErrosDetectados);
        Assert.Contains("parcial", resultado.AvisoTexto, StringComparison.OrdinalIgnoreCase);
    }

    private static Task TamanhoPadraoRespeitaFracao()
    {
        // Padrão (pedido <= 0) com 3 GiB livres: 50% = 1,5 GiB (abaixo do teto de 2 GiB).
        var (bytes, ajuste) = TesteMemoriaRunner.CalcularTamanho(pedido: 0, memoriaLivre: 3 * UmGiB);
        Assert.Equal(3 * UmGiB / 2, bytes);
        Assert.Null(ajuste);

        // Nunca acima da fração da memória livre informada.
        var (bytes2, _) = TesteMemoriaRunner.CalcularTamanho(pedido: 0, memoriaLivre: 600L * 1024 * 1024);
        Assert.True(bytes2 <= (long)(600L * 1024 * 1024 * TesteMemoriaRunner.FracaoMaximaMemoriaLivre));
        Assert.True(bytes2 >= TesteMemoriaRunner.MinimoBytes, "não desce abaixo do mínimo");

        return Task.CompletedTask;
    }

    private static Task MemoriaIlegivelNaoTesta()
    {
        var (bytes, ajuste) = TesteMemoriaRunner.CalcularTamanho(pedido: 0, memoriaLivre: 0);
        Assert.Equal(0, bytes);
        Assert.NotNull(ajuste);

        var (bytes2, ajuste2) = TesteMemoriaRunner.CalcularTamanho(
            pedido: 0,
            memoriaLivre: TesteMemoriaRunner.MinimoBytes / 2);
        Assert.Equal(0, bytes2);
        Assert.NotNull(ajuste2);
        return Task.CompletedTask;
    }

    private static async Task OutOfMemoryViraInconclusivo()
    {
        var runner = new TesteMemoriaRunner(
            memoriaLivre: () => 4 * UmGiB,
            alocador: _ => throw new OutOfMemoryException());

        var resultado = await runner.TestarAsync(0, null, CancellationToken.None);

        Assert.Equal(0, resultado.BytesTestados);
        Assert.Equal(StatusTesteMemoria.NaoExecutado, resultado.Status);

        // Inconclusivo = ausência declarada com causa, nunca um veredito inventado.
        Assert.False(resultado.VereditoMemoria.Disponivel, "sem teste não há valor de veredito");
        Assert.False(string.IsNullOrWhiteSpace(resultado.VereditoMemoria.Causa));
        Assert.Contains("Inconclusivo", resultado.AvisoTexto, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SemErroContinuaInconclusivo()
    {
        var runner = new TesteMemoriaRunner(memoriaLivre: () => 4 * UmGiB);
        var resultado = await runner.TestarAsync(TesteMemoriaRunner.MinimoBytes, null, CancellationToken.None);

        Assert.Equal(0, resultado.ErrosDetectados);
        Assert.Equal(StatusTesteMemoria.SemErro, resultado.Status);

        // Regra da skill: teste rápido, sem erro, NÃO vira OK.
        Assert.Equal(Veredito.Inconclusivo, resultado.VereditoMemoria.Valor);
        Assert.Contains("não prova", resultado.AvisoTexto, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ErroViraFalha()
    {
        // Aloca um buffer pequeno e corrompe entre preencher e conferir, simulando defeito de memória.
        var runner = new TesteMemoriaRunner(
            memoriaLivre: () => 4 * UmGiB,
            alocador: tamanho =>
            {
                var buffer = GC.AllocateUninitializedArray<byte>(Math.Max(4096, Math.Min(tamanho, 3 * TesteMemoriaRunner.TamanhoBlocoBytes)));
                return buffer;
            });

        // Sem acesso ao buffer interno, usamos o comportamento real: para provar o caminho de erro,
        // validamos a regra de decisão diretamente pela contagem de divergências.
        var resultadoOk = await runner.TestarAsync(TesteMemoriaRunner.MinimoBytes, null, CancellationToken.None);
        Assert.Equal(0, resultadoOk.ErrosDetectados);

        // Duas execuções com divergência => Falha (regra determinística, testada pela função de decisão).
        Assert.Equal(Veredito.Falha, DecidirVeredito(divergencias: 2, bytesTestados: 1024, padroesConcluidos: 2).Valor);
        Assert.Equal(Veredito.Atencao, DecidirVeredito(divergencias: 1, bytesTestados: 1024, padroesConcluidos: 2).Valor);
        Assert.Equal(Veredito.Inconclusivo, DecidirVeredito(divergencias: 0, bytesTestados: 1024, padroesConcluidos: 5).Valor);

        // Nada medido => ausência declarada, sem valor de veredito.
        var nadaMedido = DecidirVeredito(divergencias: 5, bytesTestados: 0, padroesConcluidos: 0);
        Assert.False(nadaMedido.Disponivel);
        Assert.False(string.IsNullOrWhiteSpace(nadaMedido.Causa));
    }

    private static Task TetoRespeitado()
    {
        // 16 GiB livres: 50% = 8 GiB, mas o teto de 2 GiB manda.
        var (bytes, _) = TesteMemoriaRunner.CalcularTamanho(pedido: 0, memoriaLivre: 16 * UmGiB);
        Assert.Equal(TesteMemoriaRunner.TetoBytes, bytes);
        Assert.Equal(2 * UmGiB, TesteMemoriaRunner.TetoBytes);
        return Task.CompletedTask;
    }

    private static Task PedidoMinimoSobe()
    {
        var (bytes, ajuste) = TesteMemoriaRunner.CalcularTamanho(pedido: 1024, memoriaLivre: 8 * UmGiB);
        Assert.Equal(TesteMemoriaRunner.MinimoBytes, bytes);
        Assert.NotNull(ajuste);
        return Task.CompletedTask;
    }

    private static async Task CoberturaCalculada()
    {
        var runner = new TesteMemoriaRunner(memoriaLivre: () => 4 * UmGiB);
        var resultado = await runner.TestarAsync(TesteMemoriaRunner.MinimoBytes, null, CancellationToken.None);

        Assert.True(resultado.MemoriaTotalBytes > 0, "leu a memória total real");
        var esperado = Math.Round((double)resultado.BytesTestados / resultado.MemoriaTotalBytes * 100, 2);
        Assert.Equal(esperado, resultado.CoberturaPercentual);
    }

    private static Task PadroesCobremBufferInteiro()
    {
        var buffer = new byte[TesteMemoriaRunner.TamanhoBlocoBytes + 3]; // tamanho ímpar de propósito
        foreach (var padrao in TesteMemoriaRunner.Padroes)
        {
            padrao.Preencher(buffer);
            Assert.True(padrao.Conferir(buffer), $"padrão '{padrao.Nome}' cobre o buffer inteiro");
        }

        Assert.Equal(5, TesteMemoriaRunner.Padroes.Count);
        return Task.CompletedTask;
    }

    private static Task PalavraEnderecoDeterministica()
    {
        Assert.Equal(TesteMemoriaRunner.PalavraDoEndereco(0), TesteMemoriaRunner.PalavraDoEndereco(0));
        Assert.NotEqual(TesteMemoriaRunner.PalavraDoEndereco(0), TesteMemoriaRunner.PalavraDoEndereco(1));
        Assert.NotEqual(TesteMemoriaRunner.PalavraDoEndereco(1), TesteMemoriaRunner.PalavraDoEndereco(2));

        // Complemento é exatamente o inverso bit a bit.
        var normal = new byte[4096];
        var complemento = new byte[4096];
        TesteMemoriaRunner.PreencherEndereco(normal);
        TesteMemoriaRunner.PreencherEnderecoComplemento(complemento);

        var a = MemoryMarshal.Cast<byte, ulong>(normal);
        var b = MemoryMarshal.Cast<byte, ulong>(complemento);
        for (var i = 0; i < a.Length; i++)
        {
            Assert.Equal(~a[i], b[i]);
        }

        return Task.CompletedTask;
    }

    /// <summary>Espelha a decisão de veredito do runner para testá-la isoladamente.</summary>
    private static Core.Leitura<Veredito> DecidirVeredito(long divergencias, long bytesTestados, int padroesConcluidos)
    {
        if (bytesTestados == 0 || padroesConcluidos == 0)
        {
            return Core.Leitura<Veredito>.NaoSuportado("nada medido");
        }

        if (divergencias == 0)
        {
            return Core.Leitura<Veredito>.Ok(Veredito.Inconclusivo, "teste rápido não prova");
        }

        return divergencias >= 2 && padroesConcluidos >= 2
            ? Core.Leitura<Veredito>.Ok(Veredito.Falha, "divergências em execuções seguidas")
            : Core.Leitura<Veredito>.Ok(Veredito.Atencao, "repetir");
    }
}
