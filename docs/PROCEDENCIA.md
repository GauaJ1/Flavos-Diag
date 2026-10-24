# Procedência das informações

Este arquivo registra **de onde veio cada número e cada API** que o Flavos Diag usa. A regra é
simples: se um dado não tem fonte declarada aqui, ele não pode virar afirmação no laudo.

Datas no formato dd/mm/aaaa.

## 1. Detecção do driver de leitura de sensores (PawnIO)

| Item | Valor | Origem |
|---|---|---|
| Tipo usado | `LibreHardwareMonitor.PawnIo.PawnIo` | Namespace público da dependência declarada `LibreHardwareMonitorLib` 0.9.7-pre771, obtido pelo IntelliSense do próprio pacote. **Nenhuma engenharia reversa, nenhuma descompilação.** |
| Propriedade de instalação | `static bool IsInstalled` | Idem. É a única forma usada para saber se o driver está presente. |
| Versão do driver | `static string? Version` | Idem. Aparece no diagnóstico exportado; `null` quando não instalado. |
| Momento da consulta | a cada abertura de sessão de sensores | Quando o driver é instalado com o aplicativo já aberto, a sessão em uso não o percebe. Por isso a orientação de fechar e reabrir. |
| Comando sugerido ao técnico | `winget install namazso.PawnIO -e` | Identificador do pacote no repositório público do winget. O aplicativo **não** executa o comando; apenas mostra o texto para cópia. |
| Site oficial | https://pawnio.eu | Divulgado pelo próprio projeto. O técnico deve conferir antes de instalar. |

O Flavos Diag **nunca instala, baixa ou redistribui** o driver. Ver `docs/LICENCAS.md` §1.2 e
`docs/RISCOS-LEGAIS.md` R11.

## 2. Limites térmicos

A tabela canônica está em `docs/LIMITES-TERMICOS.md`; cada linha de lá aponta a origem. Resumo das
fontes externas:

| Item | Valor | Origem | Situação |
|---|---|---|---|
| Temperatura máxima do AMD Ryzen 5 8500G (Tjmax) | ~95 °C | **Não confirmado em documento oficial da AMD** acessível nesta sessão | O padrão de parada (95 °C) é conservador e **coincide** com o valor citado publicamente para esta família, mas a fonte oficial não pôde ser lida. Por isso o texto da tela não afirma "o fabricante especifica 95 °C". |
| Sentinelas de "sem leitura" (0, 255, -127, -128) | — | Convenções documentadas de sensores ATA/SMBus e observação direta nesta máquina | Verificado por medição: `Core (Tctl/Tdie)` devolveu `0` com `Min 0`/`Max 0` sem elevação. |
| Faixa plausível de temperatura de componente | 1 a 120 °C | Decisão do projeto (D68) | Não é especificação de fabricante; é o filtro de plausibilidade do aplicativo. |
| Limites de disco por mídia (HDD 55, SSD SATA 70, NVMe 80) | — | **Valores de partida**, não especificação | Devem ser ajustados pela experiência de bancada e pela ficha do fabricante. Rotulados como "limite genérico" na interface quando não há limiar do próprio disco. |
| Limiar de aviso/crítico do NVMe | varia por modelo | Log SMART/identificação do próprio controlador, quando disponível | A precedência está implementada e testada; o campo ainda não é coletado (ver `docs/LIMITES-TERMICOS.md` §2.2). Enquanto não for, todo NVMe sai como "limite genérico". |

**Nada aqui promete proteção do hardware.** O aplicativo reduz o risco de um teste de estresse
passar do ponto; ele não substitui o limitador térmico do próprio processador.

## 3. Comandos de disco (ATA / NVMe)

| Item | Origem |
|---|---|
| ATA `IDENTIFY DEVICE` 0xEC, `SMART` 0xB0, `SMART READ DATA` 0xD0, `SMART READ THRESHOLDS` 0xD1, `WRITE SECTORS` 0x30 | `include/uapi/linux/hdreg.h` (cabeçalho público do kernel Linux) |
| NVMe `Get Log Page` 0x02, `Identify` 0x06, `Get Features` 0x0A, log `SMART` 0x02 | `include/linux/nvme.h` (cabeçalho público do kernel Linux) |
| IOCTL de pass-through ATA e SCSI_MINIPORT | Documentação pública de `DeviceIoControl`/`IOCTL_ATA_PASS_THROUGH_DIRECT` e `IOCTL_SCSI_MINIPORT` da Microsoft |

Somente os comandos **de leitura** entram na lista de permissão em vigor
(`src/FlavosDiag.Providers/Nativo/ComandosDiscoPermitidos.cs`). Ver D84.

## 4. Medições feitas na máquina do dono

Registradas porque sustentam decisões; o hardware do dono é AMD Ryzen 5 8500G, Radeon 740M,
placa MSI PRO A620M-E, Windows, 1366×768.

| Data | Medição | Resultado |
|---|---|---|
| 24/10/2026 | Leitura de temperatura **sem** elevação, com PawnIO instalado e em execução | `Core (Tctl/Tdie)` = 0 (mín 0, máx 0). `PawnIo.IsInstalled = True`. |
| 24/10/2026 | Mesma leitura **elevada** | `Core (Tctl/Tdie)` = 69 °C (mín 68,875, máx 69,0) — **a leitura funciona** |
| 24/10/2026 | `GPU Core` em três leituras ao longo de 5 s | 46 °C, mínimo 46, máximo 46 nas três — sensor saudável e estável |

**Conclusão que esses números sustentam:** a ausência de leitura da CPU nesta máquina era falta de
**elevação**, não ausência de driver. E o caso do `GPU Core` provou que `Min == Max` **não** pode ser
tratado como ausência de leitura, o que corrigiu a decisão D68 (ver D78).

**O que estes números NÃO provam:** que todo Ryzen 5 8500G se comporta assim; que o estresse real com
o driver ativo é seguro; que a leitura funciona em outras versões do driver. A validação no hardware
do dono continua **pendente** para o estresse.
