---
name: windows-hardware-providers
description: Padrões para coletar dados de hardware no Windows com C# no Flavos Diag - WMI/CIM (System.Management), armazenamento e SMART, relatório de bateria do powercfg, chamada segura ao smartctl, sensores por biblioteca, VRAM via DXGI, detecção de administrador e como degradar com elegância para "Inconclusivo". Use sempre que for criar ou alterar um provider, uma classe de coleta, um parser de saída (JSON/XML) ou qualquer integração com o sistema operacional ou com um binário externo, e ao escrever as versões Fake e as fixtures desses providers.
---

# Providers de hardware no Windows

Cada provider traduz uma fonte (WMI, processo externo, biblioteca) em um modelo do Core. O ponto central: **a fonte vai falhar** (sem permissão, sem o recurso, driver faltando, hardware virtual), e isso precisa chegar ao laudo como "Inconclusivo", não como um valor falso.

## Contrato
```csharp
public enum ReadStatus { Ok, Unavailable, NeedsAdmin, Error }

public sealed record ProviderResult<T>(ReadStatus Status, T? Value, string? Reason);
```
- Todo método de provider retorna `ProviderResult<T>`; nunca lança para a interface.
- `Reason` em pt-BR e acionável: "Exige executar como administrador", "Este equipamento não expõe temperatura da CPU", "Falha ao executar smartctl (código 2)".
- Cada provider tem interface (`IDiskProvider`), implementação real e `FakeDiskProvider` que carrega fixtures de `tests/fixtures/`. Os testes usam o Fake; o hardware real só entra em roteiro manual.

## WMI / CIM (System.Management)
Escopos e classes úteis (confira a documentação oficial da Microsoft de cada uma):
- Sistema/BIOS/placa: `Win32_OperatingSystem`, `Win32_BIOS`, `Win32_BaseBoard`, `Win32_ComputerSystem`.
- CPU: `Win32_Processor` (núcleos, threads, clock máximo, caches).
- Memória: `Win32_PhysicalMemory` (capacidade, velocidade, fabricante, número de peça, slot). Timings finos não vêm do WMI: exiba "não disponível".
- Vídeo: `Win32_VideoController`. Atenção: `AdapterRAM` é de 32 bits e satura em 4 GB; para VRAM dedicada use DXGI (`DXGI_ADAPTER_DESC`).
- Discos: escopo `root\Microsoft\Windows\Storage` com `MSFT_PhysicalDisk` (modelo, tipo de mídia, barramento, saúde) e `MSFT_StorageReliabilityCounter` (desgaste, temperatura, erros, horas), quando o driver expõe.
- Bateria: escopo `root\WMI` com `BatteryStaticData`, `BatteryFullChargedCapacity`, `BatteryCycleCount`. Muitos equipamentos não expõem tudo.
- Dispositivos com problema: `Win32_PnPEntity` com `ConfigManagerErrorCode != 0`.

Boas práticas:
- `using` em `ManagementObjectSearcher` e `ManagementObject`; `Timeout` configurado no `EnumerationOptions`/`ObjectQuery` e execução fora da thread da interface.
- Consulta só das propriedades necessárias (`SELECT Name, Capacity ...`), não `SELECT *`.
- Uma classe WMI falhando não derruba as demais: cada coleta em seu próprio `try/catch`, convertida em `ProviderResult`.
- Valores `null` do WMI são "ausente", não zero. Conversões numéricas com `Convert`/`TryParse` defensivo (WMI devolve `uint`, `ulong`, `string` conforme a classe).
- `System.Management` é só Windows: o projeto do provider tem `net8.0-windows`; o Core não o referencia.

## Processos externos (smartctl, powercfg, ping)
```csharp
var psi = new ProcessStartInfo(exePath)       // caminho fixo e conferido, nunca via PATH
{
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};
foreach (var a in args) psi.ArgumentList.Add(a);  // sem concatenar string de comando
```
- Timeout com `CancellationTokenSource`; ao estourar, `process.Kill(entireProcessTree: true)` e resultado `Error` com motivo.
- Limite de bytes lidos da saída; leitura assíncrona de stdout e stderr (evita travar no buffer).
- Códigos de saída: trate como máscara de bits quando a ferramenta documentar assim (o smartctl usa bits no código de saída para indicar avisos e erros; leia a documentação do `smartctl` antes de interpretar).
- **smartctl**: peça saída JSON (`--json`), desserialize com `System.Text.Json` tolerante (`JsonDocument`, campos opcionais), identifique o tipo do dispositivo (SATA/NVMe/USB) e trate adaptadores USB que não repassam SMART como "Inconclusivo: ponte USB sem suporte a SMART".
- **powercfg**: `powercfg /batteryreport /xml /output <arquivo temporário>`; leia o XML, apague o arquivo temporário, trate máquina sem bateria como "Não se aplica", não como falha.
- Comandos que exigem administrador devolvem `NeedsAdmin`; não tente contornar.

## Sensores (biblioteca de monitoramento)
- Atrás de `ISensorProvider`; o objeto da biblioteca é criado uma vez, mantido singleton e descartado ao fechar.
- Algumas leituras exigem driver de kernel e execução como administrador. Sem isso, devolva `NeedsAdmin` para as leituras afetadas e continue com o que for possível.
- Hardware virtual e alguns notebooks não expõem sensores. Mostre "sem leitura", nunca zero.
- Amostragem em intervalo fixo (ex.: 1 s) em tarefa de fundo, janela deslizante, e cancelamento limpo.

## Detecção de administrador
```csharp
using var id = WindowsIdentity.GetCurrent();
bool isAdmin = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
```
Use para decidir o que mostrar e explicar; peça elevação apenas por ação do usuário.

## Fixtures e testes
- Guarde amostras reais anonimizadas (saída JSON de SMART de SSD saudável, HDD com setores realocados, NVMe com desgaste alto, ponte USB sem SMART; XML de bateria saudável e degradada; dump de classes WMI) em `tests/fixtures/`.
- Testes de parser cobrem: arquivo válido, campos ausentes, valores fora de faixa, JSON/XML malformado e saída vazia.
- Nunca inclua números de série reais nas fixtures.

## Checklist por provider
- [ ] Interface + implementação real + Fake com fixtures.
- [ ] Retorna `ProviderResult` em todos os caminhos, inclusive exceção e cancelamento.
- [ ] Timeout, descarte e limite de saída tratados.
- [ ] Sem leitura → motivo claro; nunca valor padrão simulando leitura.
- [ ] Fonte registrada em `docs/ESPECIFICACAO.md` e dependências em `docs/LICENCAS.md`.
