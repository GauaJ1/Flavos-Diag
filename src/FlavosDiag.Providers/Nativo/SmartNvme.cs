using System.Runtime.InteropServices;

namespace FlavosDiag.Providers.Nativo;

/// <summary>
/// Leitura do log de saúde NVMe (NVMe Base Specification, "Get Log Page – SMART / Health Information").
/// Acesso pelo IOCTL_SCSI_MINIPORT com SRB_IO_CONTROL e assinatura do driver nativo, conforme a
/// documentação pública do Windows para NVMe.
/// </summary>
public static class SmartNvme
{
    /// <summary>Identificador do log de saúde SMART/Health do NVMe.</summary>
    public const byte LogPageSmartHealth = 0x02;

    private const uint IoCtlScsiMiniport = 0x0004D008;
    private const uint NvmeSignature = 0x4E564D45; // "NVME"

    /// <summary>
    /// Lê o log de saúde. Devolve <c>null</c> quando o dispositivo não expõe o log por esta via;
    /// o chamador registra a limitação com o motivo devolvido em <paramref name="motivo"/>.
    /// </summary>
    public static LogSaudeNvme? Ler(int numeroDisco, out string? motivo)
    {
        motivo = null;
        using var dispositivo = SmartAta.AbrirDisco(numeroDisco, escrita: false, out var erroAbertura);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            motivo = erroAbertura;
            return null;
        }

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
            Marshal.WriteInt32(comando + 0, 1);             // Version
            Marshal.WriteInt32(comando + 4, tamanhoComando); // Length
            Marshal.WriteInt32(comando + 8, 3);             // ProtocolType = NVMe
            Marshal.WriteInt32(comando + 12, 0x02);         // Flags = In
            Marshal.WriteInt32(comando + 16, 0x0040);       // CommandLength (subcomando NVMe)
            Marshal.WriteInt32(comando + 20, 0);            // ErrorInfoLength
            Marshal.WriteInt32(comando + 24, 0);            // DataToDeviceTransferLength
            Marshal.WriteInt32(comando + 28, tamanhoBufferSaida);
            Marshal.WriteInt32(comando + 32, 10);           // TimeOutValue (s)
            Marshal.WriteInt32(comando + 36, 0);            // ErrorInfoOffset
            Marshal.WriteInt32(comando + 40, 0);            // DataToDeviceBufferOffset
            Marshal.WriteInt32(comando + 44, tamanhoComando);// DataFromDeviceBufferOffset

            // NVMe command "Get Log Page": DW0 = opcode | fused, DW10 = número do log, DW11-12 = tamanho
            var subcomando = comando + 48;
            Marshal.WriteInt32(subcomando + 0, 0x02);       // Opcode Get Log Page
            Marshal.WriteInt32(subcomando + 4, 0);          // NSID
            Marshal.WriteInt32(subcomando + 40, LogPageSmartHealth); // DW10
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
                motivo = SmartAta.DescricaoErro(Marshal.GetLastWin32Error());
                return null;
            }

            var codigoRetorno = Marshal.ReadInt32(memoria, 16);
            if (codigoRetorno != 0)
            {
                motivo = "O controlador não repassou o log de saúde NVMe (código " + codigoRetorno +
                         "). Em alguns modelos a leitura exige administrador.";
                return null;
            }

            var dados = new byte[tamanhoBufferSaida];
            Marshal.Copy(memoria + tamanhoSrb + tamanhoComando, dados, 0, tamanhoBufferSaida);
            return Interpretar(dados);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            motivo = "Falha ao consultar o log NVMe: " + ex.Message;
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    /// <summary>
    /// Interpreta o log de saúde NVMe (função pura, testável com fixture de 512 bytes).
    /// Layout conforme a especificação: offsets em bytes a partir do início do log.
    /// </summary>
    public static LogSaudeNvme? Interpretar(byte[] dados)
    {
        if (dados is null || dados.Length < 512)
        {
            return null;
        }

        var avisoCritico = dados[0];
        var temperaturaKelvin = BitConverter.ToUInt16(dados, 1);
        var percentualUsado = dados[5];
        var espacoDisponivel = dados[3];
        var limiarEspaco = dados[4];
        var dadosLidos = BitConverter.ToUInt64(dados, 32) / 1000;      // unidades de 1000 setores *512B
        var dadosEscritos = BitConverter.ToUInt64(dados, 48) / 1000;
        var ciclos = BitConverter.ToUInt64(dados, 112);
        var horas = BitConverter.ToUInt64(dados, 128);
        var errosMidia = BitConverter.ToUInt64(dados, 160);

        return new LogSaudeNvme(
            AvisoCritico: avisoCritico,
            // "Sem leitura" precisa continuar sendo ausência: devolver 0 aqui viraria um "0 °C" real no laudo.
            TemperaturaCelsius: Temperatura.DeKelvinParaCelsius(temperaturaKelvin),
            PercentualVidaUsada: percentualUsado,
            EspacoDisponivelPercentual: espacoDisponivel,
            LimiarEspacoPercentual: limiarEspaco,
            DadosLidosGb: (long)dadosLidos,
            DadosEscritosGb: (long)dadosEscritos,
            HorasLigado: (long)horas,
            CiclosLigarDesligar: (long)ciclos,
            ErrosIntegridadeMidia: (long)errosMidia);
    }
}

/// <summary>Log de saúde NVMe já interpretado (unidades convertidas).</summary>
public sealed record LogSaudeNvme(
    int AvisoCritico,
    /// <summary>
    /// Temperatura em °C, ou <c>null</c> quando o controlador não informou uma leitura aproveitável
    /// (Kelvin &lt;= 273, sentinela 0xFFFF ou resultado fora de 1–120 °C). O <c>null</c> existe para que a
    /// ausência não seja confundida com uma medição real de 0 °C.
    /// </summary>
    int? TemperaturaCelsius,
    int PercentualVidaUsada,
    int EspacoDisponivelPercentual,
    int LimiarEspacoPercentual,
    long DadosLidosGb,
    long DadosEscritosGb,
    long HorasLigado,
    long CiclosLigarDesligar,
    long ErrosIntegridadeMidia)
{
    public bool PreveFalha => (AvisoCritico & (1 << 0)) != 0;

    public bool TemErroCritico => (AvisoCritico & 0xFE) != 0;
}
