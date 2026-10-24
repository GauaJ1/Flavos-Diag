using System.Runtime.InteropServices;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Providers.Nativo;

/// <summary>
/// Interpretação do bloco SMART de 512 bytes (ATA/ATAPI-8, seção de atributos SMART).
/// Função pura e testável: recebe os bytes, devolve os atributos interpretados. Nada aqui depende
/// de outro programa — os IDs e significados são os da especificação pública.
/// </summary>
public static class SmartAta
{
    /// <summary>IDs de atributo padronizados pela especificação que importam para o veredito.</summary>
    public const int IdTaxaErroLeitura = 0x01;

    public const int IdTemperatura = 0xC2;

    public const int IdSetoresRealocados = 0x05;

    public const int IdHorasLigado = 0x09;

    public const int IdCiclosLigarDesligar = 0x0C;

    public const int IdEventosRealocacao = 0xC4;

    public const int IdSetoresPendentes = 0xC5;

    public const int IdErrosNaoCorrigiveis = 0xC6;

    public const int IdTemperaturaAlternativa = 0xBE;

    /// <summary>
    /// Converte os 512 bytes do bloco SMART em atributos. Bloco inválido devolve lista vazia —
    /// o chamador trata "lista vazia" como leitura sem dados, nunca como disco saudável.
    /// </summary>
    public static IReadOnlyList<AtributoSmart> Interpretar(byte[] dados)
    {
        if (dados is null || dados.Length < 512)
        {
            return Array.Empty<AtributoSmart>();
        }

        var lista = new List<AtributoSmart>(24);
        for (var i = 0; i < 30; i++)
        {
            var baseOff = 2 + (i * 12);
            if (baseOff + 12 > 512)
            {
                break;
            }

            var id = dados[baseOff];
            if (id == 0)
            {
                continue;
            }

            var valor = dados[baseOff + 3];
            var pior = dados[baseOff + 4];
            var bruto = (long)dados[baseOff + 5]
                        | ((long)dados[baseOff + 6] << 8)
                        | ((long)dados[baseOff + 7] << 16)
                        | ((long)dados[baseOff + 10] << 24)
                        | ((long)dados[baseOff + 11] << 32);

            lista.Add(new AtributoSmart
            {
                Id = id,
                Nome = Nome(id),
                Valor = valor,
                Pior = pior,
                Limite = dados[baseOff + 2],
                Bruto = bruto,
                Interpretacao = InterpretarValor(id, bruto, valor),
            });
        }

        return lista;
    }

    /// <summary>Nome em português do atributo; IDs desconhecidos recebem rótulo neutro.</summary>
    public static string Nome(int id) => id switch
    {
        IdTaxaErroLeitura => "Taxa de erro de leitura",
        IdSetoresRealocados => "Setores realocados",
        IdHorasLigado => "Horas de uso",
        IdCiclosLigarDesligar => "Ciclos de ligar/desligar",
        IdTemperatura => "Temperatura",
        IdTemperaturaAlternativa => "Temperatura (sensor alternativo)",
        IdEventosRealocacao => "Eventos de realocação",
        IdSetoresPendentes => "Setores pendentes",
        IdErrosNaoCorrigiveis => "Erros não corrigíveis",
        _ => "Atributo " + id,
    };

    /// <summary>Texto curto do que o valor significa, no vocabulário do técnico.</summary>
    public static string InterpretarValor(int id, long bruto, int valorNormalizado) => id switch
    {
        IdSetoresRealocados when bruto > 0 => $"{bruto} setor(es) realocado(s) — indica desgaste da mídia.",
        IdSetoresRealocados => "Nenhum setor realocado.",
        IdSetoresPendentes when bruto > 0 => $"{bruto} setor(es) pendente(s) de remapeamento — risco de perda de dados.",
        IdSetoresPendentes => "Nenhum setor pendente.",
        IdErrosNaoCorrigiveis when bruto > 0 => $"{bruto} erro(s) não corrigível(is) registrado(s).",
        IdErrosNaoCorrigiveis => "Nenhum erro não corrigível.",
        IdTemperatura or IdTemperaturaAlternativa => $"{bruto} °C",
        IdHorasLigado => $"{bruto} hora(s) ligado",
        IdCiclosLigarDesligar => $"{bruto} ciclo(s)",
        _ => $"Valor normalizado {valorNormalizado}, bruto {bruto}",
    };

