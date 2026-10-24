using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.Workloads;

/// <summary>
/// Teste de memória próprio: escreve padrões conhecidos, lê de volta e confere palavra por palavra.
/// Não reinicia a máquina, não reserva mais do que uma fração segura da memória física livre e devolve
/// tudo ao final. Divergência entre o que foi escrito e o que foi lido = indício de defeito.
///
/// Os padrões são técnicas genéricas de teste de memória (decisão própria do projeto, ver
/// <c>docs/PROCEDENCIA.md</c>). Nenhum código de terceiros foi usado.
/// </summary>
public sealed class TesteMemoriaRunner : ITesteMemoriaProvider
{
    /// <summary>Fração máxima da memória física livre que o teste pode usar.</summary>
    public const double FracaoMaximaMemoriaLivre = 0.50;

    /// <summary>Teto absoluto de memória do teste, mesmo com muita RAM livre.</summary>
    public const long TetoBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>Piso do teste: abaixo disso o resultado não tem significado.</summary>
    public const long MinimoBytes = 256L * 1024 * 1024;

    /// <summary>Tamanho do bloco de trabalho: o cancelamento é verificado a cada bloco.</summary>
    public const int TamanhoBlocoBytes = 4 * 1024 * 1024;

    /// <summary>Constante do padrão de endereço (número ímpar, para espalhar os bits).</summary>
    public const ulong ConstanteEndereco = 0x9E37_79B9_7F4A_7C15UL;

    private readonly Func<long> _memoriaLivre;
    private readonly Func<int, byte[]> _alocador;

    /// <param name="memoriaLivre">Leitura da memória física livre; injetável para teste.</param>
    /// <param name="alocador">Alocador do buffer; injetável para testar o caminho de falta de memória.</param>
    public TesteMemoriaRunner(Func<long>? memoriaLivre = null, Func<int, byte[]>? alocador = null)
    {
        _memoriaLivre = memoriaLivre ?? MemoriaFisica.LivreBytes;
        _alocador = alocador ?? (tamanho => GC.AllocateUninitializedArray<byte>(tamanho));
    }

    /// <summary>
    /// Os cinco padrões, definidos uma única vez: nome, preenchimento e conferência.
    /// Um padrão de conferência é sempre derivado do mesmo cálculo do preenchimento.
    /// </summary>
    public static IReadOnlyList<PadraoMemoria> Padroes { get; } = new[]
    {
        new PadraoMemoria("0x00 (todos os bits em zero)", PreencherZeros, ConferirZeros),
        new PadraoMemoria("0xFF (todos os bits em um)", PreencherUns, ConferirUns),
        new PadraoMemoria("0x55/0xAA (bits alternados)", PreencherAlternado, ConferirAlternado),
        new PadraoMemoria("endereço vezes constante (64 bits)", PreencherEndereco, ConferirEndereco),
        new PadraoMemoria("complemento do endereço (64 bits)", PreencherEnderecoComplemento, ConferirEnderecoComplemento),
    };

    // ---- funções puras de preenchimento e conferência (testáveis isoladamente) ----

    public static void PreencherZeros(Span<byte> destino) => destino.Clear();

    public static bool ConferirZeros(ReadOnlySpan<byte> origem) => PrimeiraDiferencaConstante(origem, 0UL) < 0;

    public static void PreencherUns(Span<byte> destino) => destino.Fill(0xFF);

    public static bool ConferirUns(ReadOnlySpan<byte> origem) => PrimeiraDiferencaConstante(origem, ulong.MaxValue) < 0;

    public static void PreencherAlternado(Span<byte> destino)
    {
        for (var i = 0; i < destino.Length; i++)
        {
            destino[i] = (i & 1) == 0 ? (byte)0x55 : (byte)0xAA;
        }
    }

