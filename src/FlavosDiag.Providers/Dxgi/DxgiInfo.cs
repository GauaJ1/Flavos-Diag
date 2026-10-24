using System.Runtime.InteropServices;

namespace FlavosDiag.Providers.Dxgi;

public sealed record AdaptadorDxgi(string Descricao, long VramDedicadaBytes, uint VendorId, ulong Luid, bool EhSoftware);

/// <summary>Informação de adaptadores via DXGI (documentação pública do DirectX Graphics Infrastructure).</summary>
public interface IDxgiInfo
{
    IReadOnlyList<AdaptadorDxgi> ListarAdaptadores();
}

/// <summary>
/// Enumera adaptadores com IDXGIFactory1 → IDXGIAdapter1 → GetDesc1, lendo DedicatedVideoMemory em bytes.
/// Interop COM mínimo e próprio (sem bibliotecas externas).
/// </summary>
public sealed class DxgiInfo : IDxgiInfo
{
    private static readonly Guid IdxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private static readonly Guid IdxgiAdapter1 = new("29038f61-3839-4626-91fd-086879011a05");

    public IReadOnlyList<AdaptadorDxgi> ListarAdaptadores()
    {
        var lista = new List<AdaptadorDxgi>();
        object? fabricaObj = null;
        try
        {
            var tipo = Type.GetTypeFromCLSID(new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369"), throwOnError: true)!;
            fabricaObj = Activator.CreateInstance(tipo);
            var fabrica = (IDXGIFactory1?)fabricaObj;
            if (fabrica is null)
            {
                return lista;
            }

            for (uint i = 0; ; i++)
            {
                var hr = fabrica.EnumAdapters1(i, out var adaptador);
                if (hr != 0 || adaptador is null)
                {
                    break;
                }

                try
                {
                    adaptador.GetDesc1(out var desc);
                    var nome = desc.Description?.TrimEnd('\0') ?? string.Empty;
                    var ehSoftware = (desc.Flags & 2) != 0; // DXGI_ADAPTER_FLAG_SOFTWARE
                    lista.Add(new AdaptadorDxgi(nome, Math.Max(0L, (long)desc.DedicatedVideoMemory), desc.VendorId, desc.AdapterLuid, ehSoftware));
                }
                finally
                {
                    Marshal.ReleaseComObject(adaptador);
                }
            }
        }
        catch (Exception)
        {
            // Sem DXGI (RDP/sessão sem GPU): lista vazia; quem chama registra a limitação.
        }
        finally
        {
            if (fabricaObj is not null)
            {
                Marshal.ReleaseComObject(fabricaObj);
            }
        }

        return lista;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public ulong AdapterLuid;
        public uint Flags;
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDXGIFactory1
    {
        int EnumAdapters(uint adapter, out IntPtr ppAdapter);

        [PreserveSig]
        int MakeWindowAssociation(IntPtr windowHandle, uint flags);

        [PreserveSig]
        int GetWindowAssociation(out IntPtr windowHandle);

        [PreserveSig]
        int CreateSwapChain(IntPtr device, IntPtr desc, out IntPtr swapChain);

        [PreserveSig]
        int CreateSoftwareAdapter(IntPtr module, out IntPtr adapter);

        [PreserveSig]
        int EnumAdapters1(uint adapter, [MarshalAs(UnmanagedType.Interface)] out IDXGIAdapter1? ppAdapter);
    }

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDXGIAdapter1
    {
        [PreserveSig]
        int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

        [PreserveSig]
        int SetPrivateDataInterface(ref Guid name, IntPtr unknown);

        [PreserveSig]
        int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

        [PreserveSig]
        int GetParent(ref Guid riid, out IntPtr parent);

        [PreserveSig]
        int EnumOutputs(uint output, out IntPtr outputPtr);

        [PreserveSig]
        int GetDesc(IntPtr desc);

        [PreserveSig]
        int CheckInterfaceSupport(ref Guid interfaceName, out long umdVersion);

        [PreserveSig]
        int GetDesc1(out DXGI_ADAPTER_DESC1 desc);
    }
}
