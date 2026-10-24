using System.Runtime.CompilerServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace FlavosDiag.Providers.Gpu;

/// <summary>Resultado da execução de um frame: hash dos pixels e primeiro deslocamento divergente.</summary>
public readonly record struct ResultadoFrame(ulong Hash, int PrimeiroDeslocamentoDivergente);

/// <summary>
/// Sessão Direct3D 11 fora de tela (compute shader). Vive apenas durante o teste e libera todos os
/// recursos ao final. Não abre janela: nada aparece para o cliente.
/// </summary>
internal sealed class SessaoGpuCriada : IDisposable
{
    /// <summary>
    /// Shader de carga próprio: laço com dependência sequencial entre iterações (impede a GPU de
    /// paralelizar e eliminar o trabalho) e resultado determinístico para o mesmo frame.
    /// Divergência entre a referência e as execuções seguintes = artefato de renderização.
    /// </summary>
    private const string HlslCarga = """
        Texture2D<float4> Entrada : register(t0);
        SamplerState Amostra : register(s0);
        RWTexture2D<float4> Saida : register(u0);

        cbuffer Parametros : register(b0)
        {
            uint Frame;
            uint Largura;
            uint Altura;
            uint Padding;
        };

        float Ruido(float semente)
        {
            float v = sin(semente * 12.9898 + 78.233) * 43758.5453;
            return v - floor(v);
        }

        float4 Misturar(float4 a, float4 b, float t)
        {
            return a * (1.0 - t) + b * t;
        }

        [numthreads(8, 8, 1)]
        void CSPrincipal(uint3 id : SV_DispatchThreadID)
        {
            if (id.x >= Largura || id.y >= Altura)
            {
                return;
            }

            float2 tamanho = float2((float)Largura, (float)Altura);
            float2 uv = (float2(id.xy) + 0.5) / tamanho;
            float4 acumulado = float4(0.0, 0.0, 0.0, 0.0);
            float2 pos = uv;

            for (uint i = 0u; i < 64u; i++)
            {
                float4 amostra = Entrada.SampleLevel(Amostra, pos, 0.0);
                float n = Ruido((float)i + pos.x * 31.0 + pos.y * 17.0);
                pos = frac(pos + float2(n * 0.017, n * 0.013) + 0.0031);
                acumulado += Misturar(amostra, float4(n, 1.0 - n, pos.x, pos.y), 0.5);
            }

            Saida[id.xy] = acumulado / 64.0;
        }
        """;

    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _contexto;
    private readonly ID3D11Texture2D _entrada;
    private readonly ID3D11ShaderResourceView _entradaView;
    private readonly ID3D11SamplerState _amostrador;
    private readonly ID3D11Texture2D _saida;
    private readonly ID3D11UnorderedAccessView _saidaView;
    private readonly ID3D11ComputeShader _shader;
    private readonly ID3D11Buffer _parametros;
    private readonly ID3D11Texture2D _leitura;
    private readonly byte[] _pixels;
    private uint _frame;

    private SessaoGpuCriada(
        ID3D11Device device,
        ID3D11DeviceContext contexto,
        ID3D11Texture2D entrada,
        ID3D11ShaderResourceView entradaView,
        ID3D11SamplerState amostrador,
        ID3D11Texture2D saida,
        ID3D11UnorderedAccessView saidaView,
        ID3D11ComputeShader shader,
        ID3D11Buffer parametros,
        ID3D11Texture2D leitura,
        string nomeAdaptador)
    {
        _device = device;
        _contexto = contexto;
        _entrada = entrada;
        _entradaView = entradaView;
        _amostrador = amostrador;
        _saida = saida;
        _saidaView = saidaView;
        _shader = shader;
        _parametros = parametros;
        _leitura = leitura;
        _pixels = new byte[GpuD3D11Provider.Largura * GpuD3D11Provider.Altura * GpuD3D11Provider.BytesPorPixel];
        NomeAdaptador = nomeAdaptador;
    }

    public string NomeAdaptador { get; }

    /// <summary>Motivo da falha de criação (para o laudo). Nulo quando a sessão foi criada com êxito.</summary>
    public static string? UltimaFalha { get; private set; }

    /// <summary>Nome do shader compilado usado na carga (para o laudo).</summary>
    public static string NomeShader => "flavos-carga-gpu v1";

