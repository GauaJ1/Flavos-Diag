---
name: diag-testing-and-autonomy
description: Protocolo de trabalho autônomo e de testes do Flavos Diag - como manter PROGRESS.md, quando marcar BLOQUEADO, limite de tentativas, commits por fase, recuperação de contexto, regras anti-falsificação ("pronto" só com evidência), estratégia de testes com Fakes e fixtures, testes de segurança do estresse e do mascaramento, e os comandos de verificação. Use no início de qualquer sessão de trabalho no Flavos Diag, antes de declarar um item ou fase como concluído, ao escrever ou revisar testes, e sempre que o trabalho rodar sem supervisão.
---

# Trabalho autônomo e testes no Flavos Diag

Quando ninguém está olhando, o risco é declarar vitória sem prova. Este protocolo transforma "acho que funciona" em evidência que o dono consegue conferir de manhã.

## Ao iniciar (ou ao perder o contexto)
1. Leia `PROGRESS.md`, `docs/DECISOES.md` e `docs/ESPECIFICACAO.md`.
2. Rode `dotnet build -c Release -warnaserror` e `dotnet test -c Release` para saber o estado real.
3. Retome do primeiro item não concluído. Não refaça o que já tem evidência.

## PROGRESS.md (formato)
Uma checklist por fase com estado e evidência:
```
## Fase 1: Inventário e diagnóstico rápido
- [x] E1 Inventário de CPU — evidência: testes CpuInventoryTests (12 ok), roteiro manual pendente
- [ ] E11 Diagnóstico rápido — em andamento
- [!] E1 Timings de memória — BLOQUEADO: sem fonte pública com licença compatível (ver RISCOS-LEGAIS.md)
```
Estados: `[ ]` pendente, `[~]` em andamento, `[x]` pronto com evidência, `[!]` BLOQUEADO com motivo. Ao fechar cada fase, cole o resumo da saída de build, testes e auditoria de pacotes.

## Anti-falsificação: o que é "pronto"
Um recurso só é `[x]` se **todos** forem verdadeiros: tela ligada a dados reais, coleta real (não valor fixo), tratamento de erro e de falta de permissão, teste automatizado com fixtures, verificação executada por você e registrada. Proibido: tela de enfeite, botão sem ação, texto "em breve" em função que deveria estar pronta, valor inventado em laudo, `TODO` em fluxo principal.

Dado que não pôde ser medido vira **Inconclusivo** com a causa; nunca OK.

## Limite de tentativas e custo
- Máximo de 3 tentativas por problema com abordagens diferentes. Na 4ª: `[!] BLOQUEADO: motivo` no PROGRESS.md, deixe o projeto compilando e siga para o próximo item.
- Não repita o mesmo comando falho. Se o erro for igual, mude a hipótese.
- Não releia arquivos grandes sem necessidade; não reescreva o que funciona.
- Prefira o núcleo bem feito de cada fase a muitas coisas pela metade.

## Git
- Trabalhe em branch (ex.: `flavos-diag-mvp`). Commits pequenos; ao fechar a fase: `fase N: resumo`.
- Nunca deixe o projeto sem compilar. Se uma mudança quebrar e você não resolver, reverta **somente essa mudança**.
- Proibido: `push --force`, `reset --hard` em trabalho não commitado, apagar fora da pasta do projeto.
- Antes de commitar fase: rodar o verificador de clean room (skill `flavos-diag-clean-room`) e a auditoria de pacotes.

## Estratégia de testes
- **xUnit** em `tests/FlavosDiag.Tests`. Core testado sem Windows.
- **Fakes e fixtures**: providers reais só em roteiro manual. Fixtures anonimizadas em `tests/fixtures/` (SMART saudável, HDD com setores realocados, NVMe com desgaste, ponte USB sem SMART, bateria saudável e degradada, WMI parcial).
- **Determinismo**: nada de dependência de relógio, ordem de threads ou hardware. Injete `TimeProvider` e use sensores/cargas falsos.
- **Casos obrigatórios**:
  - Agregação de veredito: todas as combinações, lista vazia, Inconclusivo nunca vira OK.
  - Parsers: arquivo válido, campos ausentes, fora de faixa, malformado, vazio.
  - Estresse (com sensor e carga falsos): para por temperatura, para por tempo, sem sensor limita a 2 min, parada manual imediata, motivo registrado, exige confirmação para iniciar.
  - Disco: benchmark só em arquivo temporário próprio e o remove, inclusive em cancelamento e em erro; recusa iniciar sem espaço livre suficiente.
  - Mascaramento: com a opção ligada, nenhum identificador completo aparece em PDF, JSON, CSV ou log.
  - Benchmark: mesma entrada → mesma pontuação (determinismo), mediana correta.
  - JSON do laudo: valida contra o schema; `schemaVersion` presente.
- **Roteiro manual** (`docs/ROTEIRO-TESTE-MANUAL.md`) para o que exige hardware real: diagnóstico rápido sem admin, com admin, estresse curto com limite baixo de temperatura, varredura de leitura em disco de teste, bateria em notebook, notebook sem bateria, máquina virtual. **Nunca** rode estresse real durante o desenvolvimento autônomo.

## Comandos de verificação
```
dotnet restore --locked-mode
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
python -I .claude/skills/flavos-diag-clean-room/scripts/cleanroom_scan.py .
```

## Relatório final
Ao terminar (ou ao esgotar o possível), `docs/RELATORIO-FINAL.md` com: o que foi concluído por fase, o que ficou BLOQUEADO e por quê, decisões tomadas, riscos legais a revisar, como compilar/rodar/testar e os 5 próximos passos. Seja franco sobre o que não foi verificado em hardware real.
