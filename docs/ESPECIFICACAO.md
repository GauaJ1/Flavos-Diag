# Flavos Diag — Especificação funcional e técnica

> Versão do documento: 1.0 (Fase 0)
> Este documento é escrito **antes** da implementação. Todo código do Flavos Diag deve derivar dele.
> Fontes permitidas: documentação oficial da Microsoft, padrões/specs públicas (DMTF/SMBIOS, ACPI, ATA, NVMe, PCI, EDID, USB), documentação das bibliotecas usadas e conhecimento técnico geral público.
> Este documento cita apenas **categorias de função**. Nenhum produto de terceiros é nomeado.

Sumário
1. Escopo e princípios
2. Arquitetura
3. Contratos de dados (Core)
4. Providers (interfaces e implementações)
5. Recursos E1–E12
6. Regras de veredito
7. UI e acessibilidade
8. Distribuição
9. Matriz de dependências e licenças (resumo)
10. Procedência resumida (detalhe em PROCEDENCIA.md)

---

## 1. Escopo e princípios

O Flavos Diag é uma ferramenta de **bancada** para diagnóstico rápido de computadores Windows, uso por técnico de informática (MEI). Ele **lê** o aparelho, **mede** desempenho com cargas originais e **conclui** com veredito por categoria. Ele não altera o sistema.

Princípios inegociáveis:

| # | Princípio | Como é garantido |
|---|---|---|
| P1 | Somente leitura por padrão | Nenhum código escreve em registro, serviços, drivers ou arquivos do usuário. Os únicos arquivos escritos são: laudos/configuração na pasta do usuário (`%LOCALAPPDATA%\FlavosDiag`), arquivos temporários do teste de disco e CSV/pacote exportados a pedido do técnico. |
| P2 | Dado ausente ≠ OK | Todo valor numérico é `Leitura<T>` com `Estado` (`Disponivel`, `NaoSuportado`, `SemPermissao`, `Erro`) e causa. Veredito nunca transforma ausência em OK. |
| P3 | Nenhuma "correção" automática | O app detecta, registra e recomenda. Não existe reparo de setor, otimização, limpeza ou instalação de driver. |
| P4 | Segurança do equipamento | Estresse: confirmação, limite de temperatura com parada, botão de parada, duração máxima; sem sensor, teto de 2 min. |
| P5 | Sem rede para fora | Nenhuma telemetria. Requests de rede apenas para os alvos configurados pelo técnico (gateway, DNS, servidor de teste). |
| P6 | Privacidade | Números de série são mascarados por padrão no laudo exportado, com opção explícita de exportar completo. |
| P7 | Sem falsificação | Nenhum número é inventado. Onde não há leitura real, a interface mostra "sem leitura" e o motivo. |
| P8 | Originalidade | Cargas, cena de GPU, pontuação, textos e identidade visual são próprios. Não há cópia de código, nome, ícone, texto ou layout de terceiros. |

## 2. Arquitetura

Solução `FlavosDiag.sln`, .NET 8 (LTS), C#, WPF, MVVM (CommunityToolkit.Mvvm).

| Projeto | Tipo | Responsabilidade | Dependência de Windows |
|---|---|---|---|
| `FlavosDiag.Core` | classlib `net8.0` | Modelos, `Leitura<T>`, motores de pontuação, regras de veredito, esquema do laudo, mascaramento, formatação pt-BR | nenhuma |
| `FlavosDiag.Providers` | classlib `net8.0-windows` | Coletores reais e fakes atrás de interfaces | sim |
| `FlavosDiag.Workloads` | classlib `net8.0` | Cargas de CPU originais (estresse/benchmark) com verificação de resultado | nenhuma (testável no CI) |
| `FlavosDiag.Reports` | classlib `net8.0` | PDF (QuestPDF) e JSON (System.Text.Json) | não |
| `FlavosDiag.App` | `net8.0-windows` WPF, `WinExe` | Interface | sim |
| `FlavosDiag.Tests` | xUnit | Testes com fixtures/fakes | sim (alguns testes somente Windows) |

Fluxo: App (ViewModels) → Providers (interfaces) → dados → Core (regras) → Reports (laudo). O App nunca chama API do Windows diretamente; tudo passa por interfaces em `FlavosDiag.Providers`.

