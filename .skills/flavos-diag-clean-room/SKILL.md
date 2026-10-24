---
name: flavos-diag-clean-room
description: Regras de clean room do Flavos Diag (app de diagnóstico de hardware para Windows). Use SEMPRE antes de escrever ou revisar a especificação, o código, os textos de interface, nomes de recursos ou dependências do Flavos Diag, e sempre que a conversa ou o código mencionar AIDA64, Cinebench, FurMark, CPU-Z, GPU-Z, HWiNFO, CrystalDiskInfo, OCCT, Prime95, 3DMark, REA, Ghidra, IDA, Hopper, dnSpy, ILSpy, decompilar, engenharia reversa, "copiar" ou "igualar" uma função de outro programa. Use também antes de cada commit de fase para rodar o verificador.
---

# Clean room do Flavos Diag

O objetivo é recriar FUNÇÕES (ler sensores, testar disco, estressar CPU) com código 100% original. A função em si não é protegida; o código, o nome, a marca, a interface e os textos de outros programas são. Esta skill existe para que o resultado seja defensável: qualquer decisão técnica deve ter origem pública ou ser claramente decisão própria.

## O que é proibido (e por quê)
- Decompilar, desmontar, depurar ou analisar binários de terceiros, ou usar ferramentas de engenharia reversa (REA, Ghidra, IDA, Hopper, dnSpy, ILSpy, x64dbg). Isso contamina o projeto: depois de ver o código, você não consegue provar que o seu é independente.
- Ler ou copiar código-fonte de ferramentas proprietárias. Para código aberto, use apenas como dependência, respeitando a licença; não cole trechos no seu código.
- Baixar ou executar versões piratas.
- Usar nomes, marcas, ícones, textos, strings, layout ou identidade visual de outros programas. Isso vale para a interface, o código, os comentários e os nomes de arquivos.
- Reproduzir fórmulas ou algoritmos proprietários de benchmark ou prometer pontuações comparáveis com os de outros programas.

## Fontes permitidas
Documentação oficial da Microsoft (WMI/CIM, APIs de armazenamento, DXGI, Direct3D, powercfg), especificações públicas (SMBIOS/DMTF, ACPI, ATA/SMART, NVMe), documentação das bibliotecas que o projeto usa, e conhecimento técnico geral e público. Nos documentos do projeto, refira-se a "categorias de função" (ex.: "inventário de hardware"), não a produtos.

## Fluxo para cada recurso
1. Escreva em `docs/ESPECIFICACAO.md` o comportamento esperado e as fontes públicas.
2. Implemente só a partir da especificação.
3. Registre em `docs/PROCEDENCIA.md` a origem de cada decisão técnica (fonte pública ou "decisão própria") e em `docs/LICENCAS.md` cada dependência (nome, versão, licença, forma de uso).
4. Dúvida jurídica vira linha em `docs/RISCOS-LEGAIS.md`. Nunca escreva que algo é "legalmente seguro".

Se você não consegue apontar uma fonte pública para uma decisão (um endereço de registro, um offset, um algoritmo específico), pare: use uma biblioteca com licença verificada ou registre como BLOQUEADO. Não "descubra" o dado analisando outro programa.

## Verificador automático
Antes de cada commit de fase, rode:

```
python .claude/skills/flavos-diag-clean-room/scripts/cleanroom_scan.py .
```

Ele procura nomes de produtos de terceiros e de ferramentas de engenharia reversa em `src/`, `tests/` e arquivos de interface, e lista binários (.exe, .dll, .sys) versionados fora de `bin/` e `obj/`, que precisam constar em `docs/LICENCAS.md`. Saída com achados = corrigir antes de commitar. Exceções legítimas (ex.: a própria lista de proibições) vão em `scripts/cleanroom_allow.txt`, uma linha por caminho relativo.

## Checklist antes de fechar a fase
- [ ] Nenhuma ferramenta de engenharia reversa foi instalada ou usada.
- [ ] Verificador sem achados.
- [ ] Cada recurso novo tem fonte em ESPECIFICACAO.md e linha em PROCEDENCIA.md.
- [ ] Cada dependência nova tem linha em LICENCAS.md, com a licença confirmada no repositório oficial dela (não de memória).
- [ ] Nomes, textos e visual da interface são próprios.
- [ ] A interface diz que a pontuação é própria e não comparável.
