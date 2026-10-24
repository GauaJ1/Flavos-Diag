using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FlavosDiag.Providers.Nativo;

/// <summary>
/// O <b>único</b> caminho pelo qual o Flavos Diag abre <c>\\.\PhysicalDriveN</c> com acesso de escrita e
/// envia um comando de pass-through ao disco. Duas invariantes ficam garantidas aqui, e por construção:
/// </summary>
/// <list type="number">
/// <item>
/// <b>Nenhum byte de comando vem de fora.</b> O chamador escolhe um <em>pedido</em> de uma enumeração
/// fechada (<see cref="PedidoAta"/>, <see cref="PedidoNvme"/>); os bytes do task file e o opcode são
/// montados aqui, a partir de <see cref="ComandosDiscoPermitidos"/>. Não existe parâmetro de comando.
/// </item>
/// <item>
/// <b>O handle é aberto somente depois das pré-condições.</b> A elevação é conferida e o aviso ao
/// usuário é exigido antes de qualquer <c>CreateFile</c> com <c>GENERIC_WRITE</c>.
/// </item>
/// </list>
/// <para>
/// O resto do aplicativo (leitura de descritor, tamanho, penalidade de busca, log NVMe) abre o disco
/// <b>somente</b> pelo <see cref="AbrirSomenteLeitura"/>, que não permite escrita e não aceita IOCTL de
/// pass-through. A varredura teste <c>ComandosDiscoPermitidosTests</c> trava essa arquitetura no
/// código-fonte.
/// </para>
/// <remarks>
/// Fundamento: <c>docs/DECISOES.md</c> D31 (o pass-through ATA exige <c>GENERIC_WRITE</c>) e D84 (esta
/// lista de permitidos). O comando enviado é de leitura; a permissão de escrita do handle é do driver,
/// não nossa — motivo pelo qual o comando passou a ser fechado em vez de repassado.
/// </remarks>
public static class RequisicaoDisco
{
    /// <summary>Compensação de nome de caminho longo; evita que o prefixo <c>\\.\</c> seja reinterpretado.</summary>
    private const int TamanhoBlocoAta = 512;

    /// <summary>
    /// <c>GENERIC_WRITE</c> (winioctl.h). Declarado aqui, e só aqui, porque este é o único arquivo
    /// autorizado a abrir o disco com acesso de escrita. Os outros acessos usam
    /// <see cref="AbrirSomenteLeitura"/> e <c>GENERIC_READ</c>.
    /// </summary>
    private const uint GenericWrite = 0x40000000;

    /// <summary>
    /// <c>IOCTL_ATA_PASS_THROUGH_DIRECT</c> (ntddscsi.h). Declarado aqui, e só aqui, porque é o único
    /// arquivo autorizado a enviar pass-through ATA ao disco.
    /// </summary>
    private const uint IoCtlAtaPassThroughDirect = 0x0004D030;

    /// <summary>
    /// <c>IOCTL_SCSI_MINIPORT</c> (ntddscsi.h), usado para o <c>Get Log Page</c> do NVMe. Declarado aqui,
    /// e só aqui, pela mesma razão.
    /// </summary>
    private const uint IoCtlScsiMiniport = 0x0004D008;

    /// <summary>Timeout do comando ATA, em segundos.</summary>
    private const uint TimeoutAtaSegundos = 10;

    /// <summary>Timeout do comando NVMe, em segundos.</summary>
    private const uint TimeoutNvmeSegundos = 10;

    /// <summary>Pedidos ATA permitidos. A enumeração é fechada: não há como pedir outro comando.</summary>
    public enum PedidoAta
    {
        /// <summary>ATA SMART READ DATA (bloco de atributos de 512 bytes).</summary>
        LerAtributosSmart = 1,

        /// <summary>ATA SMART READ THRESHOLDS (limiares dos atributos de 512 bytes).</summary>
        LerLimiaresSmart = 2,

        /// <summary>ATA IDENTIFY DEVICE (bloco de identificação de 512 bytes).</summary>
        Identificar = 3,
    }

