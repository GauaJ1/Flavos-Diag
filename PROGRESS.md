# Flavos Diag — progresso

Registro por fase. Cada fase só é marcada como concluída com a saída de build e de teste colada aqui.

Convenções:
- **Pronto** = coleta real + tratamento de erro/permissão + teste automatizado + execução de verificação.
- Dado ausente = **Inconclusivo** com causa; nunca "OK" por omissão.
- Testes de esforço/estresse só rodam automatizados com sensores falsos.

---

## Skills do projeto

Instaladas em `.skills/` a partir do pacote enviado pelo dono (`flavos-diag-skills.zip`), versionadas junto
do projeto porque valem só para ele:

| Skill | Onde |
|---|---|
| flavos-diag-clean-room (+ `scripts/cleanroom_scan.py` e `cleanroom_allow.txt`) | `.skills/flavos-diag-clean-room/` |
| flavos-diag-safety-rules | `.skills/flavos-diag-safety-rules/` |
| csharp-wpf-standards | `.skills/csharp-wpf-standards/` |
| windows-hardware-providers | `.skills/windows-hardware-providers/` |
| diag-verdicts-and-reports | `.skills/diag-verdicts-and-reports/` |
| diag-testing-and-autonomy | `.skills/diag-testing-and-autonomy/` |
| flavos-diag-ui | `.skills/flavos-diag-ui/` |

Ajustes feitos na instalação (registrados em `docs/DECISOES.md` como D26–D28):

- pasta `.skills/` em vez de `.claude/`, porque nesta sessão as skills são lidas por caminho e a pasta
  de outro assistente pode ser gerenciada por ferramentas externas;
- verificador de clean room portado para PowerShell (`build/cleanroom.ps1`), porque Python não está
  instalado nesta máquina; a lista de termos, as pastas varridas e a lista de exceções são as mesmas;
- `cleanroom_allow.txt` recebeu o cabeçalho original (o pacote veio com a lista de exceções vazia, e o
  documento que precisa citar as proibições é `docs/RISCOS-LEGAIS.md`).

Verificação executada após a instalação:

```
PS> powershell -NoProfile -ExecutionPolicy Bypass -File .\build\cleanroom.ps1
CLEAN ROOM: nenhum achado.

PS> dotnet list FlavosDiag.sln package --vulnerable --include-transitive
O projeto fornecido `FlavosDiag.Core` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto fornecido `FlavosDiag.Workloads` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto fornecido `FlavosDiag.Reports` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto fornecido `FlavosDiag.Providers` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto fornecido `FlavosDiag.App` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto fornecido `FlavosDiag.Tests` não tem nenhum pacote vulnerável, considerando as fontes atuais.
```

Um achado real foi corrigido nessa verificação: `docs/LICENCAS.md` usava o verbo proibido pela própria
skill de clean room ao descrever o que **não** foi feito com um pacote; a frase foi reescrita.

Conflitos entre as skills e o briefing do projeto: nenhum até aqui. Se aparecer, vale a regra mais
restritiva e o conflito é registrado em `docs/DECISOES.md`.

---

## Fase 0 — Fundação
**Objetivo:** solução .NET 8 com camadas Core / Workloads / Providers / Reports / App / Tests, identidade
visual, navegação, configuração persistida, modo demonstração e a primeira suíte automatizada.

### Entregue

