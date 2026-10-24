using System.Diagnostics;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Providers.Abstractions;
using FlavosDiag.Providers.Dxgi;

namespace FlavosDiag.Providers.Wmi;

/// <summary>
/// Inventário de hardware via WMI/CIM (somente leitura) + DXGI para VRAM + APIs de exibição.
/// Cada classe WMI é uma "seção" independente: falha em uma não impede as outras.
/// </summary>
public sealed class InventarioWmiProvider : IInventarioProvider
{
    private readonly IWmiFonte _wmi;
    private readonly IAmbiente _ambiente;
    private readonly IDxgiInfo _dxgi;

    public InventarioWmiProvider(IWmiFonte wmi, IAmbiente ambiente, IDxgiInfo? dxgi = null)
    {
        _wmi = wmi;
        _ambiente = ambiente;
        _dxgi = dxgi ?? new DxgiInfo();
    }

    public string Nome => "WMI/CIM + DXGI";

    public async Task<InventarioSistema> ColetarAsync(CancellationToken cancelamento = default)
    {
        var limitacoes = new Limitacoes();
        var secoes = new SecoesWmi(await CarregarSecoesAsync(_wmi, cancelamento));

        var sistemaTask = ColetarSistemaAsync(secoes, limitacoes, cancelamento);
        var biosTask = ColetarBiosAsync(secoes, limitacoes, cancelamento);
        var placaTask = ColetarPlacaMaeAsync(secoes, limitacoes, cancelamento);
        var cpuTask = ColetarCpuAsync(secoes, limitacoes, cancelamento);
        var memoriaTask = ColetarMemoriaAsync(secoes, limitacoes, cancelamento);
        var gpuTask = ColetarGpuAsync(secoes, limitacoes, cancelamento);
        var redeTask = ColetarRedeAsync(secoes, limitacoes, cancelamento);
        var bateriaTask = ColetarBateriaAsync(secoes, limitacoes, cancelamento);
        var dispTask = ColetarDispositivosComProblemaAsync(secoes, limitacoes, cancelamento);

        await Task.WhenAll(sistemaTask, biosTask, placaTask, cpuTask, memoriaTask, gpuTask, redeTask, bateriaTask, dispTask);

        var monitores = ColetarMonitores(limitacoes);

        return new InventarioSistema
        {
            Sistema = await sistemaTask,
            Bios = await biosTask,
            PlacaMae = await placaTask,
            Cpu = await cpuTask,
            Memoria = await memoriaTask,
            Gpus = await gpuTask,
            Monitores = monitores,
            Adaptadores = await redeTask,
            Bateria = await bateriaTask,
            DispositivosComProblema = await dispTask,
            ColetadoEm = DateTimeOffset.Now,
            Limitacoes = limitacoes.Itens,
        };
    }

    private static readonly string[] Classes =
    {
        "Win32_OperatingSystem", "Win32_ComputerSystem", "Win32_BIOS", "Win32_BaseBoard", "Win32_Processor",
        "Win32_PhysicalMemory", "Win32_PhysicalMemoryArray", "Win32_VideoController", "Win32_Battery",
        "Win32_PnPEntity", "Win32_NetworkAdapter", "Win32_NetworkAdapterConfiguration",
    };

    public static async Task<Dictionary<string, IReadOnlyList<WmiLinha>>> CarregarSecoesAsync(IWmiFonte wmi, CancellationToken cancelamento)
    {
        var resultado = new Dictionary<string, IReadOnlyList<WmiLinha>>();
        foreach (var classe in Classes)
        {
            var consulta = classe == "Win32_PnPEntity"
                ? "SELECT Name, ConfigManagerErrorCode, Manufacturer, DeviceID FROM Win32_PnPEntity"
                : $"SELECT * FROM {classe}";
            var tempo = Stopwatch.StartNew();
            var tarefa = wmi.ConsultarAsync(consulta, "root\\cimv2", cancelamento);
            try
            {
                resultado[classe] = await tarefa.WaitAsync(TimeSpan.FromSeconds(25), cancelamento);
            }
            catch (TimeoutException)
            {
                resultado[classe] = Array.Empty<WmiLinha>();
            }
            catch (WmiIndisponivelException)
            {
                resultado[classe] = Array.Empty<WmiLinha>();
            }

            _ = tempo.Elapsed;
        }

        return resultado;
    }