Execução: cada tarefa de tela é assíncrona (`Task.Run`), com timeout por coletor, cancelamento em cadeia e resultados parciais. Uma falha de um coletor nunca derruba a tela; vira item "Inconclusivo" com causa.

## 3. Contratos de dados (Core)

### 3.1 `Leitura<T>`

```
Leitura<T>(
  EstadoLeitura Estado,     // Disponivel | NaoSuportado | SemPermissao | Erro | NaoLido
  T? Valor,
  string? Causa,            // texto pt-BR explicando ausência
  string? Fonte)            // origem técnica (ex.: "WMI Win32_Processor")
```

Regras:
- `Estado == Disponivel` ⟺ `Valor` preenchido.
- `Fonte` é texto técnico para auditoria (aparece em "Limitações" do laudo).
- Conversão implícita de `T` para disponível e do estado para ausente (helper `Ausente`).
- `NaoLido` = ainda não coletado (proibido em laudo).

### 3.2 Inventário (`InventarioSistema`)

| Campo | Tipo | Fonte |
|---|---|---|
| Sistema operacional: nome, versão, build, arquitetura, nome do PC, usuário, instalação, ativação informativa | string/Leitura | Registro (leitura), `Environment`, `GetVersionEx` |
| BIOS/UEFI: fabricante, versão, data | Leitura | `Win32_BIOS` |
| Placa-mãe: fabricante, modelo, versão, número de série | Leitura | `Win32_BaseBoard` |
| CPU: nome, fabricante, núcleos, threads, clock base, clock máx, cache L2/L3, arquitetura | Leitura | `Win32_Processor`, `GetLogicalProcessorInformationEx` |
| GPU: nome, driver, versão, data do driver, VRAM dedicada | Leitura | `Win32_VideoController`, DXGI (`IDXGIAdapter`) |
| Disco: modelo, firmware, barramento, mídia (SSD/HDD), tamanho, serial | Leitura | `MSFT_PhysicalDisk` (Storage WMI), `Win32_DiskDrive` |
| Memória: total, slots, por módulo (capacidade, velocidade, tipo, fabricante, part number, serial) | Leitura | `Win32_PhysicalMemory` + SMBIOS (`Win32_PhysicalMemoryArray`, tipos) |
| Rede: adaptadores (nome, descrição, MAC, velocidade, tipo), Wi-Fi força do sinal | Leitura | `Win32_NetworkAdapter`, `WlanAPI` |
| Tela: resolução e taxa de atualização por monitor | Leitura | EnumDisplayDevices/EnumDisplaySettings |
| Bateria: presente, nome, fabricante | Leitura | `Win32_Battery`, `BatteryStaticData` |
| Dispositivos com problema: nome, código de erro, fabricante | lista | `Win32_PnPEntity` (`ConfigManagerErrorCode != 0`) |

Memória: timings (CAS/RAS/CL) somente se houver fonte confiável com licença compatível (SMBIOS Type 17 estendido). Sem fonte confiável: exibir **"Não disponível"** com causa.

Limites de fabricante: fabricante/part number/serial de módulo só existem se o SPD/driver SMBIOS expõe (JEDEC SPD Byte 320+ / DDR4+). Ausência → Inconclusivo com causa.

### 3.3 Disco

`DiscoInfo` (identificação + `SaudeDisco`), `AtributoSmart` (id, nome legível, valor, pior, limite, bruto, interpretação), `SaudeNvme` (porcentagem usada, dados escritos, horas, ciclos, temperatura, erros de mídia, avisos críticos), `VereditoDisco`, `ResultadoBenchmarkDisco` (sequencial leitura/escrita MB/s, IOPS 4K aleatório, latência p95), `ResultadoVarreduraSuperficie` (blocos lidos, lentos, erro, mapa por bloco, tempo), `ResultadoVerificacaoSistemaArquivos`.

### 3.4 Sensores

`AmostraSensores(Timestamp, lista de ValoresSensor)`; `ValorSensor(Id, Categoria, Rotulo, Valor, Unidade, Minimo, Maximo)`; categorias: Temperatura, Clock, Carga, Potência, Tensão, Ventoinha, -Outros. `SerieSensores` (buffer circular com janela 60 s–10 min), `ResumoSensor` (mín, máx, média, amostras), alertas por limite.