| Item | Situação |
|---|---|
| Solução `FlavosDiag.sln` com 6 projetos (Core, Workloads, Providers, Reports, App, Tests) | pronto |
| `Directory.Build.props` (C# 12, nullable, versão 0.5.0, empresa Flavos) | pronto |
| `FlavosDiag.Core`: `Leitura<T>`, formatação pt-BR/ISO, mascaramento, modelos, regras de veredito, agregação, pontuação, histórico, diagnóstico rápido (orquestração) | pronto |
| `FlavosDiag.Workloads`: carga de matriz SIMD, crivo de primos, hash inteiro, ponto flutuante, montador/executor com verificação de checksum | pronto |
| `FlavosDiag.Providers`: leitura WMI com fonte auditada, inventário, DXGI, acessibilidade/relatório de bateria via Windows, fakes e fixtures | pronto |
| `FlavosDiag.Reports`: laudo JSON (`flavos-diag/laudo/1`) e laudo PDF (QuestPDF) | pronto |
| `FlavosDiag.App`: janela principal com menu lateral, tema claro/escuro persistido, banner de demonstração, telas Resumo e Sistema, telas das fases seguintes com estado explícito ("sem dados") | pronto |
| Provedores ainda não implementados devolvem **estado de indisponibilidade com causa** (`Indisponiveis.cs`) — nenhuma tela mostra dado inventado | pronto |
| `--modo-demo`: dados fictícios com banner permanente e registro de `ModoDemonstracao` no laudo | pronto |
| Ícones e logotipo desenhados pelo projeto (nenhum ícone de terceiros) | pronto |
| Fonte Geist (SIL OFL 1.1) empacotada + `licenses/OFL-Geist.txt` | pronto |
| Suíte automatizada (xUnit) com runner próprio quando o host do `dotnet test` é bloqueado | pronto |

### Evidência de build

```
PS> dotnet build FlavosDiag.sln --no-restore -v m
  FlavosDiag.Core -> ...\src\FlavosDiag.Core\bin\Debug\net8.0\FlavosDiag.Core.dll
  FlavosDiag.Workloads -> ...\src\FlavosDiag.Workloads\bin\Debug\net8.0\FlavosDiag.Workloads.dll
  FlavosDiag.Reports -> ...\src\FlavosDiag.Reports\bin\Debug\net8.0-windows\FlavosDiag.Reports.dll
  FlavosDiag.Providers -> ...\src\FlavosDiag.Providers\bin\Debug\net8.0-windows\FlavosDiag.Providers.dll
  FlavosDiag.App -> ...\src\FlavosDiag.App\bin\Debug\net8.0-windows\win-x64\FlavosDiag.App.dll
  FlavosDiag.Tests -> ...\tests\FlavosDiag.Tests\bin\Debug\net8.0-windows\FlavosDiag.Tests.dll

Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)
```

### Evidência de teste

```
PS> powershell -NoProfile -ExecutionPolicy Bypass -File .\build\test.ps1
Total: 23 | Aprovados: 23 | Falhas: 0 | Ignorados: 0
Relatório: ...\artifacts\testes.trx
```

Testes da fase 0: leitura com estado/causa, formatação pt-BR e ISO 8601, mascaramento e hash de chave,
ordem de dominância dos vereditos (Falha > Inconclusivo > Atenção > OK), lista vazia → Inconclusivo,
`Não se aplica` ignorado na agregação.

### Evidência de execução do aplicativo

```
PS> (execução do .exe por 15 s, modo normal e modo demonstração, com stderr capturado)
modo --modo-demo  -> janela aberta por 15 s, sem exceção
modo normal       -> janela aberta por 15 s, sem exceção
```

Falhas encontradas nessa verificação e corrigidas: gravação de configuração sem tratamento de erro
(derruba o app em pasta somente leitura) e item de menu sem ícone (NullReferenceException ao navegar).
Ambas agora têm tratamento e teste de regressão indireto (gravação devolve motivo; menu usa ícone padrão).

### Observação de ambiente (não é bloqueio do produto)

`dotnet test` falha neste ambiente porque o host de testes precisa abrir o processo filho para receber
o sinal de fim de execução e essa operação é negada (`Win32Exception (5)` em
`Process.EnableRaisingEvents`). A suíte continua sendo xUnit: `build/test.ps1` executa o mesmo assembly
por um runner próprio, que descobre os testes `[Fact]`, executa, gera `artifacts/testes.trx` (formato
TRX do Visual Studio) e retorna código de saída não-zero em caso de falha. Em uma máquina sem essa
restrição, `dotnet test FlavosDiag.sln` funciona sem mudanças.

Status da fase: **concluída**.

---

## Fase 1 — Inventário e diagnóstico rápido

### Entregue

| Item | Situação |
|---|---|
| E1 Inventário real (WMI/CIM + DXGI) exibido na aba Sistema, com limitações explícitas | pronto — verificado nesta máquina |
| E11 Diagnóstico rápido (inventário → discos → bateria → sensores 60 s → rede, com progresso e cancelamento) | pronto (orquestração); discos/bateria/rede reais entram na Fase 2 |
| Laudo: página própria com prévia (categorias, limitações, recomendações, veredito) | pronto |
| Exportação do laudo em JSON + PDF na pasta escolhida, sem sobrescrever laudo anterior | pronto |

### O que foi corrigido nesta fase

- Arquivos de código estavam com dupla codificação UTF-8 (texto acentuado virou `Ã§` em comentários e mensagens,
  e seis caracteres se perderam de vez). Todos consertados; agora há duas ferramentas internas em `tools/`
  (`ChecagemEncoding` para verificar e `CorrigirEncoding` para consertar) e a verificação terminou com
  `Arquivos com problema: 0`. Um trecho irrecuperável foi reescrito à mão.
- O PDF do laudo passou a usar a fonte Geist empacotada: a partir do QuestPDF 2026.9 fontes do sistema não são
  usadas e o documento falhava com `'Calibri'` indisponível.
- Layout do PDF corrigido (`col.Item().Height(70).Svg(...)` gerava "conflicting size constraints"; agora o SVG
  define seu próprio tamanho).
- JSON do laudo agora sai em UTF-8 legível (`UnsafeRelaxedJsonEscaping`) em vez de `\u00E7`.
- Evidências do laudo usam formatação pt-BR (`16,0 GB`) e "Não disponível: motivo" em vez de número cru ou "0".

### Evidência de build

```
PS> dotnet build FlavosDiag.sln -v q
Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)
```

### Evidência de teste

```
PS> powershell -NoProfile -ExecutionPolicy Bypass -File .\build\test.ps1 -SemBuild
Total: 37 | Aprovados: 37 | Falhas: 0 | Ignorados: 0
Relatório: ...\artifacts\testes.trx
```

Testes novos da fase 1 (14): esquema versionado do JSON, serial mascarado por padrão, `modoDemonstracao` presente
só quando é demonstração, aviso de pontuação sempre no arquivo, datas ISO 8601 com offset, dado ausente nunca vira
valor inventado, assinatura `%PDF-` e tamanho do PDF, SVG do gráfico, gravação do JSON em UTF-8 sem BOM,
exportação gravando os dois arquivos, exportação sem sobrescrever, mascaramento na exportação, pasta vazia recusada
e nome de arquivo sem caracteres inválidos.

### Evidência de verificação com hardware real

Roteiro assistido (`dotnet exec FlavosDiag.Tests.dll --roteiro-inventario`), executado nesta máquina:

```
Sistema: Microsoft Windows 11 Pro
Build: 26200.8875
Fabricante da placa: Micro-Star International Co., Ltd.
Modelo da placa: PRO A620M-E (MS-7E28)
BIOS: 1.J0
Processador: AMD Ryzen 5 8500G w/ Radeon 740M Graphics
Núcleos: 6
Threads: 12
Memória total: 16,0 GB
Pentes de memória: 2
  - Unknown 8 GiB @ 5200 MT/s (DIMMA1)
  - Unknown 8 GiB @ 5200 MT/s (DIMMB1)
GPU(s): 1
  - AMD Radeon 740M Graphics | VRAM: 0 B | driver 32.0.32015.7029
Limitações registradas: 2
  - Timings de memória (CAS/RAS/CL): Sem fonte confiável com licença compatível foi confirmada; o dado não é exibido.
  - VRAM dedicada da GPU: Não foi possível associar o adaptador DXGI à GPU WMI.
JSON: ...\artifacts\roteiro\laudo-inventario.json (1674 bytes)
PDF : ...\artifacts\roteiro\laudo-inventario.pdf (116799 bytes)
Veredito geral: Atenção
```

O JSON gerado contém `"numeroSerie": "07E2****8327"` (mascarado), `"memoria": "16,0 GB"`, `"geradoEm":
"2026-10-24T11:15:03-03:00"` e o aviso de pontuação própria. O PDF tem 1 página, assinatura `%PDF-1.4` e a fonte
Geist embutida.

Três observações honestas sobre o resultado acima:

1. **Timings de memória**: sem fonte pública com licença compatível, o dado é declarado indisponível em vez de
   vir de leitura direta de SPD (ver D abaixo).
2. **VRAM dedicada**: a GPU integrada expõe 0 B via DXGI; o app mostra "0 B" porque **foi lido** — é o valor que o
   Windows informa para memória dedicada em gráfico integrado, não uma estimativa nossa.
3. **Veredito "Atenção"** veio de "2 dispositivo(s) com erro no Gerenciador de Dispositivos" — leitura real desta
   máquina, não de fixture.

### Execução do aplicativo

```
PS> (execução do .exe por 18 s, modo normal e modo demonstração, stderr capturado)
modo normal       -> janela "Flavos Diag" aberta por 18 s, stderr vazio
modo --modo-demo  -> janela "Flavos Diag" aberta por 15 s, stderr vazio
```

### Verificações obrigatórias da skill

```
PS> powershell -NoProfile -ExecutionPolicy Bypass -File .\build\cleanroom.ps1
CLEAN ROOM: nenhum achado.

PS> dotnet list FlavosDiag.sln package --vulnerable --include-transitive
(nenhum pacote vulnerável em nenhum dos 6 projetos)
```

### Limitação de verificação (franqueza)

Nesta sessão o modelo não tem entrada de imagem, então **não foi feita inspeção visual** do PDF nem da janela.
O PDF foi conferido por estrutura (1 página, `%PDF-1.4`, fontes embutidas, 116 KB) e por testes automatizados;
a conferência visual fica no roteiro manual (`docs/ROTEIRO-TESTE-MANUAL.md`, a criar na Fase 5).

Status da fase: **concluída**.

---

## Fase 2 — Disco, bateria e rede

### Entregue

| Item | Situação |
|---|---|
| E3 Identificação de discos (modelo, firmware, série, barramento, tipo de mídia, tamanho) | pronto — verificado com 3 discos reais |
| E3 Saúde do disco: SMART ATA, log NVMe, previsão de falha do Windows, estado da API de armazenamento | pronto, com fontes por disco e limitação explícita quando uma via não responde |
| E3 Benchmark de disco em arquivo próprio, com arquivo apagado no final (inclusive em erro e cancelamento) | pronto — 3 testes verdes |
| E3 Varredura de superfície somente leitura sobre arquivo próprio | pronto |
| E3 Verificação do sistema de arquivos com `chkdsk /scan` (somente leitura, sem agendar correção) | pronto |
| E4 Bateria: `powercfg /batteryreport` (XML) + WMI, saúde = atual / projeto | pronto — máquina de teste não tem bateria, exercitado por fixture |
| E5 Rede: adaptadores, gateway, ICMP, DNS, Wi-Fi, teste de velocidade só para servidor informado | pronto — verificado com rede real |

### Evidência de build

```
PS> dotnet build FlavosDiag.sln --nologo -v q
Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)
```

### Evidência de teste

```
PS> powershell -NoProfile -ExecutionPolicy Bypass -File .\build\test.ps1 -SemBuild
Total: 60 | Aprovados: 60 | Falhas: 0 | Ignorados: 0
```

Testes novos da fase 2 (23): decodificação do bloco SMART (saudável, realocados, pendentes, vazio), log NVMe
(desgaste alto, saudável, aviso crítico), agregação de veredito por fonte de saúde, saúde da bateria
(saudável/degradada/sem ciclos/XML inválido), ping sem resposta, URL de velocidade inválida, benchmark
(recusa sem espaço, apaga o arquivo ao concluir, apaga ao cancelar, mede leitura/escrita/IOPS/latência),
e "sem fonte de saúde nunca vira OK".

### Evidência de verificação com hardware real (executado como administrador)

```
=== DISCOS ===
Discos encontrados: 3
  [0] SATA SSD | FW 0200Ic10 | SATA | Ssd | 223,6 GB
      série: 2020****0238
      fontes de saúde: SmartAta, EstadoWindows
      LIMITAÇÃO — Atributos SMART detalhados (ATA): Este controlador não repassa os atributos SMART;
                   a avaliação usa a previsão de falha do Windows.
                   O dispositivo recusou o comando SMART (status 0x51, erro 0x04).
  [1] STT256I1-2YAMCG2 | FW 060gAA10 | SATA | Ssd | 238,5 GB
      série: 2419****4204
      fontes de saúde: SmartAta, EstadoWindows
  [2] JMicron | FW 0215 | USB | mídia não informada | 931,5 GB
      série: DB12****86AE
      fontes de saúde: EstadoWindows
      LIMITAÇÃO — Atributos SMART (ATA): Função não suportada por este dispositivo.

=== BATERIA ===
  presente: não
  LIMITAÇÃO — Bateria: Nenhuma bateria encontrada neste equipamento. Não se aplica.

=== REDE ===
  adaptadores: 8 (Wi-Fi 6E, Ethernet, 2 túneis de VPN, Npcap, Wi-Fi Direct, Bluetooth)
  gateway: 192.168.0.1 | latência 8,2 ms
  alvo 8.8.8.8: 75,8 ms (perda 0%)
  DNS www.microsoft.com: resolvido em 94 ms
  Wi-Fi: CASA163_5GHZ 2
```

### O que a verificação real revelou (e foi corrigido)

1. **Nenhum disco aparecia.** A identificação usava só IOCTL de descritor, que devolve "acesso negado" sem
   elevação; o provider engolia tudo e devolvia lista vazia. Agora a lista vem do WMI
   (`Win32_DiskDrive` + `MSFT_PhysicalDisk`), que funciona sem administrador (D30).
2. **SMART falhava mesmo elevado.** O handle era aberto somente-leitura e o driver de armazenamento só
   repassa o pass-through ATA com acesso de escrita; o comando continua sendo só leitura
   (`SMART READ DATA`), nenhum setor é escrito (D31).
3. **Bateria era "presente" numa máquina sem bateria.** O `powercfg` gera o XML com `<Batteries></Batteries>`
   vazio em desktop; "o arquivo existe" não prova presença (D33).
4. **Gateway e Wi-Fi vinham vazios com internet funcionando.** O primeiro adaptador ativo era um túnel de VPN
   sem rota padrão, e o `powershell.exe` era procurado em `System32` em vez de
   `System32\WindowsPowerShell\v1.0` (D34).

### O que NÃO foi verificado (franqueza)

- **Atributos SMART detalhados** (setores realocados, horas, temperatura) não são legíveis nesta máquina: o
  controlador recusa o comando SMART nos dois SSDs SATA e a ponte USB não o suporta. Toda a lógica de
  interpretação está coberta por fixtures no formato da especificação, mas **não foi confirmada em disco real**.
  O veredito daqui usa a previsão de falha e o estado de saúde do Windows.
- **NVMe**: esta máquina não tem disco NVMe; só a fixture foi exercitada.
- **Bateria**: máquina de mesa, sem bateria. Só fixture.
- **Benchmark e varredura** foram executados em pasta temporária pequena, não em disco cheio nem em HDD real.
- **`chkdsk /scan`** não foi executado (leva dezenas de minutos); só a validação de entrada está testada.
- **Wi-Fi**: SSID lido corretamente, mas intensidade de sinal e qualidade continuam "não disponível" — a
  consulta exige API sem fio que ainda não foi implementada, e inventar número seria pior.

Status da fase: **concluída com as ressalvas acima** (itens sem verificação real registrados aqui e no
relatório final).

---
## Fase 3 — Sensores, estresse e benchmark

### Entregue

| Item | Situação |
|---|---|
| E2 Sensores reais atrás de `ISensorProvider` (temperatura, clock, carga, potência, tensão, ventoinha) | pronto; sem elevação ou driver → Inconclusivo com causa |
| E5 Estresse de CPU com limites, botão de parada e motivo registrado | pronto |
| E5 Teto obrigatório de 2 minutos quando não há sensor de temperatura | pronto |
| E6 Benchmark de CPU (5 amostras de 3 s, mediana, pontuação própria) | pronto |
| E4 Teste de memória com padrões, cancelável, com veredito próprio | pronto |

### Evidência de build

```
PS> dotnet build FlavosDiag.sln -c Release -warnaserror --nologo -v q
Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)
```

### Evidência de teste

```
PS> dotnet exec tests\FlavosDiag.Tests\bin\Debug\net8.0-windows\FlavosDiag.Tests.dll --runner
Total: 83 | Aprovados: 83 | Falhas: 0 | Ignorados: 0
```

### Evidência de clean room

```
PS> powershell -NoProfile -ExecutionPolicy Bypass -File .\build\cleanroom.ps1
CLEAN ROOM: nenhum achado.
```

### Bugs reais que os testes encontraram (todos corrigidos)

Os testes desta fase não passaram de primeira, e o motivo importa: eles encontraram defeitos no código
de produção, não nos testes.

1. **O estresse não executava trabalho nenhum.** `CargaCpuRunner` passava uma lista de cargas **vazia**
   para o executor. O laço `for` sobre zero cargas nunca girava, então nenhum trabalho era feito e — pior —
   o predicado de parada nunca era chamado, ou seja, **a proteção por temperatura não valia nada**. O teste
   "houve trabalho" falhou e expôs isso.
2. **A temperatura nunca era amostrada.** O intervalo entre leituras era subtraído de dois cronômetros
   diferentes (`relogio.Elapsed - ultimoRelogio.Elapsed`), o que dava diferença negativa e bloqueava toda
   leitura depois da primeira. Com o cronômetro de referência zerado, um teste curto também nunca amostrava.
   Um teste de estresse que não lê temperatura é um teste que não protege o equipamento.
3. **Cancelar virava exceção em vez de parada.** O executor usava `ManualResetEventSlim.Wait(duracao, token)`,
   que lança `OperationCanceledException`; o "Parar" do usuário virava falha em vez de motivo registrado.
4. **Sem barra de progresso, a parada era ignorada.** No caminho sem `IProgress`, o predicado de parada
   não era avaliado de tempos em tempos.

Os quatro são de segurança: três deles afetavam justamente as travas que protegem o equipamento do cliente.

### Teste de memória — reescrito conforme revisão

| Antes | Agora | Por quê |
|---|---|---|
| `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` como "memória livre" | `GlobalMemoryStatusEx` (`ullAvailPhys`) | "Total disponível" é um limite de política do GC, não a memória física livre. Superestima e pode levar o PC do cliente a paginar. |
| 75 % da memória, mínimo 16 MiB | 50 % da livre, teto 2 GiB, piso 256 MiB | Menos risco de paginação; piso maior dá significado ao teste. |
| Padrão caía no mínimo | Pedido ≤ 0 usa o **padrão** (maior tamanho seguro) | "Padrão = mínimo" subtestava a máquina em silêncio. |
| Dois `byte[]` grandes (fill/verify) | Blocos de 4 MiB com `Span`/`MemoryMarshal` | Dobrava a memória exigida e não permitia parada rápida. |
| `deveParar` (callback) | `IProgress<double>` + `CancellationToken` | Parada responde em menos de 1 s, verificada a cada bloco. |
| 5 padrões, dois por índice de byte | `0x00`, `0xFF`, `0x55/0xAA`, `índice×constante` (64 bits), complemento | Padrões de 64 bits e complemento pegam falhas de retenção que padrões de 8 bits não pegam. |
| Sem veredito | Teste rápido sem erro → **Inconclusivo**; 1 divergência → Atenção; divergências em padrões distintos → Falha | Um teste de segundos não sustenta a afirmação "a memória está boa". |

Nenhum teste desta fase roda estresse real: as durações são de milissegundos e os sensores são fake, conforme a
regra de não estressar hardware durante o desenvolvimento autônomo.

### O que NÃO foi verificado (franqueza)

- **Sensores em hardware real**: a leitura depende de driver de kernel e de elevação. A sessão foi validada sem
  elevação apenas no caminho de falha (devolve Inconclusivo com causa). **Não** foi confirmada a leitura real de
  temperatura nesta máquina — isso entra no roteiro manual.
- **Estresse real**: proibido durante o desenvolvimento. Os limites, a parada e os motivos estão cobertos por
  testes com sensores fake e durações curtas; a execução real (minutos, CPU aquecendo) não foi feita.
- **Teste de memória**: executado apenas com tamanhos pequenos (256 MiB) e memória livre injetada nos testes de
  borda. Não foi rodado com 2 GiB nem em máquina com memória defeituosa.
- **Benchmark de CPU**: a normalização e a mediana têm teste; uma execução completa (5 × 3 s × 2 modos) não foi
  feita nesta rodada.

---
## Fase 4 — GPU

### Entregue

| Item | Situação |
|---|---|
| E7 Prova de carga de GPU em Direct3D 11, fora de tela, sem janela | pronto — verificado em hardware real |
| E7 Detecção de artefatos por hash de pixels (FNV-1a 64 bits sobre 256×256 RGBA float) | pronto |
| E7 Monitoramento de reinício do driver (TDR) | pronto |
| E7 Benchmark de GPU com pontuação própria | pronto |
| Limite de temperatura da GPU (85 °C) e teto de 2 min sem sensor | pronto |

### Evidência de build

```
PS> dotnet build FlavosDiag.sln -c Release -warnaserror --nologo -v q
Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)
```

### Evidência de teste

```
PS> dotnet exec tests\FlavosDiag.Tests\bin\Debug\net8.0-windows\FlavosDiag.Tests.dll --runner
Total: 93 | Aprovados: 93 | Falhas: 0 | Ignorados: 0
```

### Evidência de verificação em hardware real

```
=== GPU (Direct3D 11, carga curta de verificação) ===
provider: Direct3D 11 (carga fora de tela)
adaptador: AMD Radeon 740M Graphics
duração real: 5,0 s de 5 s
frames executados: 794
motivo da parada: ConcluidoPorTempo
divergências de pixel: 0
temperatura máxima: não disponível (Este equipamento não expõe sensor de temperatura da GPU.)
limite configurado: 85 °C
AVISO: Nenhum sensor de temperatura da GPU respondeu: teste limitado a 2 minutos.

=== pontuação ===
pontos: 16,5 | fps médio: 161,5
```

### Bugs reais que a verificação encontrou

1. **Toda GPU seria reprovada.** A primeira execução acusou **806 divergências em 806 frames**. Causa: o shader
   usava o número do frame no cálculo, então cada frame produzia um resultado diferente por projeto. Um laudo
   assim condenaria uma GPU saudável. Corrigido: o shader agora é determinístico (o frame deixou de entrar no
   cálculo), e o resultado caiu para **0 divergências em 794 frames**.
2. **A causa da falha era descartada.** Quando o dispositivo não podia ser criado, o provider devolvia
   "não determinado" sem motivo, o que viola a regra de dado ausente com causa. Agora o motivo exato é levado
   até o resultado (`CausaAusencia`) e a categoria fica Inconclusivo.
3. **`Vortice.DirectX` não chegava à pasta de saída** por ser dependência apenas transitiva: o app quebrava em
   tempo de execução com `FileNotFoundException`. Resolvido com referência explícita.

### O que NÃO foi verificado (franqueza)

- **Temperatura da GPU**: os sensores não responderam **sem elevação**. A prova roda, mas o limite de 85 °C não
  pôde ser exercitado de fato — o teste ficou limitado pelos 2 minutos previstos para esse caso. Com o app
  elevado a leitura deve responder; isso entra no roteiro manual.
- **Detecção de artefato com defeito real**: não há GPU defeituosa nesta máquina. O caminho "divergência
  detectada" está coberto por teste (1 bit alterado muda o hash), mas não foi observado em hardware.
- **TDR**: nenhum reinício de driver ocorreu nesta máquina; o tratamento dos códigos está testado, o evento real não.
- **Rasterizador de software (WARP)**: existe como reserva para máquina virtual, mas nesta máquina o caminho
  usado foi a GPU de hardware.

---
## Fase 5 — Checklist, laudo, configurações e empacotamento

Pendente.
