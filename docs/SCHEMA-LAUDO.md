# Schema do laudo — `flavos-diag/laudo/1`

O laudo é gravado em dois formatos a partir do mesmo objeto: PDF (para entrega ao cliente) e JSON (para
reaproveitamento e conferência). Este documento descreve o JSON **como ele é realmente gerado**; a
estrutura abaixo foi conferida contra um arquivo produzido pelo próprio programa.

## Regras gerais

| Regra | Valor |
|---|---|
| Identificador do schema | campo `versao` = `flavos-diag/laudo/1` |
| Codificação | UTF-8 **sem BOM** |
| Nomes de campo | `camelCase` |
| Valores de enum | texto em português, com acentuação (`Atenção`, `Inconclusivo`) |
| Datas e horas | ISO 8601 com fuso, ex.: `2026-10-24T14:32:10-03:00` |
| Dados ausentes | ausente ou `null` — **nunca** `0` ou string vazia para fingir medição |

> Observação de nomenclatura: o identificador de versão fica em `versao` (não em `schema`) e é um texto
> completo `flavos-diag/laudo/1`. Quem for ler o arquivo deve comparar esse campo antes de interpretar o
> resto.

## Estrutura

```jsonc
{
  "versao": "flavos-diag/laudo/1",
  "aplicativo": "Flavos Diag 0.5.0",
  "geradoEm": "2026-10-24T14:32:10-03:00",
  "numeroOs": "ROTEIRO-20261024-1115",

  "tecnico": {
    "nome": "string",
    "empresa": "string",
    "cnpj": "string",
    "contato": "string"
  },

  "equipamento": {
    "fabricante": "string|null",
    "modelo": "string|null",
    "numeroSerie": "string|null",   // mascarado quando seriaisMascarados = true
    "cpu": "string|null",
    "memoria": "string|null",
    "sistemaOperacional": "string|null",
    "discos": [ /* um objeto por disco */ ]
  },

  "resumo": "Atenção em: Sistema.",   // texto corrido, não objeto
  "vereditoGeral": "OK|Atenção|Falha|Inconclusivo|Não se aplica",

  "categorias": [
    {
      "categoria": "string",
      "veredito": "OK|Atenção|Falha|Inconclusivo|Não se aplica",
      "/* demais campos de evidência por categoria */": "..."
    }
  ],

  "evidencias": { /* números medidos, organizados por área */ },
  "graficos": [ /* séries usadas nos gráficos do PDF */ ],

  "limitacoes": [
    { "item": "string", "motivo": "string", "fonte": "string|null" }
  ],

  "recomendacoes": [
    { "texto": "string", "prioridade": "string" }
  ],

  "checklist": [
    { "id": "string", "titulo": "string", "status": "Pendente|OK|Falha|Não se aplica", "observacao": "string|null" }
  ],

  "assinatura": { "tecnico": "string" },

  "seriaisMascarados": true,

  "avisoPontuacao": "Pontuação própria do Flavos Diag; não comparável com outros programas."
}
```

## Contrato do campo `veredito`

| Valor | Significado | Nunca significa |
|---|---|---|
| `OK` | Medido e sem sinais de problema | "Não testei e parece bem" |
| `Atenção` | Medido, com sinal que merece verificação | Falha confirmada |
| `Falha` | Medido, com sinal claro de problema | — |
| `Inconclusivo` | **Não foi possível medir**, ou a medição é insuficiente | Qualquer forma de aprovação |
| `Não se aplica` | O item não existe neste equipamento (ex.: bateria em desktop) | Falha |

Regra de agregação: `Falha` > `Inconclusivo` > `Atenção` > `OK`. `Não se aplica` é ignorado. Lista vazia
resulta em `Inconclusivo`.

## Campos que podem vir ausentes

Praticamente todos os valores medidos. Um laudo com muitos campos ausentes é um laudo **honesto sobre um
equipamento pouco acessível** — cada ausência deve ter o motivo correspondente em `limitacoes`.

Motivos de ausência, conforme `Leitura<T>`:

| Situação | O que aparece |
|---|---|
| Não suportado pelo equipamento | `Inconclusivo` + limitação com motivo |
| Sem permissão (não elevado) | `Inconclusivo` + limitação dizendo que falta elevação |
| Erro na leitura | `Inconclusivo` + limitação com o erro |
| Nunca lido | `Inconclusivo` + limitação "não coletado" |

## Mascaramento

Com `seriaisMascarados` em `true` (padrão), números de série aparecem como os 4 primeiros e os 4 últimos
caracteres separados por `****`, e apenas quando o valor tem 12 caracteres ou mais; abaixo disso, só `****`
(nunca se revela o tamanho real).

Desligar o mascaramento é decisão do técnico, registrada na configuração.

## Compatibilidade

- Um leitor **deve** comparar `versao` e recusar valores diferentes de `flavos-diag/laudo/1` em vez de
  tentar interpretar.
- Campos desconhecidos devem ser ignorados (leitura tolerante), para permitir acrescentar informação sem
  quebrar leitores antigos.
- O PDF é gerado do mesmo objeto; divergência entre PDF e JSON é sempre defeito.

## Como este documento foi conferido

O JSON acima foi comparado com um laudo gerado pelo próprio programa durante a verificação
(`--roteiro-inventario`), que produziu os campos `versao`, `aplicativo`, `geradoEm`, `numeroOs`, `tecnico`,
`equipamento`, `resumo`, `vereditoGeral`, `categorias`, `evidencias`, `graficos`, `limitacoes`,
`recomendacoes`, `checklist`, `assinatura`, `seriaisMascarados` e `avisoPontuacao`.
