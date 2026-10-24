using System.Text.Json;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Fakes;

namespace FlavosDiag.App.Services;

/// <summary>
/// Fixture WMI DE DEMONSTRAÇÃO. Valores fictícios usados somente no modo demonstração (--modo-demo),
/// nunca como leitura real. A interface exibe a faixa "Modo demonstração".
/// </summary>
public static class Recursos
{
    public static readonly string FixtureWmi = JsonSerializer.Serialize(new Dictionary<string, object[]>
    {
        ["Win32_OperatingSystem"] = new object[]
        {
            new Dictionary<string, object?> { ["Caption"] = "Microsoft Windows 11 Pro (demonstração)", ["Version"] = "10.0.26100", ["BuildNumber"] = "26100", ["OSArchitecture"] = "64-bit", ["RegisteredUser"] = "Técnico (demonstração)", ["InstallDate"] = "20250314000000.000000+000" },
        },
        ["SoftwareLicensingProduct"] = new object[] { new Dictionary<string, object?> { ["LicenseStatus"] = 1 } },
        ["Win32_BIOS"] = new object[] { new Dictionary<string, object?> { ["Manufacturer"] = "Fabricante de demonstração", ["SMBIOSBIOSVersion"] = "1.24", ["ReleaseDate"] = "20250110000000.000000+000" } },
        ["Win32_BaseBoard"] = new object[] { new Dictionary<string, object?> { ["Manufacturer"] = "Demonstração", ["Product"] = "Placa de exemplo X570 (fictícia)", ["Version"] = "Rev 1.0", ["SerialNumber"] = "DEMO-SN-000000123456" } },
        ["Win32_Processor"] = new object[] { new Dictionary<string, object?> { ["Name"] = "Processador de demonstração 8 núcleos", ["Manufacturer"] = "Fabricante de demonstração", ["NumberOfCores"] = 8, ["NumberOfLogicalProcessors"] = 16, ["MaxClockSpeed"] = 3800, ["L2CacheSize"] = 4096, ["L3CacheSize"] = 32768, ["AddressWidth"] = 64 } },
        ["Win32_PhysicalMemory"] = new object[]
        {
            new Dictionary<string, object?> { ["Capacity"] = 17179869184L, ["Speed"] = 3200, ["ConfiguredClockSpeed"] = 3200, ["SMBIOSMemoryType"] = 26, ["Manufacturer"] = "Fabricante de demonstração", ["PartNumber"] = "DEMO-16GB-3200", ["SerialNumber"] = "DIMMSN-DEMO-0001", ["DeviceLocator"] = "DIMM_A1" },
            new Dictionary<string, object?> { ["Capacity"] = 17179869184L, ["Speed"] = 3200, ["ConfiguredClockSpeed"] = 3200, ["SMBIOSMemoryType"] = 26, ["Manufacturer"] = "Fabricante de demonstração", ["PartNumber"] = "DEMO-16GB-3200", ["SerialNumber"] = "DIMMSN-DEMO-0002", ["DeviceLocator"] = "DIMM_A2" },
        },
        ["Win32_PhysicalMemoryArray"] = new object[] { new Dictionary<string, object?> { ["MemoryDevices"] = 4 } },
        ["Win32_VideoController"] = new object[] { new Dictionary<string, object?> { ["Name"] = "GPU de demonstração", ["AdapterCompatibility"] = "Fabricante de demonstração", ["DriverVersion"] = "31.0.15.0000", ["DriverDate"] = "20250601000000.000000+000" } },
        ["Win32_Battery"] = Array.Empty<object>(),
        ["Win32_PnPEntity"] = Array.Empty<object>(),
        ["Win32_NetworkAdapter"] = new object[]
        {
            new Dictionary<string, object?> { ["NetConnectionID"] = "Ethernet", ["Description"] = "Adaptador de demonstração", ["MACAddress"] = "02:00:00:00:00:01", ["Speed"] = 1000000000L, ["AdapterTypeID"] = 0, ["PhysicalAdapter"] = true, ["NetConnectionStatus"] = 2 },
        },
    });
}
