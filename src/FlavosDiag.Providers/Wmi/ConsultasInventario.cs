namespace FlavosDiag.Providers.Wmi;

/// <summary>
/// Conjunto nomeado de consultas WMI usadas no inventário. Compartilhado entre a implementação real e o
/// fake (fixtures), garantindo que as fixtures correspondam exatamente à consulta real.
/// </summary>
public static class ConsultasInventario
{
    public const string Operacao = "Win32_OperatingSystem";
    public const string Ativacao = "SoftwareLicensingProduct";
    public const string Bios = "Win32_BIOS";
    public const string PlacaMae = "Win32_BaseBoard";
    public const string Cpu = "Win32_Processor";
    public const string Memoria = "Win32_PhysicalMemory";
    public const string MemoriaArray = "Win32_PhysicalMemoryArray";
    public const string Video = "Win32_VideoController";
    public const string Bateria = "Win32_Battery";
    public const string Dispositivos = "Win32_PnPEntity";
    public const string AdaptadoresRede = "Win32_NetworkAdapter";

    /// <summary>Classe → colunas exigidas (o loader é tolerante a colunas extras).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Colunas = new Dictionary<string, string[]>
    {
        [Operacao] = new[] { "Caption", "DisplayVersion", "Version", "BuildNumber", "OSArchitecture", "RegisteredUser", "InstallDate" },
        [Ativacao] = new[] { "LicenseStatus" },
        [Bios] = new[] { "Manufacturer", "SMBIOSBIOSVersion", "Version", "ReleaseDate" },
        [PlacaMae] = new[] { "Manufacturer", "Product", "Version", "SerialNumber" },
        [Cpu] = new[] { "Name", "Manufacturer", "NumberOfCores", "NumberOfLogicalProcessors", "MaxClockSpeed", "L2CacheSize", "L3CacheSize", "AddressWidth" },
        [Memoria] = new[] { "Capacity", "Speed", "ConfiguredClockSpeed", "MemoryType", "SMBIOSMemoryType", "Manufacturer", "PartNumber", "SerialNumber", "DeviceLocator", "BankLabel" },
        [MemoriaArray] = new[] { "MemoryDevices" },
        [Video] = new[] { "Name", "AdapterCompatibility", "DriverVersion", "DriverDate" },
        [Bateria] = new[] { "Name", "Manufacturer" },
        [Dispositivos] = new[] { "Name", "ConfigManagerErrorCode", "Manufacturer", "DeviceID" },
        [AdaptadoresRede] = new[] { "NetConnectionID", "Description", "MACAddress", "Speed", "AdapterTypeID", "PhysicalAdapter", "NetConnectionStatus" },
    };
}
