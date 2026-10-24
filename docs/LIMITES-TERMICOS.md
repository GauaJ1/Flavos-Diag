# Flavos Diag — Limites térmicos

Este é o documento **canônico** dos limites térmicos numéricos do aplicativo. Se um número de
temperatura aparece em outro lugar — código, tela, laudo, configuração —, ele **precisa** vir daqui.

A fonte no código é `src/FlavosDiag.Core/Monitor/LimitesTermicos.cs`. Nenhum outro arquivo de
`src/` pode declarar um limite térmico próprio; um teste de arquitetura
(`tests/FlavosDiag.Tests/LimitesTermicosTests.cs`) percorre o fonte e reprova quem tentar.

> Como ler esta tabela: **Padrão** é o valor que o programa usa quando ninguém configurou nada.
> **Origem** diz de onde o número veio. Um número de origem "padrão do programa" é um ponto de
> partida, **não** uma especificação de fabricante, e nunca deve ser apresentado como tal.

## 1. Limites da CPU e da parada de segurança

| Limite | Padrão | Unidade | Onde é aplicado | Origem / justificativa |
|---|---|---|---|---|
| `VereditoCpuCelsius` | **95** | °C | `Regras.Cpu` (item térmico do veredito) e `Regras.Sensores` (limite da CPU). Padrão de `Configuracao.LimiteTemperaturaCpuCelsius`. | **Padrão do programa.** Subiu de 90 para 95 °C (D78). Processador moderno sob carga plena sustenta 90 °C de projeto sem defeito de refrigeração; o limite anterior reprovava máquina saudável, e veredito que reprova o que está bem ensina o técnico a ignorar o veredito. |
| `AlertaVisualCelsius` | **95** | °C | `StressViewModel` (faixa de alerta do gráfico) e `GraficoTemperatura.LimiarAlerta`. Padrão de `Configuracao.LimiarAlertaTemperaturaCelsius`. | **Padrão do programa.** Só visual: destaca a faixa no gráfico, não encerra o teste. Igual ao veredito para que a tela e o laudo apontem o mesmo ponto. |
| `ParadaAutomaticaCelsius` | **95** | °C | `ProtecaoTermica.LimiteParadaCelsius`, usado por `CargaCpuRunner` (stress e benchmark) e pela prova de stress da página CPU. Padrão de `Configuracao.LimiteParadaAutomaticaCelsius`. | **Padrão do programa.** Parada com 3 amostras consecutivas de confiança alta ≥ limite (D17, D72). Voltou de 100 para 95 °C para fechar com o alerta e o veredito: parar 5 °C acima do aviso dava ao técnico um intervalo em que já havia aviso e ainda havia carga total. |
| `EmergenciaCelsius` | **120** | °C | `CargaCpuRunner` — leitura crua sustentada acima disso encerra o teste como emergência, mesmo não sendo medição. | **Limite físico**, vindo de `TemperaturaFisica.MaximoValidoCelsius`. Não é literal repetido: a emergência e a fronteira de "isto não é medição" são o mesmo ponto, para nunca divergirem (D68, D77). |
| `ParadaMaximaCelsius` | **110** | °C | Validação de `LimitesTermicos`, tela de Configurações e `CargaCpuRunner`. | **Teto de segurança.** Parar só acima de 110 °C é parar tarde demais para proteger qualquer componente. Existe para que nem um `config.json` editado à mão desligue a proteção. |
| `ParadaMinimaCelsius` | **60** | °C | Validação de `LimitesTermicos`. | **Piso de sanidade.** Abaixo disso o "limite de parada" é limiar de alerta disfarçado e encerraria testes saudáveis. |

### Invariantes (obrigatórias)

A combinação é válida somente quando **todas** valem:

```
veredito <= alerta <= parada < emergência
parada <= 110 °C
1 °C <= cada valor <= 120 °C, e finito
```

`LimitesTermicos.Validar` verifica as invariantes e devolve **valores corrigidos** (reposição ao
padrão, campo a campo) junto com o motivo em pt-BR de cada correção. Uma configuração inválida
**nunca** é aceita em silêncio:

- na tela de **Configurações**, o técnico vê os motivos e nada é salvo;
- no caminho da configuração carregada, o valor inválido é reposto ao padrão e o motivo fica
  registrado.

## 2. Limites por tipo de componente

Usados por `Regras.Sensores`, que compara **cada componente com o seu próprio limite** (D73). Antes,
tudo era comparado com o limite da CPU, e um disco a 45 °C com limite de 90 °C produzia "OK"
enquanto o processador estava sem leitura.

| Componente | Limite | Unidade | Origem / justificativa |
|---|---|---|---|
| CPU | = limite da CPU em uso (padrão **95**) | °C | Padrão do programa; o mesmo número do veredito. |
| GPU | **limite da CPU + 5** (padrão **100**) | °C | Padrão do programa. A GPU é projetada para trabalhar mais quente que a CPU; com o mesmo limite, placa em operação normal geraria Atenção em todas as máquinas. Piso de 40 °C. |
| Memória | **85** | °C | Padrão do programa. Módulo DDR trabalha bem acima da temperatura de placa; 85 °C é a faixa em que a temperatura passa a ser sinal, não regime. |
| Armazenamento | **por mídia** — ver a tabela abaixo | °C | Padrão do programa, **exceto** quando o próprio disco declara o limiar. |
| Placa / SuperIO | = limite da CPU em uso | °C | Padrão do programa. Medem sensores dispersos pela placa; o limite da CPU serve de teto folgado. |

