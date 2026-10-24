# Flavos Diag — Procedência

> Para cada decisão técnica: de onde veio (documento público, padrão público, biblioteca, ou decisão própria) e por quê.
> Regra: tudo que está no código deve estar ligado a uma linha daqui e a uma seção de `ESPECIFICACAO.md`.

## Como ler
- **[DOC]** = documentação oficial / padrão público / documentação de biblioteca (com link).
- **[CONH]** = conhecimento técnico geral e público (sem fonte única; explicado no texto).
- **[DECISAO]** = decisão própria de projeto (trade-off, limitação, escolha de valor).

---

## Fase 0 — arquitetura e base

| # | Decisão | Origem |
|---|---|---|
| P0-01 | .NET 8 LTS, WPF, MVVM com CommunityToolkit.Mvvm | Briefing (D). `ObservableObject`/`RelayCommand`: [docs CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/). **[DECISAO]** versão 8.4.2 (última estável, MIT). |
| P0-02 | Tema Fluent via WPF-UI 4.2.1 | Briefing propõe "ex.: WPF-UI", confirmamos MIT. **[DOC]** [repo WPF-UI](https://github.com/lepoco/wpfui). Usamos apenas `ApplicationThemeManager` e estilos base; a identidade visual (cores, tipografia, ícones) é nossa. |
| P0-03 | `Leitura<T>` com estado explícito | **[DECISAO]** derivada do requisito anti-falsificação B3: um dado ausente precisa carregar a causa. Não existe no .NET um tipo nativo para isso. |
| P0-04 | Interface `IInventarioProvider` etc. + fakes | **[CONH]** injeção de dependência clássica; requisito D ("cada um com versão fake para testes"). |
| P0-05 | WMI/CIM via `System.Management` `ManagementObjectSearcher` | **[DOC]** [WMI C# example](https://learn.microsoft.com/windows/win32/wmisdk/example--getting-wmi-data-from-the-local-computer), classes `Win32_*`: [link](https://learn.microsoft.com/windows/win32/cimwin32prov/cim-win32-provider). |
| P0-06 | Data em ISO 8601 no JSON, dd/MM/yyyy na interface | Briefing (D). Formatação via `CultureInfo("pt-BR")` com formato explícito. **[DECISAO]** usar `"yyyy-MM-ddTHH:mm:sszzz"` no JSON. |
| P0-07 | Mascaramento de serial por padrão | Requisito C. **[DECISAO]** os 4 primeiros caracteres + `****` + 4 últimos quando o serial tem ≥ 12 caracteres; abaixo disso, só `****`. Configurável? Não mantemos modo "padrão = completo": configurável sim, mas default mascarado. |
| P0-08 | Sem single-file até a Fase 5; publish pastas | **[DECISAO]** debugging mais simples; Fase 5 decide single-file vs pasta. WPF não suporta `PublishSingleFile` 100% no .NET 8? Verificado no teste de publish da Fase 5; se falhar, BLOQUEADO com causa. |

## Fase 1 — inventário e diagnóstico rápido

| # | Decisão | Origem |
|---|---|---|
| P1-01 | CPU: núcleos físicos/threads de `Win32_Processor.NumberOfCores/NumberOfLogicalProcessors`; clock base de `MaxClockSpeed`; caches de `L2CacheSize/L3CacheSize` (KB) | **[DOC]** [Win32_Processor](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-processor). Limitação: `L2CacheSize` é a soma dos caches L2 visível ao WMI; documentado na interface. |
| P1-02 | Arquitetura da CPU de `PROCESSOR_ARCHITECTURE` e `PROCESSOR_IDENTIFIER` | **[DOC]** [Windows environment variables](https://learn.microsoft.com/windows/win32/procthread/environment-variables). |
| P1-03 | SO: `Environment.OSVersion` limitado; usamos `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` (leitura) `ProductName`, `DisplayVersion`, `CurrentBuildNumber`, `BuildLabEx` | **[DOC]** [ìmperícia de registro compatível](https://learn.microsoft.com/windows/win32/sysinfo/getting-the-system-version) (documented workaround: verificar versão real do OS pelo número de build). |
| P1-04 | Ativação "informativa": leitura de `SoftwareLicensingProduct` → campo `LicenseStatus` → texto (Ativado / Não ativado / Desconhecido) | **[DOC]** [SoftwareLicensingProduct (SLP) WMI](https://learn.microsoft.com/windows/win32/slapi/about-software-licensing-api). Se indisponível: Inconclusivo. Uso **apenas informativo**, sem alterar licença. |
| P1-05 | BIOS placa-mãe via `Win32_BIOS`, `Win32_BaseBoard`; datas de `ReleaseDate` (convertidas de `yyyymmdd...`) | **[DOC]** [Win32_BIOS](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-bios) [Win32_BaseBoard](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-baseboard) |
| P1-06 | GPU VRAM dedicada via DXGI `IDXGIAdapter1.GetDesc1` (registro `DedicatedVideoMemory` bytes) e nome via `Win32_VideoController` (driver, versão, data) | **[DOC]** [DXGI_ADAPTER_DESC](https://learn.microsoft.com/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc) / [DXGI_ADAPTER_DESC1](https://learn.microsoft.com/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1). **[DECISAO]** mapear por LUID/nome ordenados quando possível; quando só houver um adaptador da mesma família, associar por nome exato; senão, não inventar: exibir inventário DXGI separado do WMI. |
| P1-07 | Discos: `MSFT_PhysicalDisk` (root/Microsoft/Windows/Storage) com `FriendlyName`, `SerialNumber`, `MediaType` (3=HDD,4=SSD,5=SCM), `BusType` (17=NVMe etc.), `FirmwareVersion`, `Size` | **[DOC]** [MSFT_PhysicalDisk](https://learn.microsoft.com/windows-hardware/drivers/storage/msft-physicaldisk). Fallback: `Win32_DiskDrive` (`MediaType`, `SerialNumber`, `InterfaceType`, `Size`). |
| P1-08 | Memória: `Win32_PhysicalMemory` campos `Capacity`, `Speed`, `ConfiguredClockSpeed`, `MemoryType`/`SMBIOSMemoryType`, `Manufacturer`, `PartNumber`, `SerialNumber`, `DeviceLocator`; slots total de `Win32_PhysicalMemoryArray.MemoryDevices` | **[DOC]** [Win32_PhysicalMemory](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-physicalmemory). Tipos: [SMBIOSMemoryType](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-physicalmemory) → valores SMBIOS 7/17 memória tipo (DDR4=26, DDR5=34, etc.) **[CONH]/DOC** [SMBIOS Type 17](https://www.dmtf.org/standards/smbios). Timings: **[DECISAO]** mostrar "Não disponível" (sem fonte confiável com licença compatível). |
| P1-09 | Rede: `NetworkInterface.GetAllNetworkInterfaces()`; velocidade de `Speed` (bps) — em virtude do Wi-Fi o valor é o link rate negociado, não real | **[DOC]** [NetworkInterface](https://learn.microsoft.com/dotnet/api/system.net.networkinformation.networkinterface) |
| P1-10 | Tela: `EnumDisplayMonitors` + `EnumDisplaySettings` (`dmPelsWidth`, `dmPelsHeight`, `dmDisplayFrequency`) | **[DOC]** [EnumDisplaySettings](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsa) |
| P1-11 | Bateria presente/estado: `Win32_Battery` | **[DOC]** [Win32_Battery](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-battery) |
| P1-12 | Dispositivos com erro: `Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0` e `Status <> 'OK'`; o código `ConfigManagerErrorCode` corresponde aos "problem codes" do Gerenciador de Dispositivos | **[DOC]** [ConfigManagerErrorCode](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-pnpentity) e [Device Manager problem codes](https://learn.microsoft.com/windows-hardware/drivers/install/device-manager-problem-codes) |
| P1-13 | Link para página do fabricante: **não abrimos URL de suporte sem uma base de fabricantes** | **[DECISAO]** evitar tela sem ação: mostramos o código de erro em linguagem própria + instrução (atualizar driver, ver Gerenciador). Nenhuma instalação. Ver D2 em DECISOES. |
| P1-14 | WMI com query e timeout próprio (`ManagementObjectSearcher`) rodando em thread separada; exceções por coletor viram `Leitura.Ausente(Erro, ...)` | **[DECISAO]** robustez: uma classe WMI corrompida não derruba a tela. |
| P1-15 | Fixtures WMI em JSON de exemplo em `tests/fixtures/wmi/*.json` | **[DECISAO]** para testar o parser sem depender da máquina. Valores de exemplo **não** são dados reais da máquina e nunca vão para a interface (somente fakes/testes). |
| P1-16 | Laudo JSON versionado `flavos-diag/laudo/1` | **[DECISAO]** esquema próprio, documentado em `SCHEMA-LAUDO.md`. |
| P1-17 | Diagnóstico rápido: orquestração sequencial com timeout por etapa e cancelamento | **[DECISAO]** ordem da seção E11. Sensores coletados por 60 s. Sempre permite cancelar. |

## Fase 2 — disco, bateria e rede

| # | Decisão | Origem |
|---|---|---|
| P2-01 | Inventário de disco também via `MSFT_Disk`/`MSFT_PhysicalDisk` | **[DOC]** [MSFT_Disk](https://learn.microsoft.com/windows-hardware/drivers/storage/msft-disk) |
| P2-02 | SMART ATA: `IOCTL_ATA_PASS_THROUGH` (ou `SMART_RCV_DRIVE_DATA`) para ler o bloco de atributos | **[DOC]** [ATA/ATAPI Command Set - SMART READ DATA (B0h/D0h, LBA 0 C2h)](https://www.t13.org/documents/UploadedDocuments/docs2020/d2128r14-ATAATAPI_Command_Set_-_5.pdf) / [NVMe Base Spec 2.0](https://nvmexpress.org/specifications/). **[DECISAO]** Implementação própria usando `DeviceIoControl` e `ATA_PASS_THROUGH_EX`. |
| P2-03 | NVMe Health Log: `IOCTL_STORAGE_QUERY_PROPERTY` com `STORAGE_PROTOCOL_SPECIFIC_DATA` (Protocol = NVMe, DataType = Health/Microsoft) | **[DOC]** [MS docs](https://learn.microsoft.com/windows/win32/api/winioctl/ns-winioctl-storage_protocol_specific_data), campo `NVMeHealthLog` (estrutura NVMe Log Page 02h). *Se as APIs falharem* usamos o fallback. |
| P2-04 | Fallback: `Get-PhysicalDisk`/`StorageReliabilityCounter` | **[DOC]** [Microsoft docs](https://learn.microsoft.com/powershell/module/storage/get-storagereliabilitycounter) |
| P2-05 | Smartctl: **não empacotado nesta versão se for necessário o GPL + processo externo** — decisão: só empacotar se a implementação nativa falhar e o esforço compensar. Se empacotar → `-j` JSON no smartctl 7.4, texto GPL completo; chamado por `ProcessStartInfo` | **[DOC]** [smartmontools license](https://www.smartmontools.org/browser/trunk/smartmontools/COPYING) |
| P2-06 | Instrumentação da superfície: leitura sequencial via `FileStream` com `FileShare.Read`, blocos de 4 MiB, tempo por bloco com `Stopwatch` | **[DECISAO]** somente leitura; sem `DeviceIoControl` direto em setores (proibido pelo requisito C). |
| P2-07 | Verificação de sistema de arquivos: `chkdsk <volume> /scan` (somente leitura, online); capturar saída e código de retorno; sem `/f` ou `/r` | **[DOC]** [chkdsk](https://learn.microsoft.com/windows-server/administration/windows-commands/chkdsk) — `/scan` é somente leitura. |
| P2-08 | Benchmark de disco: arquivo temporário próprio; verificação de espaço; sequencial 128 KiB/1 MiB, 4 KiB aleatório; `FileOptions.WriteThrough` opcional; apagar no final | **[DECISAO]** medir apenas o disco onde está o arquivo; temp em `%TEMP%` por padrão, alternativa: pasta escolhida. |
| P2-09 | Bateria via `powercfg /batteryreport /output %TEMP%\...xml` e `Win32_Battery` | **[DOC]** [powercfg battery report](https://learn.microsoft.com/windows-hardware/design/component-guidelines/powercfg-command-line-options). **[DECISAO]** preferir o XML (capacidades e ciclos) e usar `Win32_Battery` apenas para estado. |
| P2-10 | Rede: ICMP via `IcmpSendEcho` (P/Invoke `icmp.dll`) com 20 amostras; fallback TCP `Socket.ConnectAsync` ao port 443/80 configurado | **[DOC]** [IcmpSendEcho](https://learn.microsoft.com/windows/win32/api/icmpapi/nf-icmpapi-icmpsendecho). **[DECISAO]** N=20, timeout 1000 ms por pacote. |
| P2-11 | DNS: `Dns.GetHostAddressesAsync` medindo latência, nome de alvo configurável; padrão do sistema (nenhum domínio embutido) | **[DOC]** |
| P2-12 | Wi-Fi: `WlanQueryInterface` (`wlan_intf_opcode_current_connection`) → `WLAN_ASSOCIATION_ATTRIBUTES.wlanSignalQuality` (0–100) | **[DOC]** [WLAN_ASSOCIATION_ATTRIBUTES](https://learn.microsoft.com/windows/win32/api/wlanapi/ns-wlanapi-wlan_association_attributes) |
| P2-13 | Teste de velocidade: só contra URL configurada pelo usuário; `HttpClient` com `Stopwatch`; sem servidores de terceiros embutidos | **[DOC]** [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient). **[DECISAO]** desabilitado por padrão. |
| P2-14 | Veredito de disco: ATA atributos 5/197/198/187/199/231, 9/12/194/190; NVMe Critical Warning/Media Errors | **[DOC]** [SMART attribute registry (T13/ATA-8)](https://www.t13.org/) |

## Fase 3 — sensores, estresse, benchmark de CPU e memória

| # | Decisão | Origem |
|---|---|---|
| P3-01 | Sensores via `LibreHardwareMonitorLib` (`Computer`, `IHardware`, `ISensor`) | **[DOC]** [LibreHardwareMonitorLib API no repositório](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Uso como dependência binária (MPL-2.0). |
| P3-02 | Sem direito de administrador, sensores de temperatura ausentes → Inconclusivo com causa "requer elevação/driver" e **nunca** OK | Requisito C/B. |
| P3-03 | Mapa de sensores: Temperature, Clock, Load, Power, Voltage, Fan → categorias próprias; descartar leituras não finitas/absurdas (temperatura < -40 °C ou > 150 °C) | **[DECISAO]** evitar atributo corrompido aparecer como alerta. |
| P3-04 | Cargas de CPU: matriz SIMD (`Vector<float>`), crivo de primos, hash inteiro (FNV-1a + xorshift), mistura FP (polinômio de Horner); checksum verificável | **[DOC]** [Vector<T>](https://learn.microsoft.com/dotnet/api/system.numerics.vector-1). **[DECISAO]** semente e tamanhos fixos; resultados comparados com checksum calculado na inicialização. |
| P3-05 | Perfis: leve = # físicos, médio = # lógicos, pesado = # lógicos com fatia maior | **[DECISAO]** |
| P3-06 | Parada por temperatura: checar sensor CPU (Tctl/Tdie) ou GPU, com histerese/alvo; parada por duração; sem sensor → 2 min | Requisito C. **[DECISAO]** avalia a cada 1 s; parada quando `>= limite` por 2 amostras. |
| P3-07 | Throttling: comparar clock medianа na segunda metade vs primeira metade; queda ≥ 10 % = throttling | **[DECISAO]** heurística documentada; limitação registrada no laudo. |
| P3-08 | Benchmark CPU: 5 amostras de 3 s por modo, mediana; pontuação = unidades de trabalho por segundo normalizadas (`pontos = trabalho/1000`) | **[DECISAO]** próprio, não comparável. |
| P3-09 | **Teste de memória rápido interno** (`TesteMemoriaRunner`): cinco padrões — `0x00`, `0xFF`, `0x55/0xAA`, `índice × constante` (64 bits, `unchecked`) e o **complemento** desse último — aplicados a 100 % do buffer, conferidos palavra a palavra com `Span`/`MemoryMarshal`; blocos de 4 MiB com cancelamento a cada bloco. | **[DECISAO]** própria. São técnicas genéricas de teste de memória descritas em material público de eletrônica/verificação: padrões constantes expõem célula presa, padrões alternados expõem acoplamento entre bits, padrão dependente de endereço expõe troca de bloco/alias de linha de cache, e o complemento expõe falha de retenção na transição 0→1. Nenhum código de terceiros foi lido ou reutilizado. |
| P3-09a | Tamanho do teste de memória: **50 % da memória física livre** lida por `GlobalMemoryStatusEx` (`ullAvailPhys`), com teto de 2 GiB e piso de 256 MiB | **[DOC]** [GlobalMemoryStatusEx](https://learn.microsoft.com/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex). **[DECISAO]** a fração e o teto são escolha própria, para não provocar paginação e não piorar a máquina do cliente. |
| P3-09b | Veredito da memória: sem erro → **Inconclusivo**; uma divergência → **Atenção** (repetir); divergências em padrões distintos → **Falha**; nada medido → **Inconclusivo** com causa | Requisito B (dado ausente nunca vira OK) e `diag-verdicts-and-reports` (teste rápido não comprova integridade). |
| P3-10 | Diagnóstico de memória nativo: `mdsched.exe /c`? — usar `Start-Process mdsched.exe` apenas com confirmação e **somente** se o usuário aceitar o reinício ao chamar a tela; a tela não agenda por padrão. | **[DOC]** [mdsched](https://learn.microsoft.com/windows-server/administration/windows-commands/mdsched) |

## Fase 4 — GPU

| # | Decisão | Origem |
|---|---|---|
| P4-01 | D3D11 + swap chain offscreen; shader HLSL compilado em tempo de build com `fxc`/DXC | **[DOC]** [D3D11 intro](https://learn.microsoft.com/windows/win32/direct3d11/atoc-dx-graphics-direct3d-11), [HLSL compiler](https://learn.microsoft.com/windows/win32/direct3dtools/dx-graphics-hlsl) |
| P4-02 | Inventário GPU via DXGI (adapter, VRAM, driver) e WMI | **[DOC]** |
| P4-03 | Detecção de artefatos: hash de pixels de uma região determinística comparado a uma referência calculada no app | **[DECISAO]** própria. |
| P4-04 | Se D3D11 não iniciar → categoria GPU = Inconclusivo com causa | Requisito. |

## Fase 5 — laudo, checklist, configurações e entrega

| # | Decisão | Origem |
|---|---|---|
| P5-01 | PDF com QuestPDF (Community) | **[DOC]** [QuestPDF docs](https://www.questpdf.com/) |
| P5-02 | JSON com `System.Text.Json` (nativo) | **[DOC]** |
| P5-03 | Pacote "laudo da OS": PDF + JSON + CSV na pasta escolhida | Briefing |
| P5-04 | Fonte empacotada: Geist se OFL; senão Inter (OFL) | **[DOC]** [OFL 1.1](https://openfontlicense.org/) |
| P5-05 | Configurações em `%LOCALAPPDATA%\FlavosDiag\config.json` | **[DECISAO]** |
| P5-06 | Histórico em `%LOCALAPPDATA%\FlavosDiag\historico.json` | **[DECISAO]** |
| P5-07 | Redução de movimento: `SystemParameters.ClientAreaAnimation` | **[DOC]** [SystemParameters](https://learn.microsoft.com/dotnet/api/system.windows.systemparameters) |