    internal sealed record SecoesWmi(Dictionary<string, IReadOnlyList<WmiLinha>> Dados)
    {
        public IReadOnlyList<WmiLinha> this[string classe] => Dados.TryGetValue(classe, out var v) ? v : Array.Empty<WmiLinha>();

        public bool Vazio(string classe) => this[classe].Count == 0;
    }

    private static string MotivoVazio(string classe) =>
        $"A classe WMI {classe} não retornou dados (serviço WMI indisponível, sem permissão ou hardware não expõe).";

    // ---------------- Sistema operacional ----------------
    private async Task<InfoSistema> ColetarSistemaAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var so = s["Win32_OperatingSystem"].FirstOrDefault();
        var cs = s["Win32_ComputerSystem"].FirstOrDefault();

        var nome = so is null || s.Vazio("Win32_OperatingSystem")
            ? Leitura<string>.NaoSuportado(MotivoVazio("Win32_OperatingSystem"), "WMI Win32_OperatingSystem")
            : Filtrar(so.Texto("Caption"), "Sistema operacional");

        var versao = so?.Texto("DisplayVersion", "Version") is { } dv
            ? Leitura<string>.Ok(dv, "WMI Win32_OperatingSystem")
            : Leitura<string>.NaoSuportado("Versão não exposta.", "WMI Win32_OperatingSystem");

        var versaoRegistro = LeitorRegistro.LerString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
        var buildRegistro = LeitorRegistro.LerString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber");
        var ubr = LeitorRegistro.LerString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR");
        if (versaoRegistro is not null)
        {
            versao = Leitura<string>.Ok(versaoRegistro, "Registro: CurrentVersion\\DisplayVersion");
        }

        var buildTexto = buildRegistro is not null
            ? (ubr is not null ? $"{buildRegistro}.{ubr}" : buildRegistro)
            : so?.Texto("BuildNumber");
        var build = buildTexto is not null
            ? Leitura<string>.Ok(buildTexto, buildRegistro is not null ? "Registro: CurrentBuildNumber" : "WMI Win32_OperatingSystem")
            : Leitura<string>.NaoSuportado("Build não exposto.");

        var arquitetura = so?.Texto("OSArchitecture") ?? System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
        var pc = cs?.Texto("Name") ?? Environment.MachineName;
        var usuario = so?.Texto("RegisteredUser") ?? Environment.UserName;

        Leitura<DateTime> instalacao;
        var dt = so?.Valor("InstallDate");
        if (dt is string textoData && textoData.Length >= 8 && DateTime.TryParseExact(textoData[..8], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d))
        {
            instalacao = Leitura<DateTime>.Ok(d, "WMI Win32_OperatingSystem.InstallDate");
        }
        else
        {
            instalacao = Leitura<DateTime>.NaoSuportado("Data de instalação não exposta.");
        }

        var ativacao = await ColetarAtivacaoAsync();

        lim.AdicionarSeAusente("Ativação do Windows", ativacao);
        lim.AdicionarSeAusente("Data de instalação do sistema", instalacao);