### 2.0 Fronteira entre Atenção e Falha

`Regras.Sensores` não tem uma terceira tabela de limites: ela usa o mesmo limite do componente e uma
margem para separar os dois vereditos.

| Constante | Padrão | Unidade | Onde é aplicado | Origem / justificativa |
|---|---|---|---|---|
| `MargemFalhaSobreOLimiteCelsius` | **10** | °C | `Regras.Sensores`: leitura `>= limite` → Atenção; leitura `>= limite + margem` → Falha. | **Padrão do programa.** Chegar ao limite é sinal para revisar a refrigeração; passar dele com folga é sinal de resfriamento que não está dando conta. Era um `+ 10` solto dentro de `Regras.Sensores` e foi trazido para cá (D80) para que a fronteira não divirja entre o veredito e o laudo. |

### 2.1 Armazenamento por tipo de mídia

> **Valores de partida; ajuste pela experiência de bancada e pela ficha do fabricante.**
>
> Nenhum destes três números foi copiado de um datasheet específico. São valores de partida
> escolhidos para separar os três regimes térmicos, que são muito diferentes entre si — um HDD
> trabalhando a 80 °C está em sofrimento, e um NVMe a 80 °C pode estar operando dentro do esperado.
> Antes havia um único limite de 60 °C para qualquer disco, o que aprovava mídia mecânica quente
> demais e reprovava SSD/NVMe em operação normal.

| Mídia | Limite | Unidade | Onde é aplicado | Origem / justificativa |
|---|---|---|---|---|
| HDD (mecânico) | **55** | °C | `Regras.DiscoIndividual` (SMART 194 e log NVMe) e `Regras.Sensores`. | **Valor de partida.** Mídia mecânica sofre desgaste acelerado acima da faixa de 50–55 °C. Também é o limite adotado quando a mídia é **desconhecida**: na dúvida, o aviso chega cedo. |
| SSD SATA | **70** | °C | Idem. | **Valor de partida.** Controladora e NAND toleram mais que um HDD, e o invólucro de 2,5" dissipa mal — 70 °C é a fronteira em que o controlador começa a reduzir. |
| SSD NVMe (M.2/PCIe) | **80** | °C | Idem. | **Valor de partida.** NVMe opera mais quente e normalmente já tem proteção térmica própria (throttling). Só vale como **limite genérico** — ver §2.2. |

### 2.2 Limiar declarado pelo próprio NVMe tem precedência

Se a **identificação ou o log do próprio disco** informar os limiares de aviso/crítico do
controlador (no NVMe, os campos *warning temperature threshold* / *critical composite temperature
threshold* do log SMART/Health), **esses valores devem ser preferidos** como referência, e a
**fonte precisa ser registrada** — campo do log, comando e data da leitura.

A forma de escolher é explícita em `LimitesTermicos.LimiteDoArmazenamentoComProcedencia`:

1. **Limiar do próprio componente disponível e plausível** → esse valor é adotado, com
   `Origem = ProprioComponente` e a fonte gravada. No laudo aparece como
   `"<valor> °C (limiar declarado pelo próprio componente — <fonte>)"`.
2. **Sem limiar declarado** → vale o padrão da mídia e o resultado é rotulado **`limite genérico`**,
   com a ressalva obrigatória:
   `"<valor> °C (limite genérico; valor de partida — ajuste pela experiência de bancada e pela ficha do fabricante)"`.

O ponto de fundo: um número do programa não pode ser apresentado como se fosse do fabricante. O
laudo diz qual dos dois está usando, sempre. Hoje o modelo `SaudeNvme` **não** carrega os campos de
limiar, então todo NVMe sai como `limite genérico` — o caminho de preferência já está pronto e
testado, e passa a valer quando o coletor trouxer o campo.

## 3. Limites que **não** moram aqui

| Assunto | Onde fica | Por quê |
|---|---|---|
| Faixa utilizável (1–120 °C) e sentinelas 0 / 255 / -128 | `TemperaturaFisica` | É a regra de "isto é uma temperatura utilizável", não um limiar de decisão. Precisa continuar sendo uma só regra, usada pela leitura, pelo gráfico e pelo laudo. |
| Quantas amostras consecutivas para parar (3) | `ProtecaoTermica` / `CargaCpuRunner` | É uma regra de histerese temporal, não um limite térmico. |
| Teto de duração do stress (30 min padrão, 60 min máximo) | `Configuracao` / `CargaCpuRunner.DuracaoMaximaPermitida` | É limite de tempo, não de temperatura. |
| Teto sem sensor (2 min, 10 min com confirmação) | `CargaCpuRunner` | Depende da confirmação explícita do técnico. |
| Faixa do eixo Y do gráfico (20–110 °C) | `GraficoTemperatura` | É escala de desenho; 110 é a altura do gráfico, não um limite de proteção. |

## 4. Como mudar um limite

1. Altere **apenas** `LimitesTermicos.cs` (constante, padrão por mídia ou cálculo por componente).
2. Atualize **esta tabela**, incluindo a origem/justificativa do número novo.
3. Registre a decisão em `docs/DECISOES.md` no formato `D<nº>`.
4. Se for um valor de partida que virou referência de fabricante, troque a origem na tabela e diga
   qual fabricante, modelo e documento.
5. Rode os testes: `LimitesTermicosTests` cobre as invariantes, a correção de configuração inválida
   e o teste de arquitetura que reprova literais térmicos fora deste módulo.
