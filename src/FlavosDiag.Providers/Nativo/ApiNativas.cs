using static FlavosDiag.Providers.Nativo.ApiNativas;

namespace FlavosDiag.Providers.Nativo;

/// <summary>
/// Declarações P/Invoke usadas pelos providers. Todas as estruturas e códigos vêm da documentação
/// pública de armazenamento do Windows (DeviceIoControl / IOCTL_* / STORAGE_PROPERTY_QUERY),
/// do setor ATA e da especificação NVMe. Nenhuma constante aqui é "descoberta" observando outro programa.
/// </summary>
internal static class ApiNativas
{
    // ---- Acesso a arquivos de dispositivo ----
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;

    // ---- IOCTL de armazenamento (winioctl.h) ----
    public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
    public const uint IOCTL_STORAGE_PREDICT_FAILURE = 0x002D1100;
    public const uint IOCTL_ATA_PASS_THROUGH = 0x0004D02C;
    public const uint IOCTL_ATA_PASS_THROUGH_DIRECT = 0x0004D030;
    public const uint IOCTL_SCSI_MINIPORT = 0x0004D008;
    public const uint IOCTL_DISK_GET_LENGTH_INFO = 0x0007405C;
    public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;

    // ---- Propriedades de armazenamento (STORAGE_PROPERTY_ID) ----
    public const int StorageDeviceProperty = 0;
    public const int StorageDeviceSeekPenaltyProperty = 7;

    public const int PropertyStandardQuery = 0;

    public const byte AtaDeviceProtocol = 3;

    // ---- Compatibilidade de barramento (STORAGE_BUS_TYPE) ----
    public const byte BusTypeAta = 3;
    public const byte BusTypeAtapi = 4;
    public const byte BusTypeScsi = 1;
    public const byte BusTypeUsb = 7;
    public const byte BusTypeRAID = 8;
    public const byte BusTypeSata = 11;
    public const byte BusTypeSas = 10;
    public const byte BusTypeNvme = 17;
    public const byte BusTypeSd = 12;
    public const byte BusTypeMmc = 13;
    public const byte BusTypeUfs = 20;
    public const byte BusTypeVirtual = 14;

    // ---- Status SMART predito (IOCTL_STORAGE_PREDICT_FAILURE) ----
    public const int PredictFailureIsFailurePredicted = 1;

    // ---- Comandos ATA (ATA/ATAPI-8 e revisões) ----
    public const byte AtaSmartDataInputCandidate = 0xD1;
    public const byte AtaSmartReturnStatus = 0xDA;
    public const byte AtaIdentifyDeviceCommand = 0xEC;
    public const byte AtaSmartReadData = 0xD0;
    public const ushort AtaSmartReadDataInputFeatures = 0x0001;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    internal static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(
        string nomeArquivo,
        uint acessoDesejado,
        uint compartilhamento,
        IntPtr atributosSeguranca,
        uint criacao,
        uint atributosEFlags,
        IntPtr modelo);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(
        Microsoft.Win32.SafeHandles.SafeFileHandle dispositivo,
        uint codigo,
        IntPtr entrada,
        int tamanhoEntrada,
        IntPtr saida,
        int tamanhoSaida,
        out int bytesRetornados,
        IntPtr sobreposto);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool GetDiskFreeSpaceExW(
        string nomePasta,
        out ulong espacoDisponivelChamador,
        out ulong espacoTotal,
        out ulong espacoTotalLivre);
}

/// <summary>
/// Estruturas de armazenamento da documentação pública. O alinhamento segue os cabeçalhos oficiais
/// (SequentialStructLayout com o tamanho declarado no primeiro campo).
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct STORAGE_PROPERTY_QUERY
{
    public int PropertyId;
    public int QueryType;
    public byte AdditionalParameters;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct STORAGE_DEVICE_DESCRIPTOR_CABECALHO
{
    public int Version;
    public int Size;
    public byte DeviceType;
    public byte DeviceTypeModifier;
    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)]
    public bool RemovableMedia;
    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)]
    public bool CommandQueueing;
    public int VendorIdOffset;
    public int ProductIdOffset;
    public int ProductRevisionOffset;
    public int SerialNumberOffset;
    public byte BusType;
    public int RawPropertiesLength;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct DEVICE_SEEK_PENALTY_DESCRIPTOR
{
    public int Version;
    public int Size;
    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)]
    public bool IncursSeekPenalty;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct STORAGE_PREDICT_FAILURE_SAIDA
{
    public int PredictFailure;
    public int VendorSpecific; // 512 bytes, primeiros 4 no cabeçalho

    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 508)]
    public byte[] DadosVendor;

    public static STORAGE_PREDICT_FAILURE_SAIDA Criar() => new()
    {
        DadosVendor = new byte[508],
    };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct ATA_PASS_THROUGH_EX
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

    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] PreviousTaskFile;

    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] CurrentTaskFile;

    public static ATA_PASS_THROUGH_EX Criar() => new()
    {
        PreviousTaskFile = new byte[8],
        CurrentTaskFile = new byte[8],
    };
}

internal static class AtaFlags
{
    public const ushort AtaFlagsRead = 0x02;
    public const ushort AtaFlagsAtaCommand16 = 0x01;
    public const ushort AtaFlagsDRDYRequired = 0x04;
    public const ushort AtaFlagsByteBlock = 0x08;
}

/// <summary>Resultado bruto de uma leitura SMART (512 bytes do bloco de dados do atributo).</summary>
public sealed record LeituraSmartBruta(bool Sucesso, byte[]? Dados512, string? Erro);

/// <summary>Identificação do dispositivo obtida pelas APIs de armazenamento do Windows.</summary>
public sealed record IdentificacaoDispositivo(
    int Numero,
    string? Modelo,
    string? Firmware,
    string? Serial,
    byte TipoBarramento,
    long TamanhoBytes,
    bool? IncorrePenalidadeBusca,
    Core.Modelos.TipoMidia? MidiaStorage = null,
    string? SaudeStorage = null);