        return new InfoSistema
        {
            Nome = nome,
            Versao = versao,
            Build = build,
            Arquitetura = Leitura<string>.Ok(arquitetura, "WMI Win32_OperatingSystem.OSArchitecture"),
            NomeComputador = Leitura<string>.Ok(pc, "WMI Win32_ComputerSystem.Name"),
            Usuario = Leitura<string>.Ok(usuario, "WMI Win32_OperatingSystem.RegisteredUser"),
            DataInstalacao = instalacao,
            Ativacao = ativacao,
        };
    }

    /// <summary>Ativação: SOMENTE informativa. Não altera licença.</summary>
    public static Leitura<string> InterpretarAtivacao(WmiLinha? produto)
    {
        var status = produto?.Inteiro("LicenseStatus");
        if (status is null)
        {
            return Leitura<string>.SemPermissao(
                "Status de ativação não disponível para as credenciais atuais (informativo).",
                "WMI SoftwareLicensingProduct");
        }

        return status.Value switch
        {
            0 => Leitura<string>.Ok("Não ativado", "WMI SoftwareLicensingProduct"),
            1 => Leitura<string>.Ok("Ativado", "WMI SoftwareLicensingProduct"),
            2 => Leitura<string>.Ok("Período de tolerância", "WMI SoftwareLicensingProduct"),
            3 => Leitura<string>.Ok("Tolerância de ativação adicional", "WMI SoftwareLicensingProduct"),
            5 => Leitura<string>.Ok("Notificação (não ativado)", "WMI SoftwareLicensingProduct"),
            6 => Leitura<string>.Ok("Tolerância estendida", "WMI SoftwareLicensingProduct"),
            _ => Leitura<string>.Ok($"Status {status.Value}", "WMI SoftwareLicensingProduct"),
        };
    }

    private async Task<Leitura<string>> ColetarAtivacaoAsync()
    {
        try
        {
            var linhas = await _wmi.ConsultarAsync(
                    "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND ApplicationID = '55c92734-d682-4d71-983e-d6ec3f16059f'",
                    "root\\cimv2",
                    CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (linhas.Count == 0)
            {
                return Leitura<string>.NaoSuportado("Nenhuma licença de produto encontrada (informativo).", "WMI SoftwareLicensingProduct");
            }

            return InterpretarAtivacao(linhas[0]);
        }
        catch (TimeoutException)
        {
            return Leitura<string>.SemPermissao("Consulta de ativação excedeu o tempo (informativo).", "WMI SoftwareLicensingProduct");
        }
        catch (WmiIndisponivelException ex)
        {
            return Leitura<string>.SemPermissao("Ativação não disponível (informativo): " + ex.Message, "WMI SoftwareLicensingProduct");
        }
    }

    // ---------------- BIOS ----------------
    private Task<InfoBios> ColetarBiosAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var bios = s["Win32_BIOS"].FirstOrDefault();
        var modo = bios is null
            ? Leitura<string>.NaoSuportado("Modo de inicialização não informado.")
            : Leitura<string>.Ok("Não determinado pelo Windows (consulte o Setup do fabricante)", "SMBIOS");

        var info = new InfoBios
        {
            Fabricante = TextoOuAusente(bios, lim, "BIOS (fabricante)", "Win32_BIOS", "Manufacturer"),
            Versao = TextoOuAusente(bios, lim, "BIOS (versão)", "Win32_BIOS", "SMBIOSBIOSVersion", "Version"),
            Data = bios?.DataWmi("ReleaseDate") is { } d
                ? Leitura<DateTime>.Ok(d, "Win32_BIOS.ReleaseDate")
                : AusenteData(lim, "BIOS (data)", "Win32_BIOS"),
            Modo = modo,
        };
        return Task.FromResult(info);
    }

    // ---------------- Placa-mãe ----------------
    private Task<InfoPlacaMae> ColetarPlacaMaeAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var placa = s["Win32_BaseBoard"].FirstOrDefault();
        return Task.FromResult(new InfoPlacaMae
        {
            Fabricante = TextoOuAusente(placa, lim, "Placa-mãe (fabricante)", "Win32_BaseBoard", "Manufacturer"),
            Modelo = TextoOuAusente(placa, lim, "Placa-mãe (modelo)", "Win32_BaseBoard", "Product"),
            Versao = TextoOuAusente(placa, lim, "Placa-mãe (versão)", "Win32_BaseBoard", "Version"),
            NumeroSerie = TextoOuAusente(placa, lim, "Placa-mãe (número de série)", "Win32_BaseBoard", "SerialNumber"),
        });
    }

    // ---------------- CPU ----------------
    private Task<InfoCpu> ColetarCpuAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var cpu = s["Win32_Processor"].FirstOrDefault();
        Leitura<int> nucleos = cpu?.Inteiro("NumberOfCores") is { } nc
            ? Leitura<int>.Ok((int)nc, "Win32_Processor.NumberOfCores")
            : Leitura<int>.NaoSuportado(MotivoVazio("Win32_Processor"), "Win32_Processor");
        Leitura<int> threads = cpu?.Inteiro("NumberOfLogicalProcessors") is { } nt
            ? Leitura<int>.Ok((int)nt, "Win32_Processor.NumberOfLogicalProcessors")
            : Leitura<int>.NaoSuportado(MotivoVazio("Win32_Processor"), "Win32_Processor");

        lim.AdicionarSeAusente("Núcleos físicos da CPU", nucleos);
        lim.AdicionarSeAusente("Threads da CPU", threads);

        return Task.FromResult(new InfoCpu
        {
            Nome = TextoOuAusente(cpu, lim, "CPU (modelo)", "Win32_Processor", "Name"),
            Fabricante = TextoOuAusente(cpu, lim, "CPU (fabricante)", "Win32_Processor", "Manufacturer"),
            NucleosFisicos = nucleos,
            ThreadsLogicas = threads,
            ClockBaseMhz = cpu?.Inteiro("MaxClockSpeed") is { } mc
                ? Leitura<double>.Ok(mc, "Win32_Processor.MaxClockSpeed")
                : Leitura<double>.NaoSuportado("Clock base não informado.", "Win32_Processor"),
            ClockMaximoMhz = cpu?.Inteiro("MaxClockSpeed") is { } mcm
                ? Leitura<double>.Ok(mcm, "Win32_Processor.MaxClockSpeed")
                : Leitura<double>.NaoSuportado("Clock máximo não informado.", "Win32_Processor"),
            CacheL2Kb = cpu?.Inteiro("L2CacheSize") is { } l2
                ? Leitura<double>.Ok(l2, "Win32_Processor.L2CacheSize")
                : Leitura<double>.NaoSuportado("Cache L2 não exposto.", "Win32_Processor"),
            CacheL3Kb = cpu?.Inteiro("L3CacheSize") is { } l3
                ? Leitura<double>.Ok(l3, "Win32_Processor.L3CacheSize")
                : Leitura<double>.NaoSuportado("Cache L3 não exposto.", "Win32_Processor"),
            Arquitetura = cpu?.Inteiro("AddressWidth") is { } aw
                ? Leitura<string>.Ok(aw + "-bit", "Win32_Processor.AddressWidth")
                : Leitura<string>.NaoSuportado("Arquitetura não exposta."),
        });
    }

    // ---------------- Memória ----------------
    private Task<InfoMemoria> ColetarMemoriaAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var modulosWmi = s["Win32_PhysicalMemory"];
        var array = s["Win32_PhysicalMemoryArray"].FirstOrDefault();

        var modulos = new List<ModuloMemoria>();
        foreach (var m in modulosWmi)
        {
            var capacidade = m.Inteiro("Capacity");
            modulos.Add(new ModuloMemoria
            {
                Local = TextoOu(m.Texto("DeviceLocator", "BankLabel"), "Slot"),
                CapacidadeBytes = capacidade is > 0
                    ? Leitura<long>.Ok(capacidade.Value, "Win32_PhysicalMemory.Capacity")
                    : Leitura<long>.NaoSuportado("Capacidade do módulo não informada."),
                Tipo = m.Inteiro("SMBIOSMemoryType") is { } tipo
                    ? Leitura<string>.Ok(NomeTipoMemoria((int)tipo), "Win32_PhysicalMemory.SMBIOSMemoryType")
                    : m.Inteiro("MemoryType") is { } mt && mt > 0
                        ? Leitura<string>.Ok(NomeTipoMemoriaAntigo((int)mt), "Win32_PhysicalMemory.MemoryType")
                        : Leitura<string>.NaoSuportado("Tipo de memória não exposto."),
                VelocidadeNominalMhz = m.Inteiro("Speed") is { } sp
                    ? Leitura<int>.Ok((int)sp, "Win32_PhysicalMemory.Speed")
                    : Leitura<int>.NaoSuportado("Velocidade nominal não informada."),
                VelocidadeConfiguradaMhz = m.Inteiro("ConfiguredClockSpeed") is { } cf
                    ? Leitura<int>.Ok((int)cf, "Win32_PhysicalMemory.ConfiguredClockSpeed")
                    : Leitura<int>.NaoSuportado("Clock configurado não informado."),
                Fabricante = TextoOu(m.Texto("Manufacturer"), "Não informado"),
                NumeroPeca = TextoOu(m.Texto("PartNumber"), "Não informado"),
                NumeroSerie = TextoOu(m.Texto("SerialNumber"), "Não informado"),
            });
        }

        var total = modulos.Where(x => x.CapacidadeBytes.Disponivel).Sum(x => x.CapacidadeBytes.Valor);
        Leitura<long> totalLeitura = modulos.Count == 0
            ? Leitura<long>.NaoSuportado(MotivoVazio("Win32_PhysicalMemory"), "Win32_PhysicalMemory")
            : total > 0 ? Leitura<long>.Ok(total, "Win32_PhysicalMemory.Capacity") : Leitura<long>.NaoSuportado("Capacidade total inválida.");

        var slotsTotais = array?.Inteiro("MemoryDevices") is { } st
            ? Leitura<int>.Ok((int)st, "Win32_PhysicalMemoryArray.MemoryDevices")
            : Leitura<int>.NaoSuportado("Total de slots não informado.");

        lim.AdicionarSeAusente("Memória instalada", totalLeitura);
        lim.Adicionar("Timings de memória (CAS/RAS/CL)", "Sem fonte confiável com licença compatível foi confirmada; o dado não é exibido.");

        return Task.FromResult(new InfoMemoria
        {
            TotalBytes = totalLeitura,
            SlotsUsados = Leitura<int>.Ok(modulos.Count, "Win32_PhysicalMemory"),
            SlotsTotais = slotsTotais,
            Timings = Leitura<string>.NaoSuportado("Não disponível: nenhuma fonte confiável e com licença compatível foi confirmada."),
            Modulos = modulos,
        });
    }

    /// <summary>SMBIOS Memory Device Type (Type 17, offset 0x12).</summary>
    public static string NomeTipoMemoria(int smbios) => smbios switch
    {
        2 => "DRAM",
        9 => "RAM",
        11 => "SRAM",
        12 => "Flash",
        13 => "EEPROM",
        15 => "3DRAM",
        16 => "SDRAM",
        17 => "SGRAM",
        18 => "RDRAM",
        19 => "DDR",
        20 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        36 => "HBM3",
        _ => "Tipo " + smbios,
    };

    private static string NomeTipoMemoriaAntigo(int tipo) => tipo switch
    {
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2 FB-DIMM",
        24 => "DDR3",
        _ => "Tipo " + tipo,
    };

    // ---------------- GPU ----------------
    private async Task<IReadOnlyList<InfoGpu>> ColetarGpuAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var lista = new List<InfoGpu>();
        IReadOnlyList<AdaptadorDxgi> dxgi;
        try
        {
            dxgi = await Task.Run(() => _dxgi.ListarAdaptadores(), ct).WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            dxgi = Array.Empty<AdaptadorDxgi>();
            lim.Adicionar("VRAM dedicada da GPU", "DXGI não respondeu: " + ex.Message);
        }

        foreach (var g in s["Win32_VideoController"])
        {
            var nome = g.Texto("Name");
            var vram = dxgi.FirstOrDefault(a => nome is not null && a.Descricao.Equals(nome, StringComparison.OrdinalIgnoreCase))
                       ?? (dxgi.Count == 1 ? dxgi[0] : null);

            if (vram is null)
            {
                lim.Adicionar("VRAM dedicada da GPU", "Não foi possível associar o adaptador DXGI à GPU WMI.");
            }

            lista.Add(new InfoGpu
            {
                Nome = nome is not null ? Leitura<string>.Ok(nome, "Win32_VideoController.Name") : Leitura<string>.NaoSuportado("GPU sem nome."),
                VramDedicadaBytes = vram is not null
                    ? Leitura<long>.Ok(vram.VramDedicadaBytes, "DXGI IDXGIAdapter1.GetDesc1.DedicatedVideoMemory")
                    : Leitura<long>.NaoSuportado("DXGI não expôs o adaptador.", "DXGI"),
                VersaoDriver = TextoOuAusente(g, lim, "Versão do driver de vídeo", "Win32_VideoController", "DriverVersion"),
                DataDriver = g.DataWmi("DriverDate") is { } dd
                    ? Leitura<DateTime>.Ok(dd, "Win32_VideoController.DriverDate")
                    : Leitura<DateTime>.NaoSuportado("Data do driver não informada.", "Win32_VideoController"),
                Fabricante = TextoOuAusente(g, lim, "Fabricante da GPU", "Win32_VideoController", "AdapterCompatibility"),
            });
        }

        // GPUs expostas só pelo DXGI (pouco comum): informa VRAM sem inventar nome.
        if (lista.Count == 0 && dxgi.Count > 0)
        {
            lista.AddRange(dxgi.Select(d => new InfoGpu
            {
                Nome = Leitura<string>.Ok(d.Descricao, "DXGI"),
                VramDedicadaBytes = Leitura<long>.Ok(d.VramDedicadaBytes, "DXGI"),
                VersaoDriver = Leitura<string>.NaoSuportado("Lido apenas pelo DXGI."),
                DataDriver = Leitura<DateTime>.NaoSuportado("Lido apenas pelo DXGI."),
                Fabricante = Leitura<string>.Ok(d.VendorId.ToString("X4"), "DXGI VendorId"),
            }));
        }

        return lista;
    }

    // ---------------- Rede ----------------
    private async Task<IReadOnlyList<InfoAdaptadorRede>> ColetarRedeAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var fisicos = s["Win32_NetworkAdapter"]
            .Where(l => l.Booleano("PhysicalAdapter") != false)
            .ToList();

        if (fisicos.Count == 0)
        {
            // Fallback: APIs de rede do .NET (não depende de WMI).
            var adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => a.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .Select(a => new InfoAdaptadorRede
                {
                    Nome = Leitura<string>.Ok(a.Name, ".NET NetworkInterface"),
                    Descricao = Leitura<string>.Ok(a.Description, ".NET NetworkInterface"),
                    Mac = a.GetPhysicalAddress().GetAddressBytes() is { Length: > 0 } mac
                        ? Leitura<string>.Ok(string.Join(":", mac.Select(b => b.ToString("X2"))), ".NET NetworkInterface")
                        : Leitura<string>.NaoSuportado("Sem endereço físico exposto."),
                    VelocidadeBitsPorSegundo = a.Speed > 0 ? Leitura<long>.Ok(a.Speed, ".NET NetworkInterface.Speed") : Leitura<long>.NaoSuportado("Velocidade desconhecida."),
                    Tipo = Leitura<string>.Ok(TraducaoTipoRede(a.NetworkInterfaceType.ToString()), ".NET NetworkInterfaceType"),
                    Ativo = a.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up,
                }).ToList();
            lim.Adicionar("Adaptadores de rede (WMI)", MotivoVazio("Win32_NetworkAdapter") + " Usado fallback .NET.");
            return await Task.FromResult(adapters);
        }

        return fisicos.Select(l =>
        {
            var tipo = l.Inteiro("AdapterTypeID") is { } t ? TipoAdaptadorRede((int)t) : "Desconhecido";
            return new InfoAdaptadorRede
            {
                Nome = TextoOu(l.Texto("NetConnectionID", "Name"), "Adaptador sem nome"),
                Descricao = Leitura<string>.Ok(l.Texto("Description") ?? "Sem descrição", "Win32_NetworkAdapter.Description"),
                Mac = l.Texto("MACAddress") is { } mac
                    ? Leitura<string>.Ok(mac, "Win32_NetworkAdapter.MACAddress")
                    : Leitura<string>.NaoSuportado("Sem endereço físico."),
                VelocidadeBitsPorSegundo = l.Inteiro("Speed") is { } sp && sp > 0
                    ? Leitura<long>.Ok(sp, "Win32_NetworkAdapter.Speed")
                    : Leitura<long>.NaoSuportado("Velocidade não informada."),
                Tipo = Leitura<string>.Ok(tipo, "Win32_NetworkAdapter.AdapterTypeID"),
                Ativo = l.Valor("NetConnectionStatus") is { } ns && Convert.ToInt64(ns) == 2,
            };
        }).ToList();
    }

    /// <summary>Win32_NetworkAdapter.AdapterTypeID.</summary>
    public static string TipoAdaptadorRede(int id) => id switch
    {
        0 => "Ethernet 802.3",
        1 => "Token Ring 802.5",
        2 => "FDDI",
        3 => "WAN",
        4 => "LocalTalk",
        5 => "Ethernet sem fio (Wi-Fi)",
        6 => "Rede de linha discada",
        7 => "Rede de linha dedicada",
        8 => "Loopback",
        9 => "Rede de fibra (ATM)",
        10 => "Rede de fibra (SONET)",
        _ => "Desconhecido",
    };

    internal static string TraducaoTipoRede(string tipoNet) => tipoNet switch
    {
        "Ethernet" => "Ethernet",
        "Wireless80211" => "Wi-Fi",
        "GigabitEthernet" => "Ethernet gigabit",
        "FastEthernetFx" => "Ethernet",
        _ => tipoNet,
    };

    // ---------------- Bateria ----------------
    private Task<InfoBateria> ColetarBateriaAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var bat = s["Win32_Battery"].FirstOrDefault();
        if (bat is null)
        {
            return Task.FromResult(new InfoBateria
            {
                Presente = false,
                Nome = Leitura<string>.NaoSuportado("Sem bateria detectada."),
                Fabricante = Leitura<string>.NaoSuportado("Sem bateria detectada."),
            });
        }

        return Task.FromResult(new InfoBateria
        {
            Presente = true,
            Nome = TextoOuAusente(bat, lim, "Bateria (nome)", "Win32_Battery", "Name"),
            Fabricante = TextoOuAusente(bat, lim, "Bateria (fabricante)", "Win32_Battery", "Manufacturer"),
        });
    }

    // ---------------- Dispositivos com problema ----------------
    private Task<IReadOnlyList<DispositivoComProblema>> ColetarDispositivosComProblemaAsync(SecoesWmi s, Limitacoes lim, CancellationToken ct)
    {
        var lista = s["Win32_PnPEntity"]
            .Where(l => (l.Inteiro("ConfigManagerErrorCode") ?? 0) != 0)
            .Select(l => new DispositivoComProblema
            {
                Nome = l.Texto("Name") ?? "(dispositivo sem nome)",
                CodigoErro = (int)(l.Inteiro("ConfigManagerErrorCode") ?? 0),
                Descricao = CodigoPnp.Descricao((int)(l.Inteiro("ConfigManagerErrorCode") ?? 0)),
                Fabricante = l.Texto("Manufacturer"),
                DeviceId = l.Texto("DeviceID"),
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<DispositivoComProblema>>(lista);
    }

    // ---------------- Monitores ----------------
    private IReadOnlyList<InfoMonitor> ColetarMonitores(Limitacoes lim)
    {
        var lista = new List<InfoMonitor>();
        try
        {
            var dd = new ApiTela.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<ApiTela.DISPLAY_DEVICE>() };
            var i = 0u;
            while (ApiTela.EnumDisplayDevices(null, i, ref dd, 0))
            {
                if ((dd.StateFlags & 1) == 0) // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
                {
                    i++;
                    dd = new ApiTela.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<ApiTela.DISPLAY_DEVICE>() };
                    continue;
                }

                var modo = ApiTela.ModoAtual(dd.DeviceName);
                if (modo.dmPelsWidth > 0)
                {
                    lista.Add(new InfoMonitor
                    {
                        Largura = Leitura<int>.Ok((int)modo.dmPelsWidth, "EnumDisplaySettings"),
                        Altura = Leitura<int>.Ok((int)modo.dmPelsHeight, "EnumDisplaySettings"),
                        TaxaAtualizacaoHz = Leitura<int>.Ok((int)modo.dmDisplayFrequency, "EnumDisplaySettings"),
                        Nome = TextoOu(dd.DeviceString, dd.DeviceName),
                    });
                }

                i++;
                dd = new ApiTela.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<ApiTela.DISPLAY_DEVICE>() };
            }
        }
        catch (Exception ex)
        {
            lim.Adicionar("Telas", "Não foi possível listar os monitores: " + ex.Message);
        }

        if (lista.Count == 0 && !lim.Itens.Any(l => l.Item == "Telas"))
        {
            lim.Adicionar("Telas", "Nenhum monitor com modo ativo foi enumerado.");
        }

        return lista;
    }

    // ---------------- helpers ----------------
    private static Leitura<string> TextoOuAusente(WmiLinha? linha, Limitacoes lim, string item, string fonte, params string[] nomes)
    {
        var texto = linha?.Texto(nomes);
        if (texto is not null)
        {
            return Leitura<string>.Ok(texto, "WMI " + fonte);
        }

        var leitura = linha is null
            ? Leitura<string>.NaoSuportado("Classe WMI " + fonte + " não retornou dados.", "WMI " + fonte)
            : Leitura<string>.NaoSuportado("Campo não preenchido pelo fabricante.", "WMI " + fonte);
        lim.AdicionarSeAusente(item, leitura);
        return leitura;
    }

    private static Leitura<DateTime> AusenteData(Limitacoes lim, string item, string fonte)
    {
        var leitura = Leitura<DateTime>.NaoSuportado("Campo não preenchido pelo fabricante.", "WMI " + fonte);
        lim.AdicionarSeAusente(item, leitura);
        return leitura;
    }

    private static Leitura<string> Filtrar(string? texto, string item) =>
        texto is not null
            ? Leitura<string>.Ok(texto, "WMI")
            : Leitura<string>.NaoSuportado(item + " não informado.");

    private static Leitura<string> TextoOu(string? texto, string padrao)
    {
        if (!string.IsNullOrWhiteSpace(texto))
        {
            return Leitura<string>.Ok(texto.Trim(), "WMI");
        }

        return Leitura<string>.Ok(padrao, "WMI");
    }
}

