# Flavos Diag — Regras de veredito

> Toda categoria do laudo recebe um dos quatro vereditos: **OK**, **Atenção**, **Inconclusivo**, **Falha**. (Bateria ausente em desktop é marcada **Não se aplica** e fica fora da agregação.)
> Implementado em `FlavosDiag.Core/Vereditos/`. Testado em `FlavosDiag.Tests/Vereditos/`. Se o código divergir deste documento, o código está errado.

## 1. Definições

| Veredito | Significado | Quando é atribuído |
|---|---|---|
| **OK** | Dados essenciais completos e nenhum indicador acima do limite. | Só com **evidência positiva e completa** da categoria. |
| **Atenção** | Dados completos, mas algum indicador fora do ideal. | Ação recomendada, não urgente. |
| **Inconclusivo** | Não foi possível obter a evidência essencial (sensor ausente, sem permissão, hardware não expõe, timeout, erro). | **Nunca vira OK.** Sempre acompanhado de causa. |
| **Falha** | Evidência de defeito que exige intervenção. | Troca/backup recomendados. |

## 2. Agregação (veredito geral)

Ordem de prioridade: `Falha` > `Inconclusivo` > `Atenção` > `OK`. `Não se aplica` é ignorada.

Algoritmo (`VereditoConsolidador.Agregar`):
1. Se qualquer categoria = `Falha` ⇒ geral = `Falha` (com a lista das categorias em Falha).
2. Senão, se qualquer categoria = `Inconclusivo` ⇒ geral = `Inconclusivo`.
3. Senão, se qualquer categoria = `Atenção` ⇒ geral = `Atenção`.
4. Senão, se **todas** as categorias aplicáveis = `OK` e há ≥ 1 ⇒ geral = `OK`.
5. Senão (nenhuma aplicável / lista vazia) ⇒ geral = `Inconclusivo` ("nenhuma categoria pôde ser avaliada").

> Racional: um Inconclusivo em qualquer lugar impede parecer "equipamento aprovado" (definição de pronto: sem leitura = Inconclusivo, nunca OK).

## 3. Regras por categoria

### 3.1 Sistema (inventário informativo)
- Sem SO ou sem CPU ⇒ **Inconclusivo** ("inventário essencial indisponível").
- Com SO e CPU:
  - ≥ 1 dispositivo com erro (código do Gerenciador de Dispositivos) ⇒ **Atenção**, lista com códigos e descrição própria.
  - Sem dispositivos com erro ⇒ **OK**.

### 3.2 Memória (inventário)
- Sem dados de memória ⇒ **Inconclusivo**.
- Total < 4 GiB ⇒ **Atenção** (informativo: pode ser limitante).
- Módulo com capacidade 0/ilegível ⇒ **Atenção**.
- Caso contrário ⇒ **OK**.
- O teste rápido interno **não** altera o veredito de inventário. Ele tem regra própria (3.2.1) porque responde a uma pergunta diferente: "a memória grava e devolve o que foi gravado?".

#### 3.2.1 Memória — teste rápido de padrões

| Situação | Veredito | Texto no laudo |
|---|---|---|
| Nada foi medido (memória livre ilegível, falta de memória, cancelado antes de concluir um padrão) | **Inconclusivo** | "Não foi possível testar a memória: <causa>." |
| Nenhuma divergência nos padrões testados | **Inconclusivo** | "Nenhuma divergência nos padrões testados. Isso não prova que a memória está perfeita: é um teste rápido, não um teste de estabilidade prolongado." |
| Uma divergência | **Atenção** | "Uma única divergência. Repita o teste antes de condenar o módulo." |
| Divergências em padrões distintos (duas execuções seguidas) | **Falha** | "Divergências em padrões distintos indicam defeito na memória: troque o módulo e teste novamente." |

Regra de ouro desta categoria: **teste rápido sem erro nunca vira OK.** Um OK afirmaria que a memória está boa, e um teste de segundos em um buffer não sustenta essa afirmação — o teste de boot completo é o único que cobre toda a memória física em milhares de passadas.

Tamanho do teste (decisão própria, em `PROCEDENCIA.md` P3-09a): 50 % da memória física **livre**, teto de 2 GiB, piso de 256 MiB. Cobertura = bytes testados / RAM total, sempre exibida junto do veredito para o técnico saber o que foi realmente coberto.