### 3.5 Testes de desempenho (CPU/GPU)

`PerfilCarga` (Leve/Médio/Pesado: número de workers, tamanho de trabalho), `ConfiguracaoEstresse` (perfil, duração, limite °C), `ResultadoEstresse` (tempo, ciclos, erros de cálculo, temperatura máx/média, clock sustentado, throttling detectado, motivo de parada, amostras de sensor), `ResultadoBenchmarkCpu` (pontuação single, multi, amostras, versão do algoritmo, pontuação própria), `TesteMemoriaRapido` (padrões, MB testados, erros, duração).

### 3.6 Laudo

`Laudo` (versão do esquema `flavos-diag/laudo/1`, dados do técnico/MEI, OS, data/hora ISO 8601, equipamento, resumo, veredito geral, lista de `ResultadoCategoria`, evidências, `Limitacao[]`, `Recomendacao[]`, checklist, assinatura). Esquema completo em `docs/SCHEMA-LAUDO.md`.

## 4. Providers

Todas as interfaces ficam em `FlavosDiag.Providers.Abstractions`. Cada interface tem uma implementação real e uma fake (`Fake*`) configurável para testes.

| Interface | Real | Fake |
|---|---|---|
| `IInventarioProvider` | WMI/CIM + DXGI + APIs Win32 | Fake com fixtures JSON de WMI |
| `ISensorProvider` | biblioteca de monitoramento open source (LHM) via `Computer`; sem driver → reduzido | `FakeSensorProvider` com séries programáveis |
| `IDiscoProvider` | Storage WMI + `IOCTL_STORAGE_*`/`DeviceIoControl` (SMART ATA, log de saúde NVMe) + `Get-PhysicalDisk`/`StorageReliabilityCounter`; opcional smartctl | Fake com fixtures |
| `IBateriaProvider` | `powercfg /batteryreport` → XML + `Win32_Battery` | Fake com XML de exemplo |
| `IRedeProvider` | `NetworkInterface`, UDP/TCP direto para alvos configurados, `WlanAPI` | Fake |
| `IGpuProvider` | DXGI (inventário); Direct3D 11 (estresse/benchmark) | Fake |
| `IElevacaoService` | `ShellExecute` `runas` quando o usuário ativa recurso que exige admin | Fake |
| `ICargaCpu` | `FlavosDiag.Workloads` | Fake |
| `ICargaGpu` | D3D11 compute/render | Fake |

Modo reduzido (sem administrador):
- Inventário: quase completo (WMI não exige admin).
- Sensores de temperatura: dependem de driver do monitor de hardware; sem instalação/admin, ficam **Inconclusivo: requer elevação**.
- SMART: ATA SMART via DeviceIoControl costuma funcionar sem admin para leitura; NVMe log de saúde pode exigir admin. Sem dados → fallback `StorageReliabilityCounter`; se também ausente → Inconclusivo.
- O app **nunca** instala driver. A elevação é apenas de processo (UAC) e é pedida por ação, com explicação.

## 5. Recursos (mapa E)

