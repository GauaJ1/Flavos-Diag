using System.Diagnostics;
using System.IO;
using System.Management;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Abstractions;
using FlavosDiag.Providers.Nativo;

namespace FlavosDiag.Providers.Discos;

/// <summary>
/// Discos físicos do Windows: identificação com as APIs de armazenamento, saúde com SMART ATA,
/// log NVMe e contadores de confiabilidade do Windows. Tudo somente leitura.
/// Quando nenhuma fonte de saúde responde, o disco fica <b>Inconclusivo com a causa</b> — nunca OK.
/// </summary>
public sealed class DiscosWindowsProvider : IDiscoProvider
{
    private const string WorkspaceTemporario = "FlavosDiag";

    private readonly IAmbiente? _ambiente;
    private readonly IAutorizacaoLeituraDisco? _autorizacao;

    /// <summary>
    /// Provider sem trava de elevação/consentimento. Mantido para composição simples e testes; o
    /// aplicativo usa o construtor com <see cref="IAmbiente"/> e <see cref="IAutorizacaoLeituraDisco"/>,
    /// que é o que faz a leitura de pass-through depender de elevação e de aceite do usuário (D78).
    /// </summary>
    public DiscosWindowsProvider()
    {
    }

    /// <param name="ambiente">Fonte da verdade sobre elevação do processo.</param>
    /// <param name="autorizacao">Registro do aceite do usuário para ler o disco.</param>
    public DiscosWindowsProvider(IAmbiente ambiente, IAutorizacaoLeituraDisco autorizacao)
    {
        _ambiente = ambiente;
        _autorizacao = autorizacao;
    }

    public string Nome => "Windows (armazenamento + SMART)";

    /// <summary>
    /// Pré-condições do pass-through conforme o estado atual. Sem ambiente/autorização injetados, a
    /// leitura de pass-through fica <b>recusada</b> (falha fechada) — nunca "liberada por omissão".
    /// </summary>
    private RequisicaoDisco.Permissoes PermissoesAtuais => new(
        ProcessoElevado: _ambiente?.EhAdministrador == true,
        UsuarioAutorizouLeitura: _autorizacao?.Autorizada == true);

    /// <summary>
    /// Texto do aviso mostrado antes da leitura do disco. Diz, sem rodeio, que o disco só é lido.
    /// </summary>
    public const string AvisoLeituraDisco =
        "O Flavos Diag vai consultar a saúde dos discos físicos deste computador.\n\n" +
        "A consulta é SOMENTE LEITURA: o programa pede ao disco os dados de saúde (identificação e " +
        "atributos SMART) e não grava nada no disco, não altera setores e não muda nenhuma configuração.\n\n" +
        "A leitura exige permissão de administrador e pode levar alguns segundos em discos lentos ou " +
        "com defeito. Ler um disco em estado crítico também o estressa — se o equipamento estiver " +
        "apresentando falhas, faça backup antes e prefira interromper.\n\n" +
        "Deseja consultar a saúde dos discos agora?";

    private bool SemElevacao => _ambiente is not null && !_ambiente.EhAdministrador;

    public async Task<IReadOnlyList<DiscoInfo>> ListarAsync(CancellationToken cancelamento = default)
    {
        var permissoes = PermissoesAtuais;
        var semElevacao = false;
        var identificacoes = await Task.Run(() =>
        {
            var encontrados = IdentificarDiscos(out var semElevacaoDetectada);
            semElevacao = semElevacaoDetectada;
            return encontrados;
        }, cancelamento);
        semElevacao |= SemElevacao;
        var confiabilidade = await LerConfiabilidadeAsync(cancelamento);

        var lista = new List<DiscoInfo>(identificacoes.Count);
        foreach (var id in identificacoes)
        {
            cancelamento.ThrowIfCancellationRequested();
            lista.Add(MontarDisco(id, confiabilidade.GetValueOrDefault(id.Numero), semElevacao, permissoes));
        }

        return lista;
    }

    // ------------------------------------------------------------------ identificação

    /// <summary>
    /// Lista os discos existentes. A <b>identificação</b> vem do WMI (funciona sem elevação);
    /// os detalhes por IOCTL só são tentados como refinamento. Assim um técnico sem administrador
    /// continua vendo os discos, com a saúde marcada como Inconclusiva e o motivo (falta elevação).
    /// </summary>
    private static List<IdentificacaoDispositivo> IdentificarDiscos(out bool semElevacao)
    {
        semElevacao = false;
        var porWmi = IdentificarPorWmi();
        var lista = new List<IdentificacaoDispositivo>(porWmi.Count);

        foreach (var item in porWmi)
        {
            var descritor = LerDescritor(item.Numero);
            var tamanho = item.TamanhoBytes > 0 ? item.TamanhoBytes : LerTamanho(item.Numero);
            var penalidade = LerPenalidadeBusca(item.Numero);
            if (descritor is null && penalidade is null)
            {
                // Os IOCTL de descritor falham justamente quando falta elevação.
                using var teste = RequisicaoDisco.AbrirSomenteLeitura(item.Numero, out var erro);
                semElevacao |= erro is not null && erro.Contains("Acesso negado", StringComparison.Ordinal);
            }

            lista.Add(new IdentificacaoDispositivo(
                item.Numero,
                descritor?.Modelo ?? item.Modelo,
                descritor?.Firmware ?? item.Firmware,
                descritor?.Serial ?? item.Serial,
                descritor?.Barramento ?? item.TipoBarramento,
                tamanho,
                penalidade is null ? null : penalidade.Value != 0,
                item.MidiaStorage,
                item.SaudeStorage));
        }

        return lista;
    }