    /// <summary>
    /// Lê o bloco SMART por IOCTL_ATA_PASS_THROUGH_DIRECT (comando ATA SMART READ DATA).
    /// Só funciona em dispositivos ATA/ATAPI que exponham o comando; a falha devolve o motivo.
    /// </summary>
    /// <remarks>
    /// O handle precisa ser aberto com <c>GENERIC_WRITE</c> mesmo para um comando de leitura: o driver de
    /// armazenamento do Windows só repassa o pass-through ATA a handles com acesso de escrita.
    /// Com acesso somente-leitura o IOCTL falha com ERROR_ACCESS_DENIED (5). Isso <b>não</b> escreve nada:
    /// o comando enviado é SMART READ DATA (0xD0), sem modificação — a restrição é do driver, não nossa.
    /// </remarks>
    public static LeituraSmartBruta LerSmartAta(int numeroDisco)
    {
        using var dispositivo = AbrirDisco(numeroDisco, escrita: true, out var erroAbertura);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            return new LeituraSmartBruta(false, null, erroAbertura);
        }

        var tamanhoSaida = Marshal.SizeOf<AtaPassThroughDirect>();
        var buffer = Marshal.AllocHGlobal(tamanhoSaida);
        var dadosSaida = Marshal.AllocHGlobal(512);
        try
        {
            var pacote = new AtaPassThroughDirect
            {
                Length = (ushort)tamanhoSaida,
                AtaFlags = AtaFlags.AtaFlagsRead | AtaFlags.AtaFlagsDRDYRequired,
                DataTransferLength = 512,
                TimeOutValue = 10,
                DataBufferOffset = dadosSaida,
                CurrentTaskFile = new byte[8],
                PreviousTaskFile = new byte[8],
            };
            // Task file do comando ATA SMART READ DATA (ATA/ATAPI-8):
            //   Feature  = 0xD8 quando é o pedido de status; para ler atributos usa-se Feature = 0xD8
            //              apenas no RETURN STATUS. Na leitura de dados: Features=0x00,
            //   SectorCount = 0x01 (lê 1 setor),
            //   LBA Low  = 0xC2 ou 0x00 (endereço do setor SMARTCYLINDER, 1-based; use 1 → 0x01 LBA Low
            //              para o bloco de dados do primeiro setor),
            //   Command  = 0xD0 (SMART READ DATA).
            // Erro 0x04 (ABRT) indica task file incompleto: todos os registradores devem estar coerentes.
            pacote.CurrentTaskFile[0] = 0x00; // Features
            pacote.CurrentTaskFile[1] = 0x01; // SectorCount = 1 setor
            pacote.CurrentTaskFile[2] = 0x01; // LBA Low = 1 (setor de dados SMART)
            pacote.CurrentTaskFile[3] = 0x4F; // LBA Mid
            pacote.CurrentTaskFile[4] = 0xC2; // LBA High
            pacote.CurrentTaskFile[5] = 0xA0; // Device/Head
            pacote.CurrentTaskFile[6] = ApiNativas.AtaSmartReadData;

            Marshal.StructureToPtr(pacote, buffer, false);
            var ok = ApiNativas.DeviceIoControl(
                dispositivo,
                ApiNativas.IOCTL_ATA_PASS_THROUGH_DIRECT,
                buffer,
                tamanhoSaida,
                buffer,
                tamanhoSaida,
                out _,
                IntPtr.Zero);

            if (!ok)
            {
                var codigo = Marshal.GetLastWin32Error();
                return new LeituraSmartBruta(false, null, DescricaoErro(codigo));
            }

            var resultado = Marshal.PtrToStructure<AtaPassThroughDirect>(buffer);
            var bytes = new byte[512];
            Marshal.Copy(dadosSaida, bytes, 0, 512);

            // Status 0x50/0x40 = comando concluído; erro no registrador → dados inválidos.
            var status = resultado.CurrentTaskFile[6];
            var erro = resultado.CurrentTaskFile[0];
            if (erro != 0 || (status & 0x81) != 0)
            {
                return new LeituraSmartBruta(false, null,
                    $"O dispositivo recusou o comando SMART (status 0x{status:X2}, erro 0x{erro:X2}).");
            }

            return new LeituraSmartBruta(true, bytes, null);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(dadosSaida);
        }
    }

    /// <summary>
    /// SMART predito pelo Windows (IOCTL_STORAGE_PREDICT_FAILURE). Funciona em muitos dispositivos
    /// USB e RAID onde o IOCTL ATA não passa; devolve apenas "prevê falha sim/não".
    /// </summary>
    public static LeituraSmartBruta LerPredicaoWindows(int numeroDisco)
    {
        using var dispositivo = AbrirDisco(numeroDisco, escrita: false, out var erroAbertura);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            return new LeituraSmartBruta(false, null, erroAbertura);
        }

        var tamanhoSaida = Marshal.SizeOf<STORAGE_PREDICT_FAILURE_SAIDA>();
        var pontSaida = Marshal.AllocHGlobal(tamanhoSaida);
        try
        {
            var saida = STORAGE_PREDICT_FAILURE_SAIDA.Criar();
            Marshal.StructureToPtr(saida, pontSaida, false);

            // IOCTL_STORAGE_PREDICT_FAILURE não recebe buffer de entrada.
            var ok = ApiNativas.DeviceIoControl(
                dispositivo,
                ApiNativas.IOCTL_STORAGE_PREDICT_FAILURE,
                IntPtr.Zero,
                0,
                pontSaida,
                tamanhoSaida,
                out var retornados,
                IntPtr.Zero);

            if (!ok || retornados == 0)
            {
                return new LeituraSmartBruta(false, null, DescricaoErro(Marshal.GetLastWin32Error()));
            }

            var lido = Marshal.PtrToStructure<STORAGE_PREDICT_FAILURE_SAIDA>(pontSaida);
            var preveFalha = lido.PredictFailure == ApiNativas.PredictFailureIsFailurePredicted;
            var bloco = new byte[512];
            bloco[0] = preveFalha ? (byte)1 : (byte)0;
            return new LeituraSmartBruta(true, bloco, null);
        }
        finally
        {
            Marshal.FreeHGlobal(pontSaida);
        }
    }

    internal static Microsoft.Win32.SafeHandles.SafeFileHandle? AbrirDisco(int numero, bool escrita, out string? erro)
    {
        erro = null;
        var caminho = @"\\.\PhysicalDrive" + numero;
        var acesso = ApiNativas.GENERIC_READ | (escrita ? ApiNativas.GENERIC_WRITE : 0);
        var alca = ApiNativas.CreateFileW(
            caminho,
            acesso,
            ApiNativas.FILE_SHARE_READ | ApiNativas.FILE_SHARE_WRITE,
            IntPtr.Zero,
            ApiNativas.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (alca.IsInvalid)
        {
            var codigo = Marshal.GetLastWin32Error();
            erro = codigo == 5
                ? "Acesso negado ao dispositivo: execute como administrador para ler o SMART."
                : DescricaoErro(codigo);
            alca.Dispose();
            return null;
        }

        return alca;
    }

    internal static string DescricaoErro(int codigo) => codigo switch
    {
        5 => "Acesso negado pelo Windows: o pass-through ATA exige executar como administrador.",
        2 => "Dispositivo não encontrado.",
        1 => "Função não suportada por este dispositivo.",
        50 => "A operação não é suportada por este dispositivo (comum em adaptadores USB).",
        1117 => "Falha de E/S ao ler o dispositivo.",
        87 => "Parâmetro inválido para este dispositivo.",
        _ => "Falha do Windows ao consultar o dispositivo (código " + codigo + ").",
    };
}

[StructLayout(LayoutKind.Sequential)]
internal struct AtaPassThroughDirect
{
    public ushort Length;
    public ushort AtaFlags;
    public byte PathId;
    public byte TargetId;
    public byte Lun;
    public byte ReservedAsUchar;
    public uint DataTransferLength;
    public uint TimeOutValue;
    public uint ReservedAsUlong;
    public IntPtr DataBufferOffset;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] PreviousTaskFile;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] CurrentTaskFile;
}