### 3.3 Disco
Por disco:
- **Falha** se qualquer: ATA setores realocados (5) > 0; ATA pendentes (197) > 0; ATA não corrigíveis (198) > 0; ATA erros (187) > 0; NVMe `CriticalWarning` ≠ 0; NVMe `MediaErrors` > 0; NVMe `PercentageUsed` ≥ 100; `ReadErrorsUncorrected` > 0 (fallback Windows).
- **Atenção** se qualquer: ATA `PercentageUsed` (231) ≥ 80; NVMe `PercentageUsed` ≥ 80 e < 100; NVMe `AvailableSpare` < 10 %; temperatura ≥ 60 °C; `Wear` ≥ 80 (fallback); varredura de superfície com blocos lentos (≥ 5× a mediana e > 500 ms) mas sem erro.
- **Inconclusivo** se nenhuma fonte de saúde pôde ser lida (SMART, NVMe e fallback ausentes) — "sem elevação ou disco não suporta".
- **OK** se há ao menos uma fonte de saúde e nenhuma condição acima.

Categoria "Disco": pior entre os discos pela ordem acima. Se não houver disco ⇒ Inconclusivo.

Recomendação automática:
- SSD com Falha (qualquer condição de mídia) ⇒ "Substituir o SSD".
- HDD com Falha ou com setores realocados > 0/pendentes > 0 ⇒ "Copiar os dados imediatamente (backup) e substituir o HDD".
- Verdito texto e ação sempre "detectar e isolar", nunca "reparar".

### 3.4 Sensores
- Nenhuma categoria de sensores lida por 60 s ⇒ **Inconclusivo** ("o hardware não expõe sensores para este modo de execução; verifique se executou como administrador").
- Temperatura ≥ limite configurado ⇒ **Atenção**; ≥ limite + 10 °C ⇒ **Falha**.
- Nenhum alerta e sensores presentes ⇒ **OK**.

### 3.5 CPU
Fontes: inventário + resultado do último estresse registrado na sessão + temperatura.
- Estresse com erros de cálculo ⇒ **Falha**.
- Throttling detectado com temperatura ≥ limite ⇒ **Atenção**.
- Throttling detectado sem temperatura ⇒ **Atenção** (medido por clock).
- Selam estresse nem temperatura ⇒ **Inconclusivo** ("sem teste de desempenho e sem sensores de temperatura").
- Estresse concluído sem erros, sem throttling, com temperatura ≤ limite ⇒ **OK**.
- CPU não detectada ⇒ **Inconclusivo**.

### 3.6 GPU
- Sem adaptador de vídeo ⇒ **Inconclusivo**.
- TDR/`DeviceRemoved` durante teste ⇒ **Falha**.
- Artefatos detectados ⇒ **Falha**.
- Temperatura ≥ limite ⇒ **Atenção**; ≥ limite + 10 °C ⇒ **Falha**.
- Sem teste executado ⇒ **Inconclusivo**.

### 3.7 Bateria
- Desktop (sem bateria) ⇒ **Não se aplica**.
- Sem capacidades de projeto/atual ⇒ **Inconclusivo**.
- Saúde = atual / projeto × 100: < 60 % ⇒ **Falha**; 60–79,9 % ⇒ **Atenção**; ≥ 80 % ⇒ **OK**.
- Ciclos disponíveis e ≥ 80 % do limite de projeto declarado no XML ⇒ no mínimo **Atenção**.

### 3.8 Rede
- Sem adaptador ativo ⇒ **Inconclusivo**.
- Sem teste executado ⇒ **Inconclusivo**.
- Perda ≥ 20 % ⇒ **Falha**; ≥ 5 % ⇒ **Atenção**; < 5 % com latência média > 100 ms ⇒ **Atenção**; senão **OK**.
- 100 % ICMP falhou mas TCP teve sucesso ⇒ **Inconclusivo** ("ICMP bloqueado; teste TCP respondeu") — não é OK nem Falha.
- 100 % falha ICMP e TCP ⇒ **Falha**.

### 3.9 Checklist
- ≥ 1 item **Falha** ⇒ **Falha**.
- ≥ 1 item **Pendente** (não preenchido) ⇒ **Inconclusivo**.
- Todos OK/Não se aplica ⇒ **OK**.

## 4. Tabela de testes obrigatórios (fixtures)

| Cenário | Esperado |
|---|---|
| Disco saudável (SMART zerado, vida 5 %) | OK |
| HDD com 2 setores realocados | Falha + recomendação backup+troca |
| SSD com vida usada 85 % sem erro | Atenção |
| NVMe `MediaErrors = 1` | Falha |
| Disco sem SMART e sem fallback | Inconclusivo |
| Bateria 48 Wh projeto / 30 Wh atual (62,5 %) | Atenção |
| Bateria 48 Wh / 25 Wh (52 %) | Falha |
| Bateria sem dados | Inconclusivo |
| Sem sensores de temperatura | Inconclusivo |
| Estresse com erro de cálculo | Falha |
| Rede com perda 10 % | Atenção |
| Rede 100 % ICMP, TCP ok | Inconclusivo |
| Lista vazia de categorias | Inconclusivo (geral) |
| Falha + OK + Inconclusivo | Falha (geral) |
| OK + Inconclusivo | Inconclusivo (geral) |