### E1 — Inventário de hardware (informações do sistema)
Sistema operacional, BIOS/UEFI, placa-mãe, CPU, memória, GPU, discos, rede, tela, bateria, dispositivos com erro. Cada bloco mostra `Leitura` e a fonte. Fluxo em `InventarioProvider.ColetarAsync` com timeout total de 90 s e por coletor 20 s.
Fontes: [Win32_Processor](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-processor), [Win32_PhysicalMemory](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-physicalmemory), [MSFT_PhysicalDisk](https://learn.microsoft.com/windows-hardware/drivers/storage/msft-physicaldisk), [DXGI IDXGIAdapter](https://learn.microsoft.com/windows/win32/api/dxgi/nn-dxgi-idxgiadapter), [DXGI_ADAPTER_DESC](https://learn.microsoft.com/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc), [Win32_PnPEntity](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-pnpentity), [Device Manager error codes](https://learn.microsoft.com/windows-hardware/drivers/install/device-manager-problem-codes), [SMBIOS 3.x DMTF](https://www.dmtf.org/standards/smbios), [Registro de instalação/ativação — leitura informativa](https://learn.microsoft.com/windows/win32/api/sysinfoapi/).

### E2 — Sensores em tempo real (monitoramento)
Interface `ISensorProvider` + biblioteca LHM (MPL-2.0). Coleta amostras a cada 1 s (intervalo padrão), janela de gráfico 60 s a 10 min, gráficos de linha próprios (desenho WPF com `Polyline`), tabela alternativa (acessibilidade), mín/máx/média, exportação CSV, alertas por limite (visuais + registro). Sem leitura → "sem leitura" com causa.
Fontes: documentação da biblioteca [LibreHardwareMonitor / LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (API `Computer`, `IHardware`, `ISensor`); [MSAcpi_ThermalZoneTemperature](https://learn.microsoft.com/windows/win32/cimwin32prov/msacpi-thermalzonetemperature) como fallback sem driver.

### E3 — Saúde do disco (diagnóstico de armazenamento)
- SMART ATA: `SMART_RCV_DRIVE_DATA`/`IOCTL_ATA_PASS_THROUGH` via `DeviceIoControl` (offset 0,423 do registro do tipo regular). Atributos interpretados: 5 setores realocados, 197/198 pendentes e não corrigíveis, 9 horas, 12 ciclos, 194/190 temperatura, 187 erros não corrigíveis, 231 vida SSD/atitude.
- NVMe: `IOCTL_STORAGE_QUERY_PROPERTY` `StorageAdapterProtocolSpecificProperty` (NVMe Health Log). Campos: `Percentage Used`, `Data Units Written`, `Power On Hours`, `Power Cycles`, `Temperature`, `Media and Data Integrity Errors`, `Critical Warning`.
- Fallback Windows: `Get-PhysicalDisk` + `StorageReliabilityCounter` (`ReadErrorsUncorrected`, `Wear`, `Temperature`, `PowerOnHours`, `StartStopCycleCount`).
- Opcional: utilitário de código aberto `smartctl` (`-j`/JSON) empacotado em `tools/`, chamado como processo separado; texto de licença incluído. Se ausente, o app segue com as APIs do Windows.
- Benchmark de disco (próprio): arquivo em `%TEMP%` ou pasta indicada, verificação de espaço livre (≥ 512 MB + 2× tamanho do arquivo), escrita sequencial (1 MiB blocks), leitura sequencial, CPU-limited?, teste 4K aleatório, fsync; apaga no final (inclusive em cancelamento/erro).
- Varredura de superfície: **somente leitura** (`FileStream` com `FileAccess.Read`, shared read, `FILE_FLAG_NO_BUFFERING` opcional não; blocos 1 MiB; tempo por bloco). Aviso obrigatório: "Faça backup antes. Discos com defeito podem piorar durante a leitura." Mapa de blocos (verde/amarelo/vermelho), cancelável, resultado registrado.
- Verificação de sistema de arquivos: `chkdsk /scan` em modo somente leitura (varredura online) para volumes NTFS; para outros FS, `chkdsk /r` não é executado; resultado registrado (código de saída + saída). Nunca `/f` nem `/r`.
- Veredito: SSD com erro de mídia → "recomendar troca"; HDD com setores realocados/pendentes crescente → backup imediato + troca. Nunca "consertar".

Fontes: [ATA/ATAPI Command Set (INCITS T13)](https://www.t13.org/), [NVMe 1.4/2.0 spec (NVM Express)](https://nvmexpress.org/), [IOCTL_STORAGE_QUERY_PROPERTY](https://learn.microsoft.com/windows/win32/api/winioctl/ni-winioctl-ioctl_storage_query_property), [STORAGE_PROTOCOL_SPECIFIC_DATA](https://learn.microsoft.com/windows/win32/api/winioctl/ns-winioctl-storage_protocol_specific_data), [STORAGE_PROPERTY_ID](https://learn.microsoft.com/windows/win32/api/winioctl/ne-winioctl-storage_property_id), [Get-PhysicalDisk](https://learn.microsoft.com/powershell/module/storage/get-physicaldisk), [StorageReliabilityCounter](https://learn.microsoft.com/powershell/module/storage/get-storagereliabilitycounter), [chkdsk](https://learn.microsoft.com/windows-server/administration/windows-commands/chkdsk), [IOCTL_ATA_PASS_THROUGH](https://learn.microsoft.com/windows-hardware/drivers/ddi/ntddscsi/ni-ntddscsi-ioctl_ata_pass_through).

### E4 — Memória
- Inventário (E1).
- Diagnóstico nativo do Windows: `mdsched.exe` (Windows Memory Diagnostic) com confirmação (exige reinício). Só re-agenda com confirmação explícita.
- **Teste rápido de padrões in-process** (implementado em `TesteMemoriaRunner`). Técnicas genéricas de teste de memória, escolhidas por decisão própria do projeto (ver `PROCEDENCIA.md`):

  | Padrão | O que detecta |
  |---|---|
  | `0x00` — todos os bits em zero | Célula presa em 1; curto entre linha e alimentação. |
  | `0xFF` — todos os bits em um | Célula presa em 0; trilha aberta. |
  | `0x55`/`0xAA` — bits alternados | Acoplamento entre bits vizinhos (um bit "puxa" o outro). |
  | `índice × constante` (palavra de 64 bits, `unchecked`) | Cada endereço tem valor único: detecta troca de blocos, linha de cache repetida e endereço que "espelha" outro. |
  | complemento do padrão anterior | Escrito em seguida ao original: detecta célula que não aceita a transição 0→1 ou 1→0 (falha de retenção). |

- Tamanho: padrão = 50 % da memória física **livre** (`GlobalMemoryStatusEx`/`ullAvailPhys`), com teto de 2 GiB e piso de 256 MiB. Nunca usa "memória total disponível" como proxy de memória livre.
- Processamento em blocos de 4 MiB, com cancelamento verificado a cada bloco (`Span`/`MemoryMarshal`, sem LINQ nos laços de conferência). O botão "Parar" responde em menos de 1 segundo.
- Resultado registra: bytes efetivamente testados (0 se nada rodou), padrões concluídos, número de palavras divergentes, primeiro deslocamento com erro, cobertura (bytes testados / RAM total), memória livre, duração e motivo.
- **Veredito** (regra explícita, em `REGRAS-VEREDITO.md`):
  - Sem erro → **Inconclusivo**. Um teste rápido não prova integridade; nunca vira OK.
  - Uma divergência → **Atenção**, com instrução de repetir o teste.
  - Divergências em padrões distintos (duas execuções seguidas) → **Falha**, com recomendação de trocar o módulo.
  - Nada medido (memória livre ilegível, falta de memória, cancelado antes de concluir um padrão) → **Inconclusivo** com a causa.
- Aviso explícito na interface e no laudo: este teste **não substitui** o teste de boot completo do Windows.
- Checklist para memória externa por pendrive (K10) registrada manualmente.

Fontes: [Windows Memory Diagnostic (mdsched)](https://learn.microsoft.com/windows-server/administration/windows-commands/mdsched), [Windows Memory Diagnostic](https://learn.microsoft.com/windows-hardware/drivers/devtest/windows-memory-diagnostics).

### E5 — Teste de estresse de CPU (teste de estabilidade)
Cargas originais determinísticas: multiplicação de matrizes com `Vector<T>` (SIMD), crivo de primos segmentado, hash (implementação própria tipo FNV-1a/mix inteiro) e mistura de inteiros e ponto flutuante (polinômio). Cada carga calcula um resultado esperado (checksum) em tempo de setup e verifica a cada bloco → erros de cálculo.
Perfis: Leve (1 worker por núcleo físico, trabalho pequeno), Médio (1 worker por thread lógica), Pesado (mesma contagem, trabalho maior, duração maior).
Segurança: confirmação modal; limite de temperatura (padrão CPU 90 °C, GPU 85 °C, configurável); botão "Parar" sempre visível; duração obrigatória (1–60 min); sem leitura de temperatura → limitar a 2 min e avisar; registrar motivo da parada (`Tempo`, `Temperatura`, `Cancelado`, `Erro`, `SemSensorLimite`).
Resultado: temp máx, temp média, clock sustentado (mediana da segunda metade), throttling (queda ≥ 10 % do clock entre a primeira e a segunda metade sob carga e mantida ≥ 5 s), erros de cálculo, amostras.

Fontes: [Vector<T> (SIMD)](https://learn.microsoft.com/dotnet/api/system.numerics.vector-1), [Parallel.For](https://learn.microsoft.com/dotnet/api/system.threading.tasks.parallel.for), [Timer Resolution / Stopwatch](https://learn.microsoft.com/dotnet/api/system.diagnostics.stopwatch).

### E6 — Benchmark de CPU (benchmark de processador)
Cargas originais determinísticas (mesmas de E5, em tamanho fixo por versão), 5 repetições de 3 s cada, pontuação por amostra, **mediana**. Single-thread (1 worker) e multi-thread (threads lógicas). Pontuação 0–∞ derivada de unidades de trabalho por segundo normalizadas (`pontos = trabalho / 1000`), fixa por `VersãoAlgoritmo=1`. Texto obrigatório na tela e no laudo: "Pontuação própria do Flavos Diag; não comparável com outros programas". Histórico por máquina (`%LOCALAPPDATA%\FlavosDiag\historico.json`) com comparação antes/depois (delta % e classificação).

### E7 — GPU (estresse e benchmark de placa de vídeo)
- Inventário e sensores: DXGI + sensores de temperatura/carga/clock quando expostos pelo hardware.
- Cena própria: D3D11 + DXC/HLSL (shader próprio) renderizando fractal (Mandelbrot) com iluminação simples, em swap chain off-screen (sem janela cheia; render para textura) e carga ajustável (iterações, resolução, contagem de draw). Implementada com `Microsoft.Windows.CsWin32` para P/Invoke (licença MIT) ou P/Invoke manual — decisão em DECISOES.md.
- Estresse: mesmas travas de E5 (duração, temperatura GPU padrão 85 °C, botão parar, sem sensor → 2 min).
- Detecção de artefatos: leitura de um bloco de pixels conhecido deterministicamente e comparação; discrepância → contagem de artefatos. Plus verificação de `GetDeviceRemovedReason` (TDR/device removed) → classifica como falha de driver/hardware.
- Benchmark GPU: passada determinística (N frames), pontuação própria = frames efetivos/s × ruído; mediana de 3 amostras.
- Se não for viável em qualidade, entrega apenas inventário+sensores e marca BLOQUEADO.

Fontes: [DXGI overview](https://learn.microsoft.com/windows/win32/direct3ddxgi/d3d10-graphics-programming-guide-dxgi), [D3D11](https://learn.microsoft.com/windows/win32/direct3d11/atoc-dx-graphics-direct3d-11), [GetDeviceRemovedReason](https://learn.microsoft.com/windows/win32/api/d3d11/nf-d3d11-id3d11device-getdeviceremovedreason), [HLSL](https://learn.microsoft.com/windows/win32/direct3dhlsl/dx-graphics-hlsl), [fxc/HLSL Compiler](https://learn.microsoft.com/windows/win32/direct3dtools/dx-graphics-hlsl-compiler).

### E8 — Bateria
`powercfg /batteryreport /output <xml>` (formato XML definido pela Microsoft) → capacidade de projeto, capacidade total (última carga completa), ciclos (quando houver campo), design maximizado; `Win32_Battery` para estado. Saúde = capacidade atual / projeto × 100. Veredito: ≥ 80 OK, 60–79 Atenção, < 60 Falha; sem dados → Inconclusivo. Arquivo XML gerado em `%TEMP%` e apagado após parse (ou mantido apenas se o técnico pedir).
Fontes: [powercfg batteryreport](https://learn.microsoft.com/windows-hardware/design/component-guidelines/powercfg-command-line-options) e [docs powercfg](https://learn.microsoft.com/windows-hardware/design/component-guidelines/battery-report), [XML de relatório de bateria (Windows Battery Report schemas)](https://learn.microsoft.com/windows-hardware/design/component-guidelines/), [Win32_Battery](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-battery).

### E9 — Rede
Adaptador (nome, descrição, MAC, velocidade, tipo, IP, gateway, DNS). Latência e perda: N=20 pacotes ICMP (`IcmpSendEcho` via P/Invoke) para gateway (auto) e alvos configuráveis; fallback TCP handshake quando ICMP bloqueado. DNS: resolução de nome de alvo configurável via `Dns.GetHostAddresses` com cronometragem. Wi-Fi: `WlanGetAvailableNetworkList`/`WlanQueryInterface` (sinal dBm). Teste de velocidade: **somente** contra servidor configurado pelo técnico (URL), HTTP GET/POST de N MB com medição de throughput, explicando que sem servidor configurado o teste não roda.
Fontes: [NetworkInterface](https://learn.microsoft.com/dotnet/api/system.net.networkinformation.networkinterface), [IcmpSendEcho](https://learn.microsoft.com/windows/win32/api/icmpapi/nf-icmpapi-icmpsendecho), [WLAN API](https://learn.microsoft.com/windows/win32/api/wlanapi/), [WLAN_ASSOCIATION_ATTRIBUTES](https://learn.microsoft.com/windows/win32/api/wlanapi/ns-wlanapi-wlan_association_attributes), [Dns.GetHostAddresses](https://learn.microsoft.com/dotnet/api/system.net.dns.gethostaddresses), [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient).

### E10 — Checklist assistido
Itens fixos originais: teste de boot por pendrive, teste de memória externo, teclado, touchpad, portas USB, áudio, câmera, tela (pixels mortos), carregador, cooler (e memória externa E4). Estados: OK / Falha / Não se aplica / Pendente; observação livre; foto opcional (cópia para `%LOCALAPPDATA%\FlavosDiag\fotos` com hashes). Checklist entra no laudo.

### E11 — Diagnóstico rápido (botão principal)
Sequência sem estresse, alvo 2–4 min: inventário → SMART/saúde dos discos → bateria → sensores por 60 s → rede básica (gateway latência). Semáforo por categoria calculado pelas regras do Core. Se um passo estourar o tempo, é marcado Inconclusivo com a causa.

### E12 — Laudo
PDF (1–2 páginas, QuestPDF) + JSON versionado (`flavos-diag/laudo/1`). Conteúdo: dados do técnico/MEI (configuráveis), OS, data dd/mm/aaaa, equipamento, resumo, veredito geral e por categoria, evidências (números + gráficos principais de sensores/benchmark), limitações, recomendações, checklist, assinatura (linha + nome/CRC opcional). Mascaramento de seriais (padrão ligado). Pacote "laudo da OS": pasta escolhida com `laudo-OS-<n>_<data>.pdf`, `.json` e `sensores.csv`.

## 6. Regras de veredito
Detalhe completo e exemplos em `docs/REGRAS-VEREDITO.md`. Resumo:
Ordem de severidade: `Falha > Inconclusivo > Atenção > OK`. Regra especial: Falha domina; Inconclusivo nunca é reduzido para OK; a categoria geral é a **pior** categoria; se todas OK, geral OK.

## 7. UI e acessibilidade
Design descrito na seção F do briefing, implementado em DESIGN própria (paleta verde-petróleo #0F766E, fonte livre Inter empacotada, ícones de traço único desenhados com `Path` próprios, claro/escuro, sem gradientes decorativos, sem emojis). Animações 150–250 ms com respeito a `SystemParameters.ClientAreaAnimation` / `SPI_GETCLIENTAREAANIMATION`. Atalhos: F5 diagnóstico rápido, Ctrl+1..0 navegação, Esc parar teste, Alt+S salvar laudo. Foco visível, contraste AA verificado (tabela de contraste em DESIGN/README). Tabela alternativa para gráficos.

## 8. Distribuição
`build/publish.ps1` gera `dist/FlavosDiag-<versão>-win-x64/` com `FlavosDiag.App.exe` self-contained single-file (WPF) + `licenses/` (LICENSE, THIRD-PARTY-NOTICES, textos MPL-2.0 dos binários usados) + `README.txt`. Sem assinatura de código → SmartScreen e alguns antivírus podem alertar; documentado.

## 9. Resumo de licenças
Ver `docs/LICENCAS.md`. Resumo: WPF-UI 4.2.1 (MIT), CommunityToolkit.Mvvm 8.4.2 (MIT), QuestPDF 2026.x (Community License, uso comercial vedado para MEI! verificar), LibreHardwareMonitorLib 0.9.7-pre771 (MPL-2.0), System.Management/MSBuild (MIT), Inter (OFL 1.1). Cada uma verificada.

## 10. Procedência
Detalhamento de cada decisão (documento público vs. decisão própria) em `docs/PROCEDENCIA.md`.
