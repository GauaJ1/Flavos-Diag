# Flavos Diag — progresso

Registro por fase. Cada fase só é marcada como concluída com a saída de build e de teste colada aqui.

Convenções:
- **Pronto** = coleta real + tratamento de erro/permissão + teste automatizado + execução de verificação.
- Dado ausente = **Inconclusivo** com causa; nunca "OK" por omissão.
- Testes de esforço/estresse só rodam automatizados com sensores falsos.

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

Pendente.

## Fase 2 — Disco, bateria e rede

Pendente.

## Fase 3 — Sensores, estresse e benchmark

Pendente.

## Fase 4 — GPU

Pendente.

## Fase 5 — Checklist, laudo, configurações e empacotamento

Pendente.