    public static SessaoGpuCriada? Criar()
    {
        UltimaFalha = null;
        try
        {
            var niveis = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 };

            ID3D11Device device;
            ID3D11DeviceContext contexto;
            try
            {
                var resultado = D3D11.D3D11CreateDevice(
                    (IDXGIAdapter?)null,
                    DriverType.Hardware,
                    DeviceCreationFlags.BgraSupport,
                    niveis,
                    out device,
                    out contexto);
                if (resultado.Failure)
                {
                    return CriarComSoftware(niveis, "a GPU de hardware recusou criar o dispositivo (0x" + resultado.Code.ToString("X8") + ")");
                }
            }
            catch (SharpGen.Runtime.SharpGenException ex)
            {
                return CriarComSoftware(niveis, "a GPU de hardware falhou: " + ex.Message);
            }

            return Construir(device, contexto) ?? Falhar("o dispositivo foi criado, mas os recursos do shader não");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Falhar(ex.Message);
        }

        static SessaoGpuCriada? Falhar(string motivo)
        {
            UltimaFalha = motivo;
            return null;
        }
    }

    private static SessaoGpuCriada? CriarComSoftware(FeatureLevel[] niveis, string motivoHardware)
    {
        try
        {
            // Rasterizador de software do Windows: garante que a prova funcione em máquina virtual,
            // a custo de desempenho (registrado como limitação no resultado).
            var resultado = D3D11.D3D11CreateDevice(
                (IDXGIAdapter?)null,
                DriverType.Warp,
                DeviceCreationFlags.BgraSupport,
                niveis,
                out ID3D11Device device,
                out ID3D11DeviceContext contexto);

            if (resultado.Failure)
            {
                UltimaFalha = motivoHardware + " e o rasterizador de software também falhou (0x" + resultado.Code.ToString("X8") + ").";
                return null;
            }

            return Construir(device, contexto) ?? FalharSoftware(motivoHardware);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UltimaFalha = motivoHardware + " e o rasterizador de software falhou: " + ex.Message;
            return null;
        }

        static SessaoGpuCriada? FalharSoftware(string motivo)
        {
            UltimaFalha = motivo + " e os recursos do shader não puderam ser criados.";
            return null;
        }
    }

    private static SessaoGpuCriada? Construir(ID3D11Device device, ID3D11DeviceContext contexto)
    {
        ID3D11Texture2D? entrada = null;
        ID3D11ShaderResourceView? entradaView = null;
        ID3D11SamplerState? amostrador = null;
        ID3D11Texture2D? saida = null;
        ID3D11UnorderedAccessView? saidaView = null;
        ID3D11ComputeShader? shader = null;
        ID3D11Buffer? parametros = null;
        ID3D11Texture2D? leitura = null;

        try
        {
            var nome = NomeDoAdaptador(device);

            var bytecode = Compiler.Compile(HlslCarga, "CSPrincipal", "flavos_gpu.hlsl", "cs_5_0");
            shader = device.CreateComputeShader(bytecode.ToArray());

            var largura = (uint)GpuD3D11Provider.Largura;
            var altura = (uint)GpuD3D11Provider.Altura;

            var dadosEntrada = new float[largura * altura * 4];
            for (var i = 0; i < dadosEntrada.Length; i++)
            {
                dadosEntrada[i] = (i % 97) / 97.0f;
            }

            entrada = device.CreateTexture2D(dadosEntrada, Format.R32G32B32A32_Float, largura, altura);
            entradaView = device.CreateShaderResourceView(entrada);
            amostrador = device.CreateSamplerState(SamplerDescription.LinearWrap);

            saida = device.CreateTexture2D(Format.R32G32B32A32_Float, largura, altura,
                bindFlags: BindFlags.UnorderedAccess | BindFlags.ShaderResource);
            saidaView = device.CreateUnorderedAccessView(saida);

            parametros = device.CreateBuffer(16, BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);

            leitura = device.CreateTexture2D(Format.R32G32B32A32_Float, largura, altura,
                bindFlags: BindFlags.None,
                usage: ResourceUsage.Staging,
                cpuAccessFlags: CpuAccessFlags.Read);

            return new SessaoGpuCriada(device, contexto, entrada, entradaView, amostrador, saida, saidaView, shader, parametros, leitura, nome);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            leitura?.Dispose();
            parametros?.Dispose();
            shader?.Dispose();
            saidaView?.Dispose();
            saida?.Dispose();
            amostrador?.Dispose();
            entradaView?.Dispose();
            entrada?.Dispose();
            contexto.Dispose();
            device.Dispose();
            return null;
        }
    }

    private static string NomeDoAdaptador(ID3D11Device device)
    {
        try
        {
            using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
            using var adaptador = dxgiDevice.GetAdapter();
            using var adaptador1 = adaptador.QueryInterface<IDXGIAdapter1>();
            var descricao = adaptador1.Description1;
            return string.IsNullOrWhiteSpace(descricao.Description) ? "adaptador sem nome" : descricao.Description.Trim();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return "adaptador não identificado";
        }
    }

    /// <summary>
    /// Executa <paramref name="frames"/> quadros e devolve o hash dos pixels.
    /// Nulo significa que o driver reiniciou (TDR) ou a execução falhou — condição de diagnóstico.
    /// </summary>
    public ResultadoFrame? Executar(uint frames)
    {
        try
        {
            for (var f = 0u; f < frames; f++)
            {
                _frame++;

                _contexto.CSSetShader(_shader);
                _contexto.CSSetShaderResource(0, _entradaView);
                _contexto.CSSetSampler(0, _amostrador);
                _contexto.CSSetUnorderedAccessView(0, _saidaView);

                var mapeado = _contexto.Map(_parametros, 0, MapMode.WriteDiscard);
                unsafe
                {
                    var destino = (uint*)mapeado.DataPointer;
                    destino[0] = _frame;
                    destino[1] = (uint)GpuD3D11Provider.Largura;
                    destino[2] = (uint)GpuD3D11Provider.Altura;
                    destino[3] = 0;
                }

                _contexto.Unmap(_parametros, 0);
                _contexto.CSSetConstantBuffer(0, _parametros);
                _contexto.Dispatch((uint)(GpuD3D11Provider.Largura / 8), (uint)(GpuD3D11Provider.Altura / 8), 1);
                _contexto.CopyResource(_leitura, _saida);
            }

            _contexto.Flush();

            // Lê os pixels de volta e compara com o esperado pelo hash.
            var pitchDaLinha = GpuD3D11Provider.Largura * GpuD3D11Provider.BytesPorPixel;
            var mapeadoSaida = _contexto.Map(_leitura, 0, MapMode.Read);
            try
            {
                unsafe
                {
                    var origem = (byte*)mapeadoSaida.DataPointer;
                    for (var linha = 0; linha < GpuD3D11Provider.Altura; linha++)
                    {
                        var destinoLinha = linha * pitchDaLinha;
                        Buffer.MemoryCopy(
                            origem + ((long)linha * mapeadoSaida.RowPitch),
                            Unsafe.AsPointer(ref _pixels[destinoLinha]),
                            pitchDaLinha,
                            pitchDaLinha);
                    }
                }
            }
            finally
            {
                _contexto.Unmap(_leitura, 0);
            }

            return CalcularHash(_pixels);
        }
        catch (SharpGen.Runtime.SharpGenException ex) when (EhRemocaoDeDispositivo(ex))
        {
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Reinício/remoção do dispositivo: o código HRESULT corresponde a TDR.</summary>
    public static bool EhRemocaoDeDispositivo(SharpGen.Runtime.SharpGenException ex)
    {
        var codigo = (uint)ex.ResultCode.Code;
        return codigo is 0x887A0005 or 0x887A0006 or 0x887A0007 or 0x887A0020;
    }

    /// <summary>
    /// Hash FNV-1a de 64 bits sobre os pixels (independente de plataforma, sem dependência de biblioteca).
    /// </summary>
    public static ResultadoFrame CalcularHash(ReadOnlySpan<byte> pixels)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong primo = 1099511628211UL;

        var hash = offset;
        for (var i = 0; i < pixels.Length; i++)
        {
            hash ^= pixels[i];
            hash = unchecked(hash * primo);
        }

        return new ResultadoFrame(hash, -1);
    }

    public void Dispose()
    {
        _leitura.Dispose();
        _parametros.Dispose();
        _shader.Dispose();
        _saidaView.Dispose();
        _saida.Dispose();
        _amostrador.Dispose();
        _entradaView.Dispose();
        _entrada.Dispose();
        _contexto.Dispose();
        _device.Dispose();
    }
}
