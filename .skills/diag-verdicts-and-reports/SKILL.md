---
name: diag-verdicts-and-reports
description: Como o Flavos Diag calcula vereditos (OK, Atenção, Inconclusivo, Falha) e gera o laudo em PDF e JSON versionado - regras de agregação, limiares configuráveis para disco, bateria, temperatura e estresse, texto de evidências e limitações, mascaramento de números de série e esquema do JSON para importação pelo sistema de OS. Use sempre que for escrever ou alterar a lógica de veredito, os limiares, o modelo do laudo, o gerador de PDF/JSON, docs/REGRAS-VEREDITO.md, docs/SCHEMA-LAUDO.md ou a pontuação de benchmark.
---

# Veredito e laudo

O laudo é o produto final do Flavos Diag: é o que o técnico entrega ao cliente e anexa à OS. Um veredito errado tem custo real (cliente confia num disco que está morrendo, ou troca peça sem necessidade). Por isso as regras são explícitas, testadas e honestas sobre o que não foi medido.

## Estados
| Estado | Significado |
|---|---|
| **OK** | Foi medido e está dentro do esperado. |
| **Atenção** | Medido; há sinal que merece acompanhamento ou ação não urgente. |
| **Falha** | Medido; problema que exige ação (troca, reparo, backup imediato). |
| **Inconclusivo** | Não foi possível medir (sem permissão, sem sensor, erro) ou o teste não cobre o suficiente. |
| Não se aplica | O componente não existe (ex.: bateria em desktop). Não entra na agregação. |

## Regras de agregação (por categoria e geral)
1. Qualquer **Falha** → resultado **Falha**.
2. Senão, qualquer **Atenção** → **Atenção**.
3. Senão, qualquer **Inconclusivo** → **Inconclusivo**. Ele **nunca** é promovido a OK.
4. Senão → **OK**.
5. A categoria inteira "não se aplica" some do geral, mas aparece no laudo.
Precedência: Falha > Atenção > Inconclusivo > OK. Essas regras vivem em `FlavosDiag.Core` (sem dependência de Windows) e têm teste para cada combinação, incluindo a lista vazia.

## Limiares: decisão própria, configurável e documentada
Não existe um padrão universal para "disco bom". Os limiares abaixo são **ponto de partida do projeto**, não verdade absoluta: ficam em arquivo de configuração, com valor padrão documentado em `docs/REGRAS-VEREDITO.md` e marcados como "decisão própria" em `docs/PROCEDENCIA.md`. O técnico pode ajustá-los.

Exemplos de regras (ajuste e documente):
- **Disco (SMART/NVMe)**: saúde geral reportada como falha → Falha; setores pendentes ou não corrigíveis > 0 → Falha; setores realocados > 0 → Atenção (Falha se estiver crescendo entre leituras); vida útil restante NVMe baixa → Atenção/Falha por faixa; erros de mídia > 0 → Falha; temperatura acima da faixa → Atenção. SMART indisponível → Inconclusivo.
- **Bateria**: saúde = capacidade atual / capacidade de projeto; faixas configuráveis (ex.: Atenção abaixo de 70 %, Falha abaixo de 50 %); sem dados → Inconclusivo; desktop → Não se aplica.
- **Temperatura sob carga**: máxima atingida e throttling; ultrapassar o limite configurado ou throttling severo → Atenção/Falha por faixa; sem sensor → Inconclusivo.
- **Estresse**: parada por segurança (temperatura) → Atenção/Falha conforme a causa; erro de cálculo detectado → Falha; teste interrompido pelo usuário antes do tempo mínimo → Inconclusivo.
- **Memória**: inventário não é teste. Sem teste de boot completo registrado → Inconclusivo, com a recomendação de rodar o teste externo.
- **Rede**: perda de pacotes e latência acima de limites → Atenção; sem alvo configurado → Não se aplica.

Cada regra devolve, além do estado, um objeto de **evidência**: valor medido, limiar, unidade, fonte e motivo em pt-BR. O PDF mostra a evidência; ninguém precisa confiar só na cor.

## Benchmark
- Pontuação **própria**, determinística, com repetições e mediana. Mostrar sempre: "Pontuação própria do Flavos Diag; não comparável com outros programas".
- Serve para comparar a **mesma máquina antes e depois** do reparo. Guarde o histórico por identificador da máquina (hash, não número de série) com data, versão do app e condições (perfil de energia, temperatura inicial).
- Não escreva em nenhum lugar que uma pontuação "equivale" a outra de outro programa.

## JSON do laudo (versionado)
- Campo `schemaVersion` obrigatório (SemVer). Mudança incompatível = nova versão maior; mantenha o leitor da anterior ou documente a migração.
- Estrutura mínima: `meta` (app, versão, data ISO 8601, técnico, número da OS), `equipment`, `categories[]` (id, estado, evidências, limitações), `checklist[]`, `recommendations[]`, `limitations[]`, `overall`.
- Datas ISO 8601; números com ponto decimal e unidade explícita (`unit`); sem formatação de cultura.
- Documente em `docs/SCHEMA-LAUDO.md` e valide o JSON gerado contra um JSON Schema nos testes. Um arquivo de exemplo em `docs/exemplos/laudo-exemplo.json` (dados fictícios) acompanha o schema.
- O sistema de OS importará esse arquivo: o campo do número da OS (`meta.workOrder`) é livre (texto) e opcional.

## Mascaramento
- Opção "Ocultar identificadores" antes de exportar: números de série, MAC, nome de usuário e nome do computador mascarados (ex.: só os 4 últimos caracteres). Aplicar no PDF, JSON e CSV, e nos logs sempre.
- Teste automático: com mascaramento ligado, nenhum identificador completo das fixtures aparece na saída.

## PDF
- Uma a duas páginas: cabeçalho (técnico/MEI), equipamento, **veredito geral**, tabela por categoria (estado com ícone e texto, nunca só cor), principais evidências, **limitações** (o que não foi possível medir e por quê), recomendações e campo de assinatura.
- Linguagem simples para o cliente leigo; termos técnicos em seção à parte.
- Texto fixo de rodapé sobre benchmark próprio e data/versão do app.
- Gere a partir do mesmo modelo do JSON (uma única fonte de verdade). Teste: PDF gerado de fixture abre, tem N páginas esperadas e contém o veredito e as limitações.

## Linguagem das recomendações
Direta e com ação: "Faça backup agora e troque o disco" (Falha de disco), "Acompanhe: refaça o teste em 30 dias" (Atenção), "Não foi possível medir a temperatura; execute como administrador" (Inconclusivo). Nunca prometa garantia de funcionamento futuro.

## Checklist ao mexer em regras
- [ ] Regra nova tem teste para cada estado e para dado ausente.
- [ ] Limiar novo está em configuração, em `REGRAS-VEREDITO.md` e em `PROCEDENCIA.md`.
- [ ] Inconclusivo continua sem virar OK em qualquer caminho.
- [ ] JSON validado pelo schema; `schemaVersion` atualizado se necessário.
- [ ] Mascaramento testado.