    /// <summary>Pedidos NVMe permitidos (Admin Command Set, somente leitura).</summary>
    public enum PedidoNvme
    {
        /// <summary>NVMe Get Log Page da página SMART/Health (log 0x02).</summary>
        LerLogSaude = 1,
    }

    /// <summary>
    /// Pré-condições para um acesso de pass-through. Todas precisam ser verdadeiras; qualquer falsa
    /// impede a abertura do disco <b>antes</b> de qualquer <c>CreateFile</c>.
    /// </summary>
    public readonly record struct Permissoes(bool ProcessoElevado, bool UsuarioAutorizouLeitura)
    {
        /// <summary>Verdadeiro somente quando o processo está elevado e o usuário aceitou o aviso.</summary>
        public bool Permitido => ProcessoElevado && UsuarioAutorizouLeitura;
    }

    /// <summary>Resultado de um pedido de leitura a um disco.</summary>
    /// <param name="Sucesso">Verdadeiro quando o dispositivo respondeu com dados.</param>
    /// <param name="Dados">Bytes de saída (512 bytes nos pedidos ATA, tamanho do log no NVMe).</param>
    /// <param name="Erro">Motivo legível em pt-BR quando <paramref name="Sucesso"/> é falso.</param>
    public sealed record Resultado(bool Sucesso, byte[]? Dados, string? Erro);

    /// <summary>
    /// Executa um pedido ATA permitido. Recusa a chamada — sem abrir o disco — quando:
    /// o processo não está elevado, o usuário não autorizou a leitura, o número do disco é inválido ou o
    /// comando montado não consta da lista de permitidos.
    /// </summary>
    /// <param name="numeroDisco">Índice do disco físico (0, 1, …).</param>
    /// <param name="pedido">Pedido da enumeração fechada; <b>não</b> existe forma de passar um opcode.</param>
    /// <param name="permissoes">Elevação e consentimento conferidos pelo chamador.</param>
    public static Resultado Executar(int numeroDisco, PedidoAta pedido, Permissoes permissoes)
    {
        var recusa = Validar(numeroDisco, permissoes);
        if (recusa is not null)
        {
            return new Resultado(false, null, recusa);
        }

        // A lista de permitidos é conferida ANTES de qualquer abertura de disco: um pedido fora da
        // enumeração nunca chega a tocar o dispositivo.
        var comando = ComandoAta(pedido);
        if (ComandosDiscoPermitidos.DescreverAta(comando) is null)
        {
            return new Resultado(false, null, Recusa(comando));
        }

        using var dispositivo = Abrir(numeroDisco, acessoDeEscrita: true, out var erroAbertura);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            return new Resultado(false, null, erroAbertura);
        }