    /// <summary>Discos físicos pelo WMI (Win32_DiskDrive). Fonte primária da lista; nunca lança.</summary>
    private static List<IdentificacaoDispositivo> IdentificarPorWmi()
    {
        var lista = new List<IdentificacaoDispositivo>();
        var porStorage = DiscosPorStorage();
        try
        {
            var escopo = new ManagementScope(@"root\cimv2");
            escopo.Connect();
            using var searcher = new ManagementObjectSearcher(
                escopo,
                new ObjectQuery("SELECT Index, Model, SerialNumber, FirmwareRevision, InterfaceType, Size, MediaType, Capabilities FROM Win32_DiskDrive"));
            searcher.Options.Timeout = TimeSpan.FromSeconds(20);

            using var resultado = searcher.Get();
            foreach (ManagementObject disco in resultado)
            {
                var indice = Convert.ToInt32(disco["Index"] ?? -1);
                if (indice < 0)
                {
                    disco.Dispose();
                    continue;
                }

                var tamanho = disco["Size"] is { } s ? Convert.ToInt64(s) : 0L;
                var tipo = (disco["InterfaceType"] as string ?? string.Empty).Trim();
                var storage = porStorage.GetValueOrDefault(indice);
                lista.Add(new IdentificacaoDispositivo(
                    indice,
                    (disco["Model"] as string)?.Trim(),
                    (disco["FirmwareRevision"] as string)?.Trim(),
                    (disco["SerialNumber"] as string)?.Trim(),
                    storage.Barramento ?? BarramentoDoWmi(tipo),
                    tamanho,
                    null,
                    storage.Midia,
                    storage.Saude));
                disco.Dispose();
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException
                                       or System.Runtime.InteropServices.COMException or InvalidCastException or FormatException)
        {
            // WMI sem o escopo de disco: a lista fica vazia e o chamador registra a limitação.
        }

        lista.Sort((a, b) => a.Numero.CompareTo(b.Numero));
        return lista;
    }

    /// <summary>
    /// Tipo de mídia e estado de saúde anunciados pela API de armazenamento do Windows
    /// (MSFT_PhysicalDisk, namespace root\Microsoft\Windows\Storage). Não exige elevação.
    /// </summary>
    private static Dictionary<int, (byte? Barramento, TipoMidia? Midia, string? Saude)> DiscosPorStorage()
    {
        var mapa = new Dictionary<int, (byte?, TipoMidia?, string?)>();
        try
        {
            var escopo = new ManagementScope(@"root\Microsoft\Windows\Storage");
            escopo.Connect();
            using var searcher = new ManagementObjectSearcher(
                escopo,
                new ObjectQuery("SELECT DeviceId, MediaType, BusType, HealthStatus FROM MSFT_PhysicalDisk"));
            searcher.Options.Timeout = TimeSpan.FromSeconds(15);
            using var resultado = searcher.Get();
            foreach (ManagementObject disco in resultado)
            {
                if (int.TryParse(Convert.ToString(disco["DeviceId"]), out var numero))
                {
                    // MediaType: 3 = HDD, 4 = SSD, 5 = SCM; 0 = não especificado.
                    var midia = Convert.ToInt32(disco["MediaType"] ?? 0) switch
                    {
                        3 => TipoMidia.Hdd,
                        4 or 5 => TipoMidia.Ssd,
                        _ => (TipoMidia?)null,
                    };

                    // HealthStatus: 0 = saudável, 1 = aviso, 2 = não saudável.
                    var saude = Convert.ToInt32(disco["HealthStatus"] ?? -1) switch
                    {
                        0 => "O Windows informa o disco como saudável.",
                        1 => "O Windows informa aviso de saúde neste disco.",
                        2 => "O Windows informa que este disco NÃO está saudável.",
                        _ => null,
                    };

                    var barramento = Convert.ToInt32(disco["BusType"] ?? 0);
                    mapa[numero] = (barramento > 0 ? (byte)barramento : null, midia, saude);
                }

                disco.Dispose();
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException
                                       or System.Runtime.InteropServices.COMException or InvalidCastException or FormatException)
        {
            // Escopo Storage indisponível: seguimos com Win32_DiskDrive.
        }

        return mapa;
    }
    private static byte BarramentoDoWmi(string tipo) => tipo.ToUpperInvariant() switch
    {
        "NVME" => ApiNativas.BusTypeNvme,
        "IDE" => ApiNativas.BusTypeAta,
        "SATA" => ApiNativas.BusTypeSata,
        "SCSI" => ApiNativas.BusTypeScsi,
        "USB" => ApiNativas.BusTypeUsb,
        "1394" => ApiNativas.BusTypeUsb,
        _ => 0,
    };

    /// <summary>Quantidade de discos físicos anunciada pelo Windows (não há API direta; tentamos em ordem).</summary>
    private static int DescobrirQuantidade()
    {
        for (var n = 0; n < 64; n++)
        {
            using var alca = RequisicaoDisco.AbrirSomenteLeitura(n, out var erro);
            // Para no primeiro índice inexistente; "acesso negado" significa que o disco existe.
            if (erro is not null && erro.Contains("não encontrado", StringComparison.Ordinal))
            {
                return n;
            }
        }

        return 64;
    }

    private static (string? Modelo, string? Firmware, string? Serial, byte Barramento)? LerDescritor(int numero)
    {
        using var dispositivo = RequisicaoDisco.AbrirSomenteLeitura(numero, out _);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            return null;
        }

        const int tamanhoBuffer = 1024;
        var entrada = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<STORAGE_PROPERTY_QUERY>());
        var saida = System.Runtime.InteropServices.Marshal.AllocHGlobal(tamanhoBuffer);
        try
        {
            var consulta = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = ApiNativas.StorageDeviceProperty,
                QueryType = ApiNativas.PropertyStandardQuery,
            };
            System.Runtime.InteropServices.Marshal.StructureToPtr(consulta, entrada, false);

            var ok = ApiNativas.DeviceIoControl(
                dispositivo,
                ApiNativas.IOCTL_STORAGE_QUERY_PROPERTY,
                entrada,
                System.Runtime.InteropServices.Marshal.SizeOf<STORAGE_PROPERTY_QUERY>(),
                saida,
                tamanhoBuffer,
                out var retornados,
                IntPtr.Zero);

            if (!ok || retornados < 36)
            {
                return null;
            }

            var cabecalho = System.Runtime.InteropServices.Marshal.PtrToStructure<STORAGE_DEVICE_DESCRIPTOR_CABECALHO>(saida);
            var bytes = new byte[tamanhoBuffer];
            System.Runtime.InteropServices.Marshal.Copy(saida, bytes, 0, tamanhoBuffer);

            string? Ler(int offset) => offset > 0 && offset < bytes.Length ? TextoAscii(bytes, offset) : null;

            var barramento = cabecalho.BusType;
            var modelo = Ler(cabecalho.ProductIdOffset);
            var firmware = Ler(cabecalho.ProductRevisionOffset);
            var serial = Ler(cabecalho.SerialNumberOffset);
            var fabricante = Ler(cabecalho.VendorIdOffset);

            var modeloCompleto = string.Join(' ', new[] { fabricante, modelo }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return (
                string.IsNullOrWhiteSpace(modeloCompleto) ? null : modeloCompleto.Trim(),
                firmware,
                serial,
                barramento);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(entrada);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(saida);
        }
    }

