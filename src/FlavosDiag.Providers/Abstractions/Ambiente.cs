using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace FlavosDiag.Providers.Abstractions;

/// <summary>Acesso ao sistema — atrás de interface para permitir testes do inventário sem depender da máquina.</summary>
public interface IAmbiente
{
    bool EhAdministrador { get; }

    bool ModoDemonstracao { get; }

    string PastaUsuario { get; }
}

public sealed class AmbienteSistema : IAmbiente
{
    public string PastaUsuario { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlavosDiag");

    public bool ModoDemonstracao { get; init; }

    public bool EhAdministrador
    {
        get
        {
            try
            {
                using var identidade = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identidade);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

/// <summary>Leitura simplificada do registro (somente leitura).</summary>
public static class LeitorRegistro
{
    public static string? LerString(string subChave, string valor, RegistryHive hive = RegistryHive.LocalMachine)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var chave = baseKey.OpenSubKey(subChave, writable: false);
            return chave?.GetValue(valor, null) as string ?? chave?.GetValue(valor)?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>P/Invoke mínimos de tela (EnumDisplaySettings).</summary>
public static class ApiTela
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    public const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    public static DEVMODE ModoAtual(string? dispositivo)
    {
        var dm = new DEVMODE
        {
            dmDeviceName = string.Empty,
            dmFormName = string.Empty,
            dmSize = (ushort)Marshal.SizeOf<DEVMODE>(),
        };
        return EnumDisplaySettings(dispositivo, ENUM_CURRENT_SETTINGS, ref dm) ? dm : default;
    }
}
