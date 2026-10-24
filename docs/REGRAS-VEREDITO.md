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

#### 3.2.1 Memória — teste rápido de padrões (regra única)

Esta é a **única** regra da memória para o teste rápido. Ela substitui as formulações antigas das decisões
D38, D56 e D58 (ver D79 em `DECISOES.md`). A unidade de contagem é a **execução**: cada padrão aplicado ao
buffer na mesma rodada do teste é uma execução independente. `ErrosDetectados` conta palavras divergentes e
`ExecucoesComErro` conta padrões que divergiram; a regra usa a segunda contagem, porque duas palavras erradas
no mesmo padrão são uma única evidência de defeito, não duas.

| # | Caso | Entradas (`BytesTestados`, `ExecucoesComErro`) | Veredito | Justificativa |
|---|---|---|---|---|
| M1 | Zero erros, sem nenhum teste executado (só inventário) | `0`, `0` (ou `ResultadoTesteMemoria` ausente, ou `Status = NaoExecutado`) | **Inconclusivo** + recomendação de executar o teste | Inventário diz quanto há instalado, não se os módulos retêm dados. Sem gravar e reler, nada foi medido; aprovar aqui seria afirmar o que ninguém verificou. |
| M2 | Zero erros, só o teste rápido (sem divergência em nenhum padrão) | `> 0`, `0` | **Inconclusivo** + recomendação do teste de boot completo | Um teste de segundos que cobre parte da memória livre não sustenta "a memória está boa". O teste rápido sem erro **nunca** vira OK; ele só descarta a evidência de defeito. |
| M3 | 1 erro (uma execução com divergência) | `> 0`, `1` | **Atenção** + repetir o teste | Uma divergência isolada pode ser defeito intermitente ou interferência de outro programa, overclock ou evento isolado. Não basta para condenar o módulo, mas também não pode ser ignorada. |
| M4 | 2 erros no mesmo padrão (2+ palavras divergentes, 1 execução com erro) | `> 0`, `1` | **Atenção** + repetir o teste | O defeito apareceu uma única vez, ainda que em vários pontos do mesmo padrão. A repetição é o que separa defeito de evento isolado — a segunda execução é a evidência que falta. |
| M5 | 2 erros em padrões distintos (2+ execuções com divergência) | `> 0`, `≥ 2` | **Falha** + trocar o módulo e testar novamente | Dois padrões independentes falharam: não é um evento isolado nem dependência de um padrão específico. A memória não está retendo o que foi gravado. |

> **Leitura com atenção:** o ponto 4 (`ErrosDetectados ≥ 2`) **não** é usado para Falha. Antes desta regra o
> código tratava 2 ou mais palavras divergentes como Falha, mesmo dentro de uma única execução; isso conflitava
> com o caso M4 acima.

Regra de ouro desta categoria: **teste rápido sem erro nunca vira OK.** Um OK afirmaria que a memória está boa, e um teste de segundos em um buffer não sustenta essa afirmação — o teste de boot completo é o único que cobre toda a memória física em milhares de passadas.

Fora do teste, a categoria ainda aplica o inventário (seção 3.2) nos casos M1 e M2: total abaixo de 4 GiB ou
módulo com capacidade ilegível viram **Atenção** mesmo com o teste rápido limpo, porque ali o motivo é o
inventário, não o teste.

> **PENDENTE — divergência entre este documento e o código (não corrigida).** `Regras.Memoria`, em
> `src/FlavosDiag.Core/Vereditos/Regras.cs:125`, usa `teste.ExecucoesComErro >= 2 || teste.ErrosDetectados >= 2`
> e portanto **reprova o caso M4** (2 palavras divergentes em um único padrão) como Falha, enquanto a tabela
> acima manda Atenção. Os casos M1, M2, M3 e M5 batem com o código. A correção exige remover o termo
> `|| teste.ErrosDetectados >= 2` da condição e ajustar o teste `Regras memória: duas divergências é Falha`
> (`tests/FlavosDiag.Tests/RegrasMemoriaTests.cs:76-83`), que trava o comportamento atual. Isso muda
> comportamento de veredito e ficou registrado aqui, não alterado.

> **Fonte do padrão de `ExecucoesComErro`.** O campo do record tem padrão `1`
> (`src/FlavosDiag.Core/Modelos/Testes.cs:149`), herdado de chamadores antigos que só criavam o resultado
> quando havia erro. Isso é compatibilidade de serialização: `TesteMemoriaRunner` sempre preenche o campo com a
> contagem real de padrões com divergência (`src/FlavosDiag.Workloads/TesteMemoriaRunner.cs:403`), então a regra
> acima nunca depende do padrão. Nenhuma configuração de usuário controla esse valor; não há campo
> `ExecucoesComErro` em `Configuracao`.

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

### 3.10 Parada automática do stress (não é veredito, é limite de segurança)

A D17 pedia parada com **2 amostras consecutivas ≥ limite, com histerese**. O que está em vigor é a D72: a
contagem é de **3** amostras e **não há histerese na direção contrária**.

| Situação | Efeito no contador de calor | Onde no código |
|---|---|---|
| Leitura de confiança alta ≥ limite | incrementa | `src/FlavosDiag.Core/Monitor/ProtecaoTermica.cs:66-76` |
| Leitura de confiança alta < limite | **zera** (qualquer valor abaixo, não "abaixo de limite − histerese") | `src/FlavosDiag.Core/Monitor/ProtecaoTermica.cs:77-81` |
| Leitura ausente ou sentinela | **mantém** (não zera, não incrementa) | `src/FlavosDiag.Core/Monitor/ProtecaoTermica.cs:86-89` |
| Contador atinge 3 | para, motivo `Temperatura` | `src/FlavosDiag.Core/Monitor/ProtecaoTermica.cs:25,69` |

Detalhes que não mudam o comportamento, mas confundem na leitura do código:

- `CargaCpuRunner.AmostrasAcimaDoLimiteParaParar = 3` (`src/FlavosDiag.Workloads/CargaCpuRunner.cs:43`) repete
  o valor e **é usada** na parada do stress (`CargaCpuRunner.cs:234`). O caminho de parada do **benchmark**
  (`CargaCpuRunner.cs:372`) escreve o literal `3` em vez de usar a constante: mesmo valor, mesma regra, mas
  mudar a constante não muda o benchmark.
- `CargaCpuRunner.AmostrasConsecutivasEmergencia = 2` (`CargaCpuRunner.cs:57`) é a contagem da **emergência**
  para leitura crua acima de 120 °C, não a da parada por temperatura medida. As duas contagens diferentes são
  intencionais (ver D72).
- `CargaCpuRunner.DuracaoMaximaPermitida = 60 min` (`CargaCpuRunner.cs:46`) corta o teto de 120 min que a D72 e
  `Configuracao.TetoDuracaoStressMinutos` (`src/FlavosDiag.App/Services/Servicos.cs:70`, padrão 30) permitem:
  o campo de configuração aceita até 120, mas a carga nunca passa de 60.

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
| Memória: sem teste executado (M1) | Inconclusivo |
| Memória: teste rápido sem divergência (M2) | Inconclusivo |
| Memória: 1 execução com divergência (M3) | Atenção + repetir |
| Memória: 2 palavras divergentes no mesmo padrão (M4) | Atenção + repetir |
| Memória: 2 execuções com divergência (M5) | Falha + trocar o módulo |