        return EnviarAta(dispositivo, pedido, comando);
    }

    /// <summary>
    /// Executa um pedido NVMe permitido. Recusa a chamada — sem abrir o disco — nas mesmas condições de
    /// <see cref="Executar(int, PedidoAta, Permissoes)"/>.
    /// </summary>
    public static Resultado Executar(int numeroDisco, PedidoNvme pedido, Permissoes permissoes)
    {
        var recusa = Validar(numeroDisco, permissoes);
        if (recusa is not null)
        {
            return new Resultado(false, null, recusa);
        }

        // Lista de permitidos antes de abrir o disco, como no caminho ATA.
        var opcode = OpcodeNvme(pedido);
        if (ComandosDiscoPermitidos.DescreverNvme(opcode) is null)
        {
            return new Resultado(false, null, Recusa(opcode));
        }

        using var dispositivo = Abrir(numeroDisco, acessoDeEscrita: true, out var erroAbertura);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            return new Resultado(false, null, erroAbertura);
        }

        return EnviarNvme(dispositivo, opcode);
    }

    /// <summary>
    /// Abre o disco <b>somente leitura</b>. É o único acesso permitido fora do pass-through: serve para
    /// os IOCTL de armazenamento do Windows (descritor, tamanho, penalidade de busca, previsão de falha),
    /// que não enviam comando ATA/NVMe ao dispositivo. Não existe sobrecarga que permita escrita.
    /// </summary>
    public static SafeFileHandle? AbrirSomenteLeitura(int numero, out string? erro) =>
        Abrir(numero, acessoDeEscrita: false, out erro);

    /// <summary>Monta o byte do registrador Command de um pedido ATA. Ponto único de tradução.</summary>
    /// <remarks>
    /// O código usa a forma tarefa de <c>SMART_RCV_DRIVE_DATA</c>, em que o byte <c>0xD0</c> (SMART READ
    /// DATA) vai no registrador Command — é o que o driver de disco do Windows repassa e o que D31
    /// documenta. A forma canônica da ATA/ATAPI-8 (<c>0xB0</c> em Command com o subcomando em Features)
    /// também é aceita pela lista, para o caso de a montagem mudar; hoje não é emitida.
    /// </remarks>
    private static byte ComandoAta(PedidoAta pedido) => pedido switch
    {
        PedidoAta.LerAtributosSmart => ComandosDiscoPermitidos.AtaSmartReadData,
        PedidoAta.LerLimiaresSmart => ComandosDiscoPermitidos.AtaSmart,
        PedidoAta.Identificar => ComandosDiscoPermitidos.AtaIdentifyDevice,
        _ => 0x00,
    };

    /// <summary>Subcomando SMART (registrador Features) do pedido; 0 quando o pedido não é SMART.</summary>
    private static byte SubcomandoSmart(PedidoAta pedido) => pedido switch
    {
        PedidoAta.LerAtributosSmart => ComandosDiscoPermitidos.AtaSmartReadData,
        PedidoAta.LerLimiaresSmart => ComandosDiscoPermitidos.AtaSmartReadThresholds,
        _ => 0x00,
    };

    /// <summary>Opcode NVMe (Admin Command Set) do pedido.</summary>
    private static byte OpcodeNvme(PedidoNvme pedido) => pedido switch
    {
        PedidoNvme.LerLogSaude => ComandosDiscoPermitidos.NvmeGetLogPage,
        _ => 0x00,
    };

    /// <summary>
    /// Confere as pré-condições (elevação, consentimento, número do disco). Devolve o motivo da recusa,
    /// ou <c>null</c> quando o pedido pode seguir para a conferência da lista de permitidos.
    /// </summary>
    private static string? Validar(int numeroDisco, Permissoes permissoes)
    {
        if (!permissoes.ProcessoElevado)
        {
            return "Leitura de pass-through recusada: o processo não está elevado. " +
                   "O Windows só repassa comandos ao disco para administrador.";
        }

        if (!permissoes.UsuarioAutorizouLeitura)
        {
            return "Leitura de pass-through recusada: o usuário não autorizou a leitura do disco.";
        }

        if (numeroDisco < 0)
        {
            return "Número de disco inválido: " + numeroDisco + ".";
        }

        return null;
    }

    /// <summary>Mensagem de recusa padronizada, citando o byte e a lista permitida.</summary>
    private static string Recusa(byte comando) =>
        $"Comando 0x{comando:X2} recusado: não consta da lista de comandos de leitura permitidos " +
        $"({string.Join(", ", ComandosDiscoPermitidos.DescricaoDaLista)}).";

    /// <summary>
    /// Envia o pedido ATA por <c>IOCTL_ATA_PASS_THROUGH_DIRECT</c>. O byte de comando já foi aceito pela
    /// lista; aqui só se monta o task file e se interpreta o resultado.
    /// </summary>
    private static Resultado EnviarAta(SafeFileHandle dispositivo, PedidoAta pedido, byte comando)
    {
        var tamanhoSaida = Marshal.SizeOf<AtaPassThroughDirect>();
        var buffer = Marshal.AllocHGlobal(tamanhoSaida);
        var dadosSaida = Marshal.AllocHGlobal(TamanhoBlocoAta);
        try
        {
            var pacote = new AtaPassThroughDirect
            {
                Length = (ushort)tamanhoSaida,
                AtaFlags = AtaFlags.AtaFlagsRead | AtaFlags.AtaFlagsDRDYRequired,
                DataTransferLength = TamanhoBlocoAta,
                TimeOutValue = TimeoutAtaSegundos,
                DataBufferOffset = dadosSaida,
                CurrentTaskFile = new byte[8],
                PreviousTaskFile = new byte[8],
            };

            // Task file do comando SMART (ATA/ATAPI-8):
            //   Features    = subcomando (0xD0 leitura de dados / 0xD1 leitura de limiares),
            //   SectorCount = 1 setor,
            //   LBA Low/Mid/High = 0x01 / 0x4F / 0xC2 (setor de dados SMART; 0x4F e 0xC2 são as chaves
            //               do SMART — SMART_LCYL_PASS / SMART_HCYL_PASS),
            //   Device/Head = 0xA0 (mestre),
            //   Command     = 0xD0 na forma tarefa de SMART_RCV_DRIVE_DATA (ver <see cref="ComandoAta"/>).
            pacote.CurrentTaskFile[0] = SubcomandoSmart(pedido);
            pacote.CurrentTaskFile[1] = 0x01;
            pacote.CurrentTaskFile[2] = 0x01;
            pacote.CurrentTaskFile[3] = 0x4F;
            pacote.CurrentTaskFile[4] = 0xC2;
            pacote.CurrentTaskFile[5] = 0xA0;
            pacote.CurrentTaskFile[6] = comando;

            Marshal.StructureToPtr(pacote, buffer, false);
            var ok = ApiNativas.DeviceIoControl(
                dispositivo,
                IoCtlAtaPassThroughDirect,
                buffer,
                tamanhoSaida,
                buffer,
                tamanhoSaida,
                out _,
                IntPtr.Zero);

            if (!ok)
            {
                return new Resultado(false, null, DescricaoErro(Marshal.GetLastWin32Error()));
            }

            var resultado = Marshal.PtrToStructure<AtaPassThroughDirect>(buffer);
            var bytes = new byte[TamanhoBlocoAta];
            Marshal.Copy(dadosSaida, bytes, 0, TamanhoBlocoAta);

            // Status 0x50/0x40 = comando concluído; erro no registrador → dados inválidos.
            var status = resultado.CurrentTaskFile[6];
            var erro = resultado.CurrentTaskFile[0];
            if (erro != 0 || (status & 0x81) != 0)
            {
                return new Resultado(false, null,
                    $"O dispositivo recusou o comando SMART (status 0x{status:X2}, erro 0x{erro:X2}).");
            }

            return new Resultado(true, bytes, null);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(dadosSaida);
        }
    }

    /// <summary>Envia o pedido NVMe por <c>IOCTL_SCSI_MINIPORT</c> com <c>SRB_IO_CONTROL</c>.</summary>
    private static Resultado EnviarNvme(SafeFileHandle dispositivo, byte opcode)
    {
        // SRB_IO_CONTROL (36 bytes) + STORAGE_PROTOCOL_COMMAND para Get Log Page.
        const int tamanhoSrb = 36;
        const int tamanhoComando = 64;
        const int tamanhoBufferSaida = 512;
        var tamanhoTotal = tamanhoSrb + tamanhoComando + tamanhoBufferSaida;

        var memoria = Marshal.AllocHGlobal(tamanhoTotal);
        try
        {
            for (var i = 0; i < tamanhoTotal; i++)
            {
                Marshal.WriteByte(memoria, i, 0);
            }

            // SRB_IO_CONTROL
            Marshal.WriteInt32(memoria, 0, tamanhoSrb + tamanhoBufferSaida);
            Marshal.WriteInt32(memoria, 4, 0x00);           // Signature: "NVME" em little-endian parcial
            Marshal.WriteByte(memoria, 4, (byte)'N');
            Marshal.WriteByte(memoria, 5, (byte)'V');
            Marshal.WriteByte(memoria, 6, (byte)'M');
            Marshal.WriteByte(memoria, 7, (byte)'E');
            Marshal.WriteInt32(memoria, 8, 0);              // Timeout
            Marshal.WriteInt32(memoria, 12, 0);             // ControlCode
            Marshal.WriteInt32(memoria, 16, 1);             // ReturnCode
            Marshal.WriteInt32(memoria, 20, tamanhoBufferSaida);
            Marshal.WriteInt32(memoria, 24, tamanhoComando + tamanhoBufferSaida);

            var comando = memoria + tamanhoSrb;

            // STORAGE_PROTOCOL_COMMAND: Version(4), Length(4), ProtocolType(4), Flags(4),
            // CommandLength(4), ErrorInfoLength(4), DataToDeviceTransferLength(4),
            // DataFromDeviceTransferLength(4), TimeOutValue(4), ErrorInfoOffset(4),
            // DataToDeviceBufferOffset(4), DataFromDeviceBufferOffset(4), CommandSpecific(16)
            Marshal.WriteInt32(comando + 0, 1);              // Version
            Marshal.WriteInt32(comando + 4, tamanhoComando); // Length
            Marshal.WriteInt32(comando + 8, 3);              // ProtocolType = NVMe
            Marshal.WriteInt32(comando + 12, 0x02);          // Flags = In
            Marshal.WriteInt32(comando + 16, 0x0040);        // CommandLength (subcomando NVMe)
            Marshal.WriteInt32(comando + 20, 0);             // ErrorInfoLength
            Marshal.WriteInt32(comando + 24, 0);             // DataToDeviceTransferLength
            Marshal.WriteInt32(comando + 28, tamanhoBufferSaida);
            Marshal.WriteInt32(comando + 32, (int)TimeoutNvmeSegundos);
            Marshal.WriteInt32(comando + 36, 0);             // ErrorInfoOffset
            Marshal.WriteInt32(comando + 40, 0);             // DataToDeviceBufferOffset
            Marshal.WriteInt32(comando + 44, tamanhoComando);// DataFromDeviceBufferOffset

            // NVMe command "Get Log Page": DW0 = opcode | fused, DW10 = número do log, DW11-12 = tamanho
            var subcomando = comando + 48;
            Marshal.WriteInt32(subcomando + 0, opcode);      // Opcode Get Log Page
            Marshal.WriteInt32(subcomando + 4, 0);           // NSID
            Marshal.WriteInt32(subcomando + 40, ComandosDiscoPermitidos.NvmeLogPageSmartHealth); // DW10
            Marshal.WriteInt32(subcomando + 44, (tamanhoBufferSaida + 1) / 4); // DW11: número de dwords

            var ok = ApiNativas.DeviceIoControl(
                dispositivo,
                IoCtlScsiMiniport,
                memoria,
                tamanhoTotal,
                memoria,
                tamanhoTotal,
                out var retornados,
                IntPtr.Zero);

            if (!ok || retornados < tamanhoSrb)
            {
                return new Resultado(false, null, DescricaoErro(Marshal.GetLastWin32Error()));
            }

            var codigoRetorno = Marshal.ReadInt32(memoria, 16);
            if (codigoRetorno != 0)
            {
                return new Resultado(false, null,
                    "O controlador não repassou o log de saúde NVMe (código " + codigoRetorno +
                    "). Em alguns modelos a leitura exige administrador.");
            }

            var dados = new byte[tamanhoBufferSaida];
            Marshal.Copy(memoria + tamanhoSrb + tamanhoComando, dados, 0, tamanhoBufferSaida);
            return new Resultado(true, dados, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new Resultado(false, null, "Falha ao consultar o log NVMe: " + ex.Message);
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    /// <summary>Abertura privada do disco: ponto único de <c>CreateFileW</c> em <c>\\.\PhysicalDriveN</c>.</summary>
    private static SafeFileHandle? Abrir(int numero, bool acessoDeEscrita, out string? erro)
    {
        erro = null;
        var caminho = @"\\.\PhysicalDrive" + numero;
        var acesso = ApiNativas.GENERIC_READ | (acessoDeEscrita ? GenericWrite : 0);
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

    /// <summary>Texto pt-BR para os códigos de erro do Windows observados neste caminho.</summary>
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

/// <summary>
/// <c>ATA_PASS_THROUGH_DIRECT</c> (ntddscsi.h). Vive aqui porque só o caminho único de
/// <see cref="RequisicaoDisco"/> monta um pass-through ATA.
/// </summary>
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