    private static string TextoAscii(byte[] buffer, int inicio)
    {
        var fim = inicio;
        while (fim < buffer.Length && buffer[fim] != 0)
        {
            fim++;
        }

        var texto = System.Text.Encoding.ASCII.GetString(buffer, inicio, fim - inicio).Trim();
        return texto;
    }

    private static long LerTamanho(int numero)
    {
        using var dispositivo = RequisicaoDisco.AbrirSomenteLeitura(numero, out _);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            return 0;
        }

        var saida = System.Runtime.InteropServices.Marshal.AllocHGlobal(8);
        try
        {
            var ok = ApiNativas.DeviceIoControl(
                dispositivo,
                ApiNativas.IOCTL_DISK_GET_LENGTH_INFO,
                IntPtr.Zero,
                0,
                saida,
                8,
                out _,
                IntPtr.Zero);
            return ok ? System.Runtime.InteropServices.Marshal.ReadInt64(saida) : 0;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(saida);
        }
    }

    private static long? LerPenalidadeBusca(int numero)
    {
        using var dispositivo = RequisicaoDisco.AbrirSomenteLeitura(numero, out _);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            return null;
        }

        var tamanhoEntrada = System.Runtime.InteropServices.Marshal.SizeOf<STORAGE_PROPERTY_QUERY>();
        var tamanhoSaida = System.Runtime.InteropServices.Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>();
        var entrada = System.Runtime.InteropServices.Marshal.AllocHGlobal(tamanhoEntrada);
        var saida = System.Runtime.InteropServices.Marshal.AllocHGlobal(tamanhoSaida);
        try
        {
            var consulta = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = ApiNativas.StorageDeviceSeekPenaltyProperty,
                QueryType = ApiNativas.PropertyStandardQuery,
            };
            System.Runtime.InteropServices.Marshal.StructureToPtr(consulta, entrada, false);

            var ok = ApiNativas.DeviceIoControl(
                dispositivo,
                ApiNativas.IOCTL_STORAGE_QUERY_PROPERTY,
                entrada,
                tamanhoEntrada,
                saida,
                tamanhoSaida,
                out _,
                IntPtr.Zero);

            if (!ok)
            {
                return null;
            }

            var descritor = System.Runtime.InteropServices.Marshal.PtrToStructure<DEVICE_SEEK_PENALTY_DESCRIPTOR>(saida);
            return descritor.IncursSeekPenalty ? 1 : 0;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(entrada);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(saida);
        }
    }

    // ------------------------------------------------------------------ saúde

    private static DiscoInfo MontarDisco(
        IdentificacaoDispositivo id,
        ConfiabilidadeWindows? confiabilidade,
        bool semElevacao,
        RequisicaoDisco.Permissoes permissoes)
    {
        var limitacoes = new List<DadoAusente>();
        var fontes = new List<FonteSaudeDisco>();
        IReadOnlyList<AtributoSmart> atributos = Array.Empty<AtributoSmart>();
        SaudeNvme? nvme = null;
        bool? previstoFalha = null;
        bool? prognosticoStorage = null;
        var midia = ClassificarMidia(id, out var motivoMidia);
        if (motivoMidia is not null)
        {
            limitacoes.Add(new DadoAusente("Tipo de mídia", motivoMidia));
        }

        var eNvme = id.TipoBarramento == ApiNativas.BusTypeNvme;
        if (!permissoes.Permitido)
        {
            // Leitura de pass-through recusada por falta de elevação ou de aceite do usuário (D78):
            // o disco não é aberto. A limitação é registrada com a causa e o veredito fica Inconclusivo —
            // nunca OK, porque nada foi lido.
            limitacoes.Add(new DadoAusente(
                eNvme ? "Log de saúde NVMe" : "Atributos SMART (ATA)",
                !permissoes.ProcessoElevado
                    ? "Leitura não executada: o processo não está elevado. O Windows só repassa a leitura " +
                      "SMART/NVMe para administrador. O veredito fica Inconclusivo."
                    : "Leitura não executada: o usuário não autorizou a leitura do disco. O veredito fica Inconclusivo."));
        }
        else if (eNvme)
        {
            var log = SmartNvme.Ler(id.Numero, permissoes, out var motivo);
            if (log is not null)
            {
                fontes.Add(FonteSaudeDisco.Nvme);
                nvme = ConverterNvme(log);
            }
            else
            {
                limitacoes.Add(new DadoAusente("Log de saúde NVMe", motivo ?? "Não disponível."));
            }
        }
        else
        {
            var bruto = SmartAta.LerSmartAta(id.Numero, permissoes);
            if (bruto.Sucesso && bruto.Dados512 is not null)
            {
                var interpretados = SmartAta.Interpretar(bruto.Dados512);
                if (interpretados.Count > 0)
                {
                    atributos = interpretados;
                    fontes.Add(FonteSaudeDisco.SmartAta);
                }
                else
                {
                    limitacoes.Add(new DadoAusente("Atributos SMART", "O bloco de atributos veio vazio."));
                }
            }
            else
            {
                // Segunda tentativa: previsão de falha pelo próprio Windows (funciona em muitos
                // dispositivos USB/RAID onde o pass-through ATA é bloqueado pelo driver).
                var predicao = SmartAta.LerPredicaoWindows(id.Numero);
                if (predicao.Sucesso && predicao.Dados512 is { Length: > 0 } blocoPredito)
                {
                    previstoFalha = blocoPredito[0] != 0;
                    fontes.Add(FonteSaudeDisco.SmartAta);
                    limitacoes.Add(new DadoAusente(
                        "Atributos SMART detalhados (ATA)",
                        "Este controlador não repassa os atributos SMART; a avaliação usa a previsão de falha do Windows. " +
                        (bruto.Erro ?? string.Empty).Trim()));
                }
                else
                {
                    limitacoes.Add(new DadoAusente(
                        "Atributos SMART (ATA)",
                        bruto.Erro ?? "Não disponível."));
                }
            }
        }

        if (confiabilidade is not null)
        {
            fontes.Add(FonteSaudeDisco.ConfiabilidadeWindows);
        }

        // O estado de saúde do MSFT_PhysicalDisk é uma leitura real do Windows. Só entra quando o
        // driver informa algo — e só marca "OK" se as demais fontes também permitirem.
        if (id.SaudeStorage is { Length: > 0 } saudeStorage)
        {
            fontes.Add(FonteSaudeDisco.EstadoWindows);
            if (id.SaudeStorage.StartsWith("O Windows informa o disco como saudável", StringComparison.Ordinal))
            {
                prognosticoStorage = true;
            }
            else
            {
                prognosticoStorage = false;
                limitacoes.Add(new DadoAusente("Estado de saúde (Windows)", saudeStorage));
            }
        }

        if (fontes.Count == 0)
        {
            limitacoes.Add(new DadoAusente(
                "Saúde do disco",
                !permissoes.UsuarioAutorizouLeitura
                    ? "A leitura do disco não foi autorizada. Autorize a consulta de saúde para diagnosticar. O veredito fica Inconclusivo."
                    : semElevacao
                        ? "Sem elevação: o Windows só libera a leitura SMART/NVMe para administrador. Execute o aplicativo como administrador para diagnosticar a saúde. O veredito fica Inconclusivo."
                        : "Nenhuma fonte de saúde respondeu para este disco. O veredito fica Inconclusivo."));
        }

        return new DiscoInfo
        {
            Numero = id.Numero,
            Modelo = id.Modelo is null
                ? Leitura<string>.NaoSuportado("O Windows não informou o modelo deste disco.")
                : Leitura<string>.Ok(id.Modelo, "STORAGE_DEVICE_DESCRIPTOR"),
            Firmware = id.Firmware is null
                ? Leitura<string>.NaoSuportado("Firmware não informado pelo dispositivo.")
                : Leitura<string>.Ok(id.Firmware, "STORAGE_DEVICE_DESCRIPTOR"),
            Barramento = Barramento(id.TipoBarramento),
            Midia = midia,
            TamanhoBytes = id.TamanhoBytes > 0
                ? Leitura<long>.Ok(id.TamanhoBytes, "IOCTL_DISK_GET_LENGTH_INFO")
                : Leitura<long>.NaoSuportado("O Windows não informou o tamanho deste disco."),
            NumeroSerie = id.Serial is null
                ? Leitura<string>.NaoSuportado("Número de série não exposto por este dispositivo.")
                : Leitura<string>.Ok(id.Serial, "STORAGE_DEVICE_DESCRIPTOR"),
            Saude = new SaudeDisco
            {
                Fontes = fontes,
                AtributosAta = atributos,
                Nvme = nvme,
                Confiabilidade = confiabilidade,
                PrevisaoFalhaWindows = previstoFalha,
                SaudavelSegundoWindows = prognosticoStorage,
                Limitacoes = limitacoes,
            },
        };
    }

    private static Leitura<TipoMidia> ClassificarMidia(IdentificacaoDispositivo id, out string? motivo)
    {
        motivo = null;
        if (id.IncorrePenalidadeBusca is { } penalidade)
        {
            return Leitura<TipoMidia>.Ok(penalidade ? TipoMidia.Hdd : TipoMidia.Ssd, "DEVICE_SEEK_PENALTY_DESCRIPTOR");
        }

        if (id.MidiaStorage is { } midia)
        {
            return Leitura<TipoMidia>.Ok(midia, "MSFT_PhysicalDisk.MediaType");
        }

        if (id.TipoBarramento == ApiNativas.BusTypeNvme)
        {
            return Leitura<TipoMidia>.Ok(TipoMidia.Ssd, "barramento NVMe");
        }

        motivo = "O Windows não informou se a mídia é magnética ou sólida.";
        return Leitura<TipoMidia>.NaoSuportado(motivo);
    }

    private static Leitura<string> Barramento(byte tipo) => tipo switch
    {
        ApiNativas.BusTypeNvme => Leitura<string>.Ok("NVMe", "STORAGE_DEVICE_DESCRIPTOR"),
        ApiNativas.BusTypeSata => Leitura<string>.Ok("SATA", "STORAGE_DEVICE_DESCRIPTOR"),
        ApiNativas.BusTypeAta => Leitura<string>.Ok("ATA", "STORAGE_DEVICE_DESCRIPTOR"),
        ApiNativas.BusTypeAtapi => Leitura<string>.Ok("ATAPI", "STORAGE_DEVICE_DESCRIPTOR"),
        ApiNativas.BusTypeUsb => Leitura<string>.Ok("USB", "STORAGE_DEVICE_DESCRIPTOR"),
        ApiNativas.BusTypeScsi => Leitura<string>.Ok("SCSI", "STORAGE_DEVICE_DESCRIPTOR"),
        ApiNativas.BusTypeSas => Leitura<string>.Ok("SAS", "STORAGE_DEVICE_DESCRIPTOR"),
        ApiNativas.BusTypeRAID => Leitura<string>.Ok("RAID", "STORAGE_DEVICE_DESCRIPTOR"),
        ApiNativas.BusTypeVirtual => Leitura<string>.Ok("Virtual", "STORAGE_DEVICE_DESCRIPTOR"),
        0 => Leitura<string>.NaoSuportado("O Windows não informou o barramento (dispositivo virtual ou desconhecido)."),
        _ => Leitura<string>.Ok("Outro (" + tipo + ")", "STORAGE_DEVICE_DESCRIPTOR"),
    };

    private static SaudeNvme ConverterNvme(LogSaudeNvme log) => new()
    {
        AvisoCritico = Leitura<int>.Ok(log.AvisoCritico, "NVMe log 0x02"),
        // O campo Kelvin vem zerado em alguns controladores: "sem leitura" tem de sair como ausência com
        // causa, senão o laudo anuncia "0 °C" como se o sensor tivesse medido.
        TemperaturaCelsius = log.TemperaturaCelsius is { } celsius
            ? Temperatura.InterpretarTemperatura(celsius, "NVMe log 0x02")
            : Leitura<int>.NaoSuportado(Temperatura.CausaSentinelaOuForaDaFaixa, "NVMe log 0x02"),
        EspacoDisponivelPercentual = Leitura<int>.Ok(log.EspacoDisponivelPercentual, "NVMe log 0x02"),
        LimiarEspacoDisponivel = Leitura<int>.Ok(log.LimiarEspacoPercentual, "NVMe log 0x02"),
        PercentualVidaUsada = Leitura<double>.Ok(log.PercentualVidaUsada, "NVMe log 0x02"),
        DadosEscritosGb = Leitura<long>.Ok(log.DadosEscritosGb, "NVMe log 0x02"),
        DadosLidosGb = Leitura<long>.Ok(log.DadosLidosGb, "NVMe log 0x02"),
        HorasLigado = Leitura<long>.Ok(log.HorasLigado, "NVMe log 0x02"),
        CiclosLigadoDesligado = Leitura<long>.Ok(log.CiclosLigarDesligar, "NVMe log 0x02"),
        ErrosIntegridadeMidia = Leitura<long>.Ok(log.ErrosIntegridadeMidia, "NVMe log 0x02"),
    };

    /// <summary>
    /// Contadores de confiabilidade do Windows (escopo Storage). Servem de segunda opinião quando o
    /// SMART não está acessível; nunca substituem a leitura quando ela existe.
    /// </summary>
    private static async Task<Dictionary<int, ConfiabilidadeWindows>> LerConfiabilidadeAsync(CancellationToken cancelamento)
    {
        return await Task.Run<Dictionary<int, ConfiabilidadeWindows>>(() =>
        {
            var mapa = new Dictionary<int, ConfiabilidadeWindows>();
            try
            {
                var escopo = new ManagementScope(@"root\Microsoft\Windows\Storage");
                escopo.Connect();
                using var discos = new ManagementObjectSearcher(
                    escopo,
                    new ObjectQuery("SELECT Number FROM MSFT_PhysicalDisk"));
                discos.Options.Timeout = TimeSpan.FromSeconds(20);

                var numeros = new List<int>();
                using (var resultado = discos.Get())
                {
                    foreach (ManagementObject disco in resultado)
                    {
                        cancelamento.ThrowIfCancellationRequested();
                        var numero = Convert.ToInt32(disco["Number"] ?? -1);
                        if (numero >= 0)
                        {
                            numeros.Add(numero);
                        }

                        disco.Dispose();
                    }
                }

                foreach (var numero in numeros)
                {
                    using var contadores = new ManagementObjectSearcher(
                        escopo,
                        new ObjectQuery($"SELECT * FROM MSFT_StorageReliabilityCounter WHERE DeviceNumber = {numero}"));
                    contadores.Options.Timeout = TimeSpan.FromSeconds(10);
                    using var resultado = contadores.Get();
                    foreach (ManagementObject contador in resultado)
                    {
                        mapa[numero] = new ConfiabilidadeWindows
                        {
                            ErrosLeituraNaoCorrigidos = LerContador<long>(contador, "ReadErrorsUncorrected", "ReadErrorsTotal"),
                            DesgastePercentual = LerContador<int>(contador, "Wear"),
                            TemperaturaCelsius = LerTemperaturaContador(contador),
                            HorasLigado = LerContador<long>(contador, "PowerOnHours"),
                        };
                        contador.Dispose();
                    }
                }
            }
            catch (ManagementException)
            {
                // Sem o escopo Storage (Windows antigo ou WMI restrito): segue sem o fallback.
            }
            catch (UnauthorizedAccessException)
            {
                // Idem: a ausência é registrada pelo consumidor ao ver que não há confiabilidade.
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Serviço WMI indisponível: o SMART continua sendo a fonte principal.
            }

            return mapa;
        }, cancelamento);
    }

    /// <summary>
    /// Leitura da temperatura em <c>MSFT_StorageReliabilityCounter</c>.
    /// </summary>
    /// <remarks>
    /// Some drivers expõem o contador com 0 ou 255 quando o termistor não está calibrado. Esses sentinelas
    /// (e qualquer valor fora de 1–120 °C) precisam virar ausência com causa: publicados como estão, o
    /// técnico leria "0 °C" como uma medição real. A regra de faixa é a mesma do SMART e do NVMe
    /// (<see cref="Temperatura"/>), para nenhuma fonte divergir da outra.
    /// </remarks>
    internal static Leitura<int> LerTemperaturaContador(ManagementObject objeto)
    {
        var bruta = LerContador<int>(objeto, "Temperature");
        return bruta.Disponivel
            ? Temperatura.InterpretarTemperatura(bruta.Valor, bruta.Fonte)
            : bruta;
    }

    private static Leitura<T> LerContador<T>(ManagementObject objeto, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            try
            {
                var valor = objeto[nome];
                if (valor is null)
                {
                    continue;
                }

                var convertido = Convert.ChangeType(valor, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                return Leitura<T>.Ok((T)convertido, "MSFT_StorageReliabilityCounter." + nome);
            }
            catch (Exception ex) when (ex is ManagementException or FormatException or InvalidCastException)
            {
                // propriedade ausente neste driver: tenta a próxima
            }
        }

        return Leitura<T>.NaoSuportado("O driver deste disco não expõe " + nomes[0] + ".");
    }

    // ------------------------------------------------------------------ benchmark

    /// <summary>
    /// Benchmark de disco: escreve <b>apenas</b> um arquivo próprio na pasta informada, confere o espaço
    /// livre antes, e apaga o arquivo no finally — inclusive em erro ou cancelamento.
    /// </summary>
    public async Task<ResultadoBenchmarkDisco> BenchmarkAsync(string pasta, long tamanhoBytes, IProgress<double>? progresso, CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(pasta))
        {
            throw new ArgumentException("Escolha uma pasta para o teste de escrita.", nameof(pasta));
        }

        Directory.CreateDirectory(pasta);
        var livre = EspacoLivre(pasta);
        var necessario = tamanhoBytes + (64L * 1024 * 1024);
        if (livre < necessario)
        {
            throw new InvalidOperationException(
                $"Espaço insuficiente em {pasta}: livre {FormatacaoBr.Bytes(livre)}, necessário {FormatacaoBr.Bytes(necessario)}.");
        }

        var caminho = Path.Combine(pasta, "flavos-benchmark-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            return await Task.Run(() => ExecutarBenchmark(caminho, tamanhoBytes, progresso, cancelamento), cancelamento);
        }
        finally
        {
            try
            {
                if (File.Exists(caminho))
                {
                    File.Delete(caminho);
                }
            }
            catch (IOException)
            {
                // arquivo travado por antivírus: fica registrado no console do app, não derruba o teste
            }
        }
    }

    private static ResultadoBenchmarkDisco ExecutarBenchmark(string caminho, long tamanho, IProgress<double>? progresso, CancellationToken cancelamento)
    {
        const int bloco = 1024 * 1024;
        var buffer = new byte[bloco];
        new Random(1234).NextBytes(buffer);
        var executadoEm = DateTimeOffset.Now;

        // Escrita sequencial
        var cronometro = Stopwatch.StartNew();
        long escrito = 0;
        using (var arquivo = new FileStream(caminho, FileMode.CreateNew, FileAccess.Write, FileShare.None, bloco, FileOptions.WriteThrough))
        {
            while (escrito < tamanho)
            {
                cancelamento.ThrowIfCancellationRequested();
                arquivo.Write(buffer, 0, bloco);
                escrito += bloco;
                progresso?.Report(Math.Min(0.45, escrito / (double)tamanho * 0.45));
            }

            arquivo.Flush(true);
        }

        cronometro.Stop();
        var escritaMbps = tamanho / 1024.0 / 1024 / Math.Max(0.001, cronometro.Elapsed.TotalSeconds);

        // Leitura sequencial
        cronometro.Restart();
        long lido = 0;
        using (var arquivo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.None, bloco, FileOptions.SequentialScan))
        {
            var destino = new byte[bloco];
            int l;
            while ((l = arquivo.Read(destino, 0, bloco)) > 0)
            {
                cancelamento.ThrowIfCancellationRequested();
                lido += l;
                progresso?.Report(0.45 + Math.Min(0.30, lido / (double)tamanho * 0.30));
            }
        }

        cronometro.Stop();
        var leituraMbps = tamanho / 1024.0 / 1024 / Math.Max(0.001, cronometro.Elapsed.TotalSeconds);

        // Leitura aleatória 4K
        var aleatorio = new Random(4321);
        var pequeno = new byte[4096];
        var latencias = new List<double>(4096);
        var operacoes = Math.Min(4096, Math.Max(256, tamanho / 4096));
        using (var arquivo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.RandomAccess))
        {
            cronometro.Restart();
            for (var i = 0; i < operacoes; i++)
            {
                cancelamento.ThrowIfCancellationRequested();
                var offset = (long)aleatorio.NextInt64(0, Math.Max(1, arquivo.Length - 4096) / 4096) * 4096;
                var inicio = Stopwatch.GetTimestamp();
                arquivo.Position = offset;
                arquivo.Read(pequeno, 0, 4096);
                latencias.Add(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
            }

            cronometro.Stop();
            progresso?.Report(0.95);
        }

        var segundosAleatorio = Math.Max(0.001, cronometro.Elapsed.TotalSeconds);
        latencias.Sort();
        var p95 = latencias.Count == 0 ? 0 : latencias[(int)Math.Min(latencias.Count - 1, Math.Floor(latencias.Count * 0.95))];
        var media = latencias.Count == 0 ? 0 : latencias.Average();
        progresso?.Report(1);

        return new ResultadoBenchmarkDisco
        {
            ExecutadoEm = executadoEm,
            Pasta = Path.GetDirectoryName(caminho) ?? string.Empty,
            TamanhoArquivoBytes = tamanho,
            SeqLeituraMbps = Math.Round(leituraMbps, 1),
            SeqEscritaMbps = Math.Round(escritaMbps, 1),
            Aleatorio4kLeituraIops = Math.Round(operacoes / segundosAleatorio, 0),
            LatenciaMediaMs = Math.Round(media, 3),
            LatenciaP95Ms = Math.Round(p95, 3),
        };
    }

    private static long EspacoLivre(string pasta)
    {
        var raiz = Path.GetPathRoot(Path.GetFullPath(pasta));
        if (string.IsNullOrEmpty(raiz))
        {
            return 0;
        }

        var info = new DriveInfo(raiz);
        return info.AvailableFreeSpace;
    }

    // ------------------------------------------------------------------ varredura de superfície

    /// <summary>
    /// Varredura de superfície: só leitura, sobre o próprio arquivo de teste criado aqui.
    /// Nunca lê setores de um disco do cliente diretamente, nunca escreve em setor algum.
    /// </summary>
    public async Task<ResultadoVarreduraSuperficie> VarrerSuperficieAsync(string caminhoArquivo, IProgress<double>? progresso, CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(caminhoArquivo))
        {
            throw new ArgumentException("Informe o arquivo de teste a ser lido.", nameof(caminhoArquivo));
        }

        return await Task.Run(() =>
        {
            const int bloco = 1024 * 1024;
            var executadoEm = DateTimeOffset.Now;
            var blocos = new List<BlocoSuperficie>();
            var lidos = 0;
            var lentos = 0;
            var erros = 0;
            var cancelada = false;
            var cronometro = Stopwatch.StartNew();
            long total = 0;

            var buffer = new byte[bloco];
            using var arquivo = new FileStream(caminhoArquivo, FileMode.Open, FileAccess.Read, FileShare.Read, bloco, FileOptions.SequentialScan);
            total = arquivo.Length;
            var posicao = 0L;

            while (posicao < total)
            {
                if (cancelamento.IsCancellationRequested)
                {
                    cancelada = true;
                    break;
                }

                var inicio = Stopwatch.GetTimestamp();
                try
                {
                    var l = arquivo.Read(buffer, 0, bloco);
                    if (l <= 0)
                    {
                        break;
                    }

                    var ms = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
                    var tipo = ms > 500 ? TipoBlocoSuperficie.Lento : TipoBlocoSuperficie.Ok;
                    if (tipo == TipoBlocoSuperficie.Lento)
                    {
                        lentos++;
                    }

                    lidos++;
                    blocos.Add(new BlocoSuperficie
                    {
                        Offset = posicao,
                        TamanhoBytes = l,
                        TempoMs = Math.Round(ms, 2),
                        Tipo = tipo,
                    });
                    posicao += l;
                }
                catch (IOException ex)
                {
                    erros++;
                    blocos.Add(new BlocoSuperficie
                    {
                        Offset = posicao,
                        TamanhoBytes = bloco,
                        TempoMs = Math.Round(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds, 2),
                        Tipo = TipoBlocoSuperficie.Erro,
                        Erro = ex.Message,
                    });
                    posicao += bloco;
                }

                progresso?.Report(total == 0 ? 1 : posicao / (double)total);
            }

            cronometro.Stop();
            return new ResultadoVarreduraSuperficie
            {
                ExecutadoEm = executadoEm,
                CaminhoArquivo = caminhoArquivo,
                TotalBytes = total,
                BlocosLidos = lidos,
                BlocosLentos = lentos,
                BlocosComErro = erros,
                TempoTotalSegundos = Math.Round(cronometro.Elapsed.TotalSeconds, 2),
                Cancelada = cancelada,
                Blocos = blocos.Take(500).ToList(),
            };
        }, cancelamento);
    }

    // ------------------------------------------------------------------ verificação do sistema de arquivos

    /// <summary>
    /// Verificação online do sistema de arquivos, somente leitura (`chkdsk /scan`).
    /// Não corrige, não desmonta o volume e não agenda nada para o próximo boot.
    /// </summary>
    public async Task<ResultadoVerificacaoSistemaArquivos> VerificarSistemaArquivosAsync(string volume, CancellationToken cancelamento = default)
    {
        var letra = string.IsNullOrWhiteSpace(volume) ? "C:" : volume.Trim().TrimEnd('\\');
        if (letra.Length == 1)
        {
            letra += ":";
        }

        if (letra.Length != 2 || !char.IsLetter(letra[0]) || letra[1] != ':')
        {
            throw new ArgumentException("Volume inválido. Informe uma letra de unidade, como C:.", nameof(volume));
        }

        var executavel = Path.Combine(Environment.SystemDirectory, "chkdsk.exe");
        var mensagens = new List<string>();
        var codigo = Leitura<int>.NaoLido();
        var interrompida = false;

        if (!File.Exists(executavel))
        {
            return new ResultadoVerificacaoSistemaArquivos
            {
                ExecutadoEm = DateTimeOffset.Now,
                Volume = letra,
                CodigoSaida = Leitura<int>.NaoSuportado("chkdsk.com não encontrado em " + Environment.SystemDirectory),
                CanceladaOuInterrompida = false,
                MensagensRelevantes = new[] { "A verificação não pôde ser executada." },
                TextoResumo = "Inconclusivo: o Windows não expôs a ferramenta de verificação.",
            };
        }

        var psi = new ProcessStartInfo(executavel)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(letra);
        psi.ArgumentList.Add("/scan");

        using var processo = new Process { StartInfo = psi };
        processo.Start();
        var saida = processo.StandardOutput.ReadToEndAsync();
        var erro = processo.StandardError.ReadToEndAsync();

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(TimeSpan.FromMinutes(45));

        try
        {
            await processo.WaitForExitAsync(limite.Token);
            codigo = Leitura<int>.Ok(processo.ExitCode, "chkdsk /scan");
        }
        catch (OperationCanceledException)
        {
            interrompida = true;
            try
            {
                processo.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // processo já terminou entre o cancelamento e o Kill
            }
        }

        var texto = (await saida) + Environment.NewLine + (await erro);
        foreach (var linha in texto.Split('\n'))
        {
            var limpa = linha.Trim();
            if (limpa.Length == 0)
            {
                continue;
            }

            if (limpa.Contains("problema", StringComparison.OrdinalIgnoreCase)
                || limpa.Contains("erro", StringComparison.OrdinalIgnoreCase)
                || limpa.Contains("corrig", StringComparison.OrdinalIgnoreCase)
                || limpa.Contains("agendad", StringComparison.OrdinalIgnoreCase)
                || limpa.Contains("não foi possível", StringComparison.OrdinalIgnoreCase))
            {
                mensagens.Add(limpa);
            }
        }

        var resumo = interrompida
            ? "Verificação interrompida por cancelamento ou tempo limite (45 min). Resultado: Inconclusivo."
            : codigo is { Disponivel: true } c
                ? c.Valor == 0
                    ? "Nenhum problema encontrado na verificação do sistema de arquivos."
                    : $"A verificação terminou com código {c.Valor}. Veja as mensagens abaixo e considere backup."
                : "Resultado da verificação desconhecido.";

        return new ResultadoVerificacaoSistemaArquivos
        {
            ExecutadoEm = DateTimeOffset.Now,
            Volume = letra,
            CodigoSaida = codigo,
            CanceladaOuInterrompida = interrompida,
            MensagensRelevantes = mensagens.Take(50).ToList(),
            TextoResumo = resumo,
        };
    }
}
