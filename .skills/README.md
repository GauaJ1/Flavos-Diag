# Skills do Flavos Diag

Sete skills para o Claude Code ajudarem a construir o Flavos Diag (app de diagnóstico para Windows) sem se perder, sem quebrar o clean room e sem declarar "pronto" o que não está.

| Skill | Para que serve |
|---|---|
| flavos-diag-clean-room | Regras de clean room + verificador automático (`scripts/cleanroom_scan.py`) |
| flavos-diag-safety-rules | Segurança do equipamento do cliente e do próprio app (processos, privilégios, dados, dependências) |
| csharp-wpf-standards | Padrões de C#/.NET 8/WPF/MVVM, projeto, async, testes, publicação |
| windows-hardware-providers | WMI, SMART, powercfg, smartctl, sensores, degradação para "Inconclusivo" |
| diag-verdicts-and-reports | Veredito (OK/Atenção/Inconclusivo/Falha), benchmark próprio, laudo PDF/JSON |
| diag-testing-and-autonomy | Protocolo autônomo, PROGRESS.md, testes com Fakes e fixtures |
| flavos-diag-ui | Design WPF, temas, animações, acessibilidade |

## Instalação (Claude Code)
Copie as pastas para dentro do projeto do Flavos Diag, mantendo esta estrutura:

```
meu-projeto/
  .claude/
    skills/
      flavos-diag-clean-room/
      flavos-diag-safety-rules/
      csharp-wpf-standards/
      windows-hardware-providers/
      diag-verdicts-and-reports/
      diag-testing-and-autonomy/
      flavos-diag-ui/
```

Reinicie o Claude Code e confira com `/skills`. Para valer em todos os projetos, coloque em `~/.claude/skills/` (nesse caso ajuste o caminho do verificador citado nas skills).

## Frase para colar no início do prompt principal
```
Antes de qualquer trabalho, carregue as skills do projeto: flavos-diag-clean-room, flavos-diag-safety-rules,
diag-testing-and-autonomy, csharp-wpf-standards, windows-hardware-providers, diag-verdicts-and-reports e
flavos-diag-ui. Siga-as em todas as fases. Se uma skill contradisser o prompt, vale a regra mais restritiva
de segurança e de clean room, e você registra o conflito em docs/DECISOES.md.
```

## Observações
- As regras de segurança refletem boas práticas gerais e não substituem revisão de um profissional. Versões de .NET, NuGet e bibliotecas mudam: confira a documentação oficial atual.
- Os limiares de veredito são ponto de partida e configuráveis; ajuste com a sua experiência de bancada.
- O verificador de clean room reduz o risco, mas não o elimina. Dúvidas jurídicas continuam sendo para um profissional.