internal static class ValorExt
{
    public static object? Valor(this WmiLinha linha, string nome) =>
        linha.TryGetValue(nome, out var v) ? v : null;
}

/// <summary>Descrições em linguagem própria dos códigos de erro do Gerenciador de Dispositivos (Windows).</summary>
public static class CodigoPnp
{
    public static string Descricao(int codigo) => codigo switch
    {
        1 => "O dispositivo não está configurado corretamente.",
        3 => "O driver do dispositivo pode estar corrompido.",
        10 => "O dispositivo não pode ser iniciado.",
        12 => "Não há recursos livres suficientes para o dispositivo.",
        14 => "O dispositivo precisa ser reiniciado.",
        18 => "Os drivers do dispositivo precisam ser reinstalados.",
        19 => "O registro do dispositivo está incompleto ou corrompido.",
        21 => "O dispositivo está sendo removido.",
        22 => "O dispositivo está desabilitado.",
        24 => "O dispositivo não está presente, não funciona ou não tem driver instalado.",
        28 => "Os drivers do dispositivo não estão instalados.",
        31 => "O dispositivo não consegue carregar o driver necessário.",
        32 => "O driver de inicialização do dispositivo está desabilitado.",
        37 => "O Windows não inicializou o driver do dispositivo.",
        39 => "O driver está corrompido ou ausente.",
        43 => "O dispositivo relatou falha ao Windows.",
        45 => "O dispositivo não está conectado no momento.",
        52 => "Não foi possível verificar a assinatura dos drivers do dispositivo.",
        54 => "O dispositivo falhou e não está mais disponível.",
        _ => $"Código de erro {codigo} reportado pelo Gerenciador de Dispositivos.",
    };
}