    public static bool ConferirAlternado(ReadOnlySpan<byte> origem)
    {
        for (var i = 0; i < origem.Length; i++)
        {
            if (origem[i] != ((i & 1) == 0 ? (byte)0x55 : (byte)0xAA))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Palavra de 64 bits única por endereço: <c>índice * constante</c>, sempre em unchecked.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong PalavraDoEndereco(long indice) => unchecked((ulong)indice * ConstanteEndereco);

    public static void PreencherEndereco(Span<byte> destino) => PreencherPalavras(destino, 0, complemento: false);

    public static bool ConferirEndereco(ReadOnlySpan<byte> origem) => LocalizarDiferencaPalavras(origem, 0, complemento: false) < 0;

    public static void PreencherEnderecoComplemento(Span<byte> destino) => PreencherPalavras(destino, 0, complemento: true);

    public static bool ConferirEnderecoComplemento(ReadOnlySpan<byte> origem) =>
        LocalizarDiferencaPalavras(origem, 0, complemento: true) < 0;

    private static void PreencherPalavras(Span<byte> destino, long deslocamentoBytes, bool complemento)
    {
        var palavras = MemoryMarshal.Cast<byte, ulong>(destino);
        var baseIndice = deslocamentoBytes / sizeof(ulong);
        for (var i = 0; i < palavras.Length; i++)
        {
            var v = PalavraDoEndereco(baseIndice + i);
            palavras[i] = complemento ? ~v : v;
        }

        // Sobra não múltipla de 8 bytes (só ocorre em buffers de teste): confere byte a byte.
        var resto = destino.Length % sizeof(ulong);
        if (resto > 0)
        {
            var baseByte = (long)palavras.Length * sizeof(ulong);
            for (var i = 0; i < resto; i++)
            {
                var v = PalavraDoEndereco(baseIndice + palavras.Length);
                var b = (byte)(v >> (8 * i));
                destino[(int)baseByte + i] = complemento ? (byte)~b : b;
            }
        }
    }

    /// <summary>Primeiro deslocamento em bytes com divergência, ou -1 se está íntegro.</summary>
    private static long LocalizarDiferencaPalavras(ReadOnlySpan<byte> origem, long deslocamentoBytes, bool complemento)
    {
        var palavras = MemoryMarshal.Cast<byte, ulong>(origem);
        var baseIndice = deslocamentoBytes / sizeof(ulong);
        for (var i = 0; i < palavras.Length; i++)
        {
            var v = PalavraDoEndereco(baseIndice + i);
            var esperado = complemento ? ~v : v;
            if (palavras[i] != esperado)
            {
                return deslocamentoBytes + ((long)i * sizeof(ulong));
            }
        }

        return -1;
    }

    /// <summary>
    /// Primeiro deslocamento com divergência para um padrão constante (0x00 / 0xFF), ou -1.
    /// Laço puro sobre <see cref="MemoryMarshal"/>: sem LINQ e sem alocação por chamada.
    /// </summary>
    private static long PrimeiraDiferencaConstante(ReadOnlySpan<byte> origem, ulong valor)
    {
        var palavras = MemoryMarshal.Cast<byte, ulong>(origem);
        for (var i = 0; i < palavras.Length; i++)
        {
            if (palavras[i] != valor)
            {
                return (long)i * sizeof(ulong);
            }
        }

        // Sobra não múltipla de 8 bytes.
        var resto = origem.Length % sizeof(ulong);
        if (resto > 0)
        {
            var baseByte = palavras.Length * sizeof(ulong);
            for (var i = 0; i < resto; i++)
            {
                var esperado = (byte)(valor >> (8 * i));
                if (origem[baseByte + i] != esperado)
                {
                    return baseByte + i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Calcula o tamanho do teste. <paramref name="pedido"/> &lt;= 0 significa "use o padrão":
    /// 50% da memória livre, respeitando o teto de 2 GiB e o piso de 256 MiB.
    /// Devolve também o motivo quando o tamanho foi ajustado (nunca ajustar em silêncio).
    /// </summary>
    public static (long Bytes, string? Ajuste) CalcularTamanho(long pedido, long memoriaLivre)
    {
        if (memoriaLivre <= 0)
        {
            return (0, "A memória física livre não pôde ser lida; nenhum teste é seguro.");
        }

        var limiteFracao = (long)(memoriaLivre * FracaoMaximaMemoriaLivre);
        var limite = Math.Min(limiteFracao, TetoBytes);
        limite -= limite % TamanhoBlocoBytes;

        if (limite < MinimoBytes)
        {
            return (0,
                $"Só há {FormatacaoBr.Bytes(memoriaLivre)} de memória física livre; o mínimo seguro para o teste " +
                $"é {FormatacaoBr.Bytes(MinimoBytes)}. Feche outros programas e tente novamente.");
        }

        if (pedido <= 0)
        {
            // Padrão = maior tamanho seguro, não o mínimo.
            return (limite, null);
        }

        if (pedido < MinimoBytes)
        {
            return (MinimoBytes, $"O tamanho pedido era menor que o mínimo; usei {FormatacaoBr.Bytes(MinimoBytes)}.");
        }

        if (pedido > limite)
        {
            return (limite,
                $"O tamanho pedido ({FormatacaoBr.Bytes(pedido)}) excede o máximo seguro com a memória livre atual " +
                $"({FormatacaoBr.Bytes(memoriaLivre)}); o teste foi limitado a {FormatacaoBr.Bytes(limite)}.");
        }

        return (pedido, null);
    }

    public Task<ResultadoTesteMemoria> TestarAsync(
        long bytes,
        IProgress<double>? progresso,
        CancellationToken cancelamento)
    {
        return Task.Run(() => Executar(bytes, progresso, cancelamento), cancelamento);
    }

    private ResultadoTesteMemoria Executar(long pedido, IProgress<double>? progresso, CancellationToken cancelamento)
    {
        var executadoEm = DateTimeOffset.Now;
        var relogio = Stopwatch.StartNew();
        var memoriaLivre = _memoriaLivre();
        var memoriaTotal = MemoriaFisica.TotalBytes();
        var (tamanho, ajuste) = CalcularTamanho(pedido, memoriaLivre);

        var avisos = new List<string>();
        if (ajuste is not null)
        {
            avisos.Add(ajuste);
        }

        if (tamanho == 0)
        {
            relogio.Stop();
            avisos.Add("Nada foi medido — resultado Inconclusivo.");
            return Inconclusivo(executadoEm, relogio.Elapsed, avisos, totalBytes: memoriaTotal, status: StatusTesteMemoria.NaoExecutado);
        }

        byte[]? buffer = null;
        long bytesTestados = 0;
        long palavrasDivergentes = 0;
        long primeiroDeslocamento = -1;
        var padroesConcluidos = new List<string>();
        var cancelado = false;
        var falhouAlocacao = false;

        try
        {
            buffer = _alocador((int)Math.Min(tamanho, int.MaxValue));
        }
        catch (OutOfMemoryException)
        {
            falhouAlocacao = true;
        }

        if (falhouAlocacao || buffer is null)
        {
            relogio.Stop();
            avisos.Add($"Não foi possível reservar {FormatacaoBr.Bytes(tamanho)} para o teste. Nada foi medido — Inconclusivo.");
            return Inconclusivo(executadoEm, relogio.Elapsed, avisos, memoriaTotal, StatusTesteMemoria.NaoExecutado);
        }

        try
        {
            for (var p = 0; p < Padroes.Count; p++)
            {
                var padrao = Padroes[p];
                var concluiuPadrao = true;

                // Preenche e confere bloco a bloco: o cancelamento responde em menos de 1 s.
                for (var inicio = 0; inicio < buffer.Length; inicio += TamanhoBlocoBytes)
                {
                    if (cancelamento.IsCancellationRequested)
                    {
                        cancelado = true;
                        concluiuPadrao = false;
                        break;
                    }

                    var fim = Math.Min(inicio + TamanhoBlocoBytes, buffer.Length);
                    padrao.Preencher(buffer.AsSpan(inicio, fim - inicio));
                }

                if (cancelado)
                {
                    break;
                }

                for (var inicio = 0; inicio < buffer.Length; inicio += TamanhoBlocoBytes)
                {
                    if (cancelamento.IsCancellationRequested)
                    {
                        cancelado = true;
                        concluiuPadrao = false;
                        break;
                    }

                    var fim = Math.Min(inicio + TamanhoBlocoBytes, buffer.Length);
                    var bloco = buffer.AsSpan(inicio, fim - inicio);

                    if (!padrao.Conferir(bloco))
                    {
                        var deslocamento = LocalizarDivergencia(padrao.Nome, buffer, inicio, bloco);
                        palavrasDivergentes++;
                        if (primeiroDeslocamento < 0)
                        {
                            primeiroDeslocamento = deslocamento;
                        }
                    }
                }

                if (!concluiuPadrao)
                {
                    break;
                }

                padroesConcluidos.Add(padrao.Nome);
                bytesTestados = buffer.Length;
                progresso?.Report((double)(p + 1) / Padroes.Count);
            }
        }
        finally
        {
            buffer = null;
            GC.Collect();
        }

        relogio.Stop();

        var cobertura = memoriaTotal > 0 ? (double)bytesTestados / memoriaTotal : 0;

        if (cancelado)
        {
            avisos.Add("Teste interrompido pelo usuário: resultado parcial, que não reprova a memória.");
        }

        StatusTesteMemoria status;
        Leitura<Veredito> leitura;

        if (bytesTestados == 0)
        {
            avisos.Add("Nenhum padrão foi concluído — nada foi medido, então o resultado é Inconclusivo.");
            status = StatusTesteMemoria.NaoExecutado;
            leitura = Leitura<Veredito>.NaoSuportado("Nenhum padrão foi concluído.");
        }
        else if (palavrasDivergentes == 0)
        {
            // Sem divergência NÃO vira OK: teste rápido não prova que a memória está boa.
            avisos.Add(
                "Nenhuma divergência nos padrões testados. Isso não prova que a memória está perfeita: é um teste " +
                "rápido, não um teste de estabilidade prolongado. Para conclusão definitiva, execute o teste de " +
                "memória completo no boot pelo Windows.");
            status = StatusTesteMemoria.SemErro;
            leitura = Leitura<Veredito>.Ok(Veredito.Inconclusivo, "teste rápido sem divergência não comprova integridade");
        }
        else if (palavrasDivergentes >= 2 && padroesConcluidos.Count >= 2)
        {
            avisos.Add(
                $"{palavrasDivergentes} divergência(s) em padrões distintos. Duas execuções seguidas com erro indicam " +
                "defeito na memória: troque o módulo e teste novamente.");
            status = StatusTesteMemoria.Erro;
            leitura = Leitura<Veredito>.Ok(Veredito.Falha, $"{palavrasDivergentes} divergência(s) em execuções seguidas");
        }
        else
        {
            avisos.Add(
                "Uma única divergência. Repita o teste antes de condenar o módulo: pode ser interferência de outro " +
                "programa, overclock ou um evento isolado.");
            status = StatusTesteMemoria.Erro;
            leitura = Leitura<Veredito>.Ok(Veredito.Atencao, "uma divergência: repetir o teste");
        }

        return new ResultadoTesteMemoria
        {
            ExecutadoEm = executadoEm,
            BytesTestados = bytesTestados,
            Padroes = padroesConcluidos,
            ErrosDetectados = palavrasDivergentes,
            Duracao = relogio.Elapsed,
            Cancelado = cancelado,
            AvisoTexto = string.Join(" ", avisos),
            PrimeiroDeslocamentoErro = primeiroDeslocamento >= 0 ? primeiroDeslocamento : null,
            CoberturaPercentual = Math.Round(cobertura * 100, 2),
            Status = status,
            VereditoMemoria = leitura,
            MemoriaLivreBytes = memoriaLivre,
            MemoriaTotalBytes = memoriaTotal,
        };
    }

    private static ResultadoTesteMemoria Inconclusivo(
        DateTimeOffset executadoEm,
        TimeSpan duracao,
        List<string> avisos,
        long totalBytes,
        StatusTesteMemoria status) => new()
        {
            ExecutadoEm = executadoEm,
            BytesTestados = 0,
            Padroes = Array.Empty<string>(),
            ErrosDetectados = 0,
            Duracao = duracao,
            Cancelado = false,
            AvisoTexto = string.Join(" ", avisos),
            PrimeiroDeslocamentoErro = null,
            CoberturaPercentual = 0,
            Status = status,
            VereditoMemoria = Leitura<Veredito>.NaoSuportado(string.Join(" ", avisos)),
            MemoriaLivreBytes = 0,
            MemoriaTotalBytes = totalBytes,
        };

    /// <summary>Deslocamento absoluto da primeira palavra divergente dentro do bloco conferido.</summary>
    private static long LocalizarDivergencia(string nomePadrao, byte[] buffer, int inicioBloco, Span<byte> bloco)
    {
        var esperado = new byte[bloco.Length];
        var alvo = esperado.AsSpan();
        Varrendo(nomePadrao, alvo);

        var palavrasBuffer = MemoryMarshal.Cast<byte, ulong>(bloco);
        var palavrasEsperado = MemoryMarshal.Cast<byte, ulong>(alvo);
        for (var i = 0; i < palavrasBuffer.Length; i++)
        {
            if (palavrasBuffer[i] != palavrasEsperado[i])
            {
                return inicioBloco + ((long)i * sizeof(ulong));
            }
        }

        return inicioBloco;

        void Varrendo(string nome, Span<byte> destino)
        {
            for (var i = 0; i < Padroes.Count; i++)
            {
                if (Padroes[i].Nome == nome)
                {
                    Padroes[i].Preencher(destino);
                    return;
                }
            }
        }
    }
}

/// <summary>Preenche um bloco com o padrão. Delegado próprio porque <see cref="Action{T}"/> não aceita <see cref="Span{T}"/>.</summary>
public delegate void PreenchedorBloco(Span<byte> destino);

/// <summary>Confere um bloco contra o padrão.</summary>
public delegate bool ConferidorBloco(ReadOnlySpan<byte> origem);

/// <summary>Um padrão de teste de memória: nome, preenchimento e conferência.</summary>
public sealed record PadraoMemoria(string Nome, PreenchedorBloco Preencher, ConferidorBloco Conferir);

/// <summary>Leitura da memória física do sistema, via <c>GlobalMemoryStatusEx</c>.</summary>
public static class MemoriaFisica
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    /// <summary>Memória física livre em bytes, ou 0 quando a leitura falha (nunca um valor inventado).</summary>
    public static long LivreBytes()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return 0;
        }

        return status.ullAvailPhys > long.MaxValue ? long.MaxValue : (long)status.ullAvailPhys;
    }

    /// <summary>Memória física total em bytes, ou 0 quando a leitura falha.</summary>
    public static long TotalBytes()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return 0;
        }

        return status.ullTotalPhys > long.MaxValue ? long.MaxValue : (long)status.ullTotalPhys;
    }
}
