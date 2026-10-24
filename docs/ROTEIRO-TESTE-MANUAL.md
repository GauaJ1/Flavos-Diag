# Roteiro de teste manual — Flavos Diag

Este documento existe porque **nem tudo pode ser verificado autonomamente**. O que foi executado e
verificado está em `PROGRESS.md` com a saída dos comandos. O que está aqui é o que precisa de uma pessoa,
de um equipamento específico ou de elevação de privilégio.

Regra: um item só muda de "não verificado" para "verificado" quando alguém executa o passo e cola o
resultado observado. Não basta "parece que funciona".

---

## 1. Antes de começar

| Preparação | Como |
|---|---|
| Build de release | `dotnet build FlavosDiag.sln -c Release --nologo` |
| Pacote portátil | `powershell -ExecutionPolicy Bypass -File .\build\publish.ps1` |
| Pasta do pacote | `dist\FlavosDiag-0.5.0-win-x64\` |
| Pasta de dados | `%LOCALAPPDATA%\FlavosDiag` (configurações e laudos) |

Anote, para cada item que testar: a data (dd/mm/aaaa), o equipamento, e o resultado observado.

---

## 2. Verificações que dependem de privilégio

O desenvolvimento foi feito **sem elevação**. Estes itens não puderam ser confirmados.

### 2.1 Sensores de temperatura (com administrador)

1. Clique com o botão direito em `FlavosDiag.App.exe` → **Executar como administrador**.
2. Abra a aba **CPU** e observe o campo de temperatura.

- **Esperado sem elevação:** "Sem leitura de temperatura" ou "Inconclusivo" com motivo, em máquina cujo
  driver de sensores exige privilégio.
- **Esperado com elevação:** temperatura em °C, atualizando.
- **Se continuar sem leitura com elevação:** o equipamento realmente não expõe o sensor. Isso não é
  defeito — é limitação do hardware, e o laudo deve dizer isso.

Resultado observado: ______________________________________________

### 2.2 Temperatura da GPU

Mesmo procedimento na aba **GPU**, depois de clicar em "Prova de GPU".

Durante o desenvolvimento, a prova de GPU **funcionou** (AMD Radeon 740M, 794 quadros, 0 divergências),
mas **a temperatura não respondeu sem elevação**. Confirmar se responde com elevação.

Resultado observado: ______________________________________________

### 2.3 Atributos SMART de disco ATA

1. Como administrador, abra a aba **Disco** e clique em "Ler novamente".
2. Verifique, em "Fontes de saúde consultadas", se aparece "Atributos do disco (ATA)".

Durante o desenvolvimento, neste controlador (AMD A620, AHCI) **o comando de atributos ATA foi recusado pelo
controlador** (`status=0x51`, registro de erro `0x04`). O app caiu para o estado de saúde do Windows, o que é
o comportamento correto, mas **os atributos SMART brutos não foram exercitados em hardware real**.

Resultado observado: ______________________________________________

### 2.4 Disco NVMe

**Não verificado:** a máquina de desenvolvimento não tem unidade NVMe (2 SSD SATA + 1 USB). O caminho de
leitura do registro de saúde NVMe está coberto apenas por teste com dados sintéticos.

Equipamento com NVMe usado no teste: ______________________________________
Resultado observado: ______________________________________________

### 2.5 Bateria

**Não verificado:** a máquina de desenvolvimento é um desktop sem bateria. O caminho "não se aplica" foi
exercitado (aparece corretamente); o caminho "bateria presente, com ciclos e capacidade" não.

Notebook usado no teste: ______________________________________
Resultado observado: ______________________________________________

---

## 3. Testes que mexem no equipamento

Executar **apenas** com autorização do dono do computador, em superfície ventilada, e nunca em máquina
que esteja executando trabalho do cliente.

### 3.1 Estresse de CPU

1. Aba **CPU** → "Teste de estresse" → confirmar.
2. Observar a temperatura e o andamento.
3. Deixar terminar ou parar pelo botão.

- **Esperado:** para sozinho ao atingir o limite de parada automática (padrão 100 °C, 3 amostras
  consecutivas), ou em 2 min se não houver temperatura de CPU válida; registra o motivo.
- **Esperado ao fim do teto:** com duração padrão de 30 min, aviso um minuto antes do fim e encerramento
  pelo teto de duração, se nada parou antes.
- **Nunca deve:** continuar com temperatura de confiança alta acima do limite de parada; travar a interface.

Resultado observado: ______________________________________________

### 3.2 Teste de memória

1. Aba **Memória** → "Testar memória" → confirmar.
2. Observar a barra de progresso até o fim.

- **Esperado:** verifica até 50 % da memória livre, limitado a 2 GiB e nunca menos que 256 MiB.
- **Esperado sem erro:** veredito **Inconclusivo**, não OK (um teste rápido não prova que a memória está boa).
- **Esperado num pente defeituoso:** veredito Falha, com o deslocamento da primeira divergência.

Resultado observado: ______________________________________________

### 3.3 Prova de GPU

1. Aba **GPU** → "Prova de GPU" → confirmar.
2. Observar por 1 minuto.

- **Esperado:** nada aparece na tela (a carga é fora de tela, sem janela).
- **Esperado ao final:** quadros processados e 0 divergências de pixel em equipamento saudável.
- **Esperado em GPU defeituosa:** divergências de pixel maiores que zero, ou reinício do driver.

Resultado observado: ______________________________________________

---

## 4. Fluxo completo de bancada

1. Executar o **diagnóstico rápido** (aba Resumo).
2. Percorrer as abas Disco, Memória, CPU, GPU, Bateria, Rede e conferir se cada uma mostra dado real ou
   motivo de ausência — **nunca um valor inventado**.
3. Preencher o **Checklist**, marcando cada item após testar.
4. Gerar o **laudo** (abas Laudo) e abrir o PDF.
5. Conferir no PDF: data em dd/mm/aaaa, categoria por área, limitações, e números de série mascarados.

Resultado observado: ______________________________________________

---

## 4A. Verificações de interface (dependem de clique humano)

Os testes automatizados cobrem os comandos, e uma verificação por UI Automation confirmou que os
botões existem, estão visíveis e acionam o comando certo (ver `PROGRESS.md`). O que **falta** é o julgamento
humano: aparência, contraste e legibilidade nos dois temas, e o comportamento em telas físicas diferentes.

**Já confirmado automaticamente (não precisa repetir, mas repetir não custa):** os 11 itens do menu navegam
por acionamento real; cancelar para em 0,06 s; a janela abre em 1240×720 numa tela 1366×768 e os botões do
rodapé ficam visíveis.

**O que estes passos ainda decidem:** o desenho está correto e legível?

### 4A.1 Clicar em cada item do menu

Para **cada um dos 10 itens** (Resumo, Sistema, Disco, Memória, CPU, GPU, Bateria, Rede, Checklist, Laudo)
e também para **Configurações** (botão do rodapé):

1. Clique no item.
2. Confira: o título no topo muda para o nome da página; o conteúdo muda; **o item clicado fica destacado**
   e os outros não.

- **Esperado:** os 11 itens navegam. Se algum não fizer nada, o defeito voltou. (Confirmado por UI
  Automation: 10 de 10 categorias + Configurações.)
- **Esperado em Configurações:** o botão do rodapé fica destacado como os demais.
- **Esperado:** clicar repetidamente no mesmo item não "pisca" nem perde o foco do teclado.

Marque os que funcionaram: ______ de 11

### 4A.2 Alternar o tema 3 vezes

1. Abra uma categoria qualquer (ex.: Disco) para haver conteúdo na tela.
2. Clique em **Alternar tema** (rodapé) ou pressione **Ctrl+T**. Repita **3 vezes**.
3. A cada troca, confira visualmente: fundo, textos, cartões, **e os botões do rodapé** (tema e
   Configurações) — inclusive o texto dentro deles e a borda.
4. Abra a aba **Configurações** e confira os campos de texto.

- **Esperado:** toda a interface acompanha a troca, sem nenhum pedaço preso no tema anterior e **sem
  reiniciar o programa**.
- **Esperado após 3 trocas:** o tema voltou ao estado inicial (3 é ímpar: claro → escuro → claro → escuro,
  ou o inverso), coerente com o rótulo do botão.
- **Esperado:** o texto continua legível nos dois temas (contraste).

Resultado observado: ______________________________________________

### 4A.3 Iniciar e cancelar o diagnóstico

1. Na aba **Resumo**, clique em **Diagnóstico rápido**.
2. Assim que a barra começar a andar, clique em **Cancelar**.

- **Esperado:** a execução para **em menos de 2 segundos**. (Medido por UI Automation: 0,06 s.)
- **Esperado:** a tela mostra "Diagnóstico cancelado" e **não** mostra erro nem veredito **Falha** —
  cancelar é escolha do técnico, não defeito do equipamento.
- **Esperado:** o botão "Diagnóstico rápido" volta a ficar clicável (dá para começar de novo).

Repita uma vez deixando o diagnóstico terminar, para confirmar que sem cancelar ele conclui normalmente.

Resultado observado: ______________________________________________

### 4A.4 Redimensionar a janela

Testar em **1366×768** e em **1920×1080**, com escala do Windows em **100%** e em **150%**
(Configurações → Sistema → Tela → Escala).

1. Abra o programa e confira se a janela **cabe inteira na tela** (nada para fora, nem embaixo).
2. Confira se os botões do rodapé (tema e Configurações) estão **visíveis e clicáveis**.
3. Em cada página, confira se aparece **barra de rolagem** quando o conteúdo passa da altura da janela, e
   se o fim do conteúdo pode ser alcançado.
4. Arraste a janela para o menor tamanho permitido e repita os itens 2 e 3.

- **Esperado:** nenhum conteúdo fica cortado e nada fica inalcançável.
- **Esperado:** a roda do mouse rola a página (uma rolagem só, sem "travar").

Resultado observado por combinação:

| Resolução | Escala | Coube? | Rolagem ok? | Observação |
|---|---|---|---|---|
| 1366×768 | 100% | | | |
| 1366×768 | 150% | | | |
| 1920×1080 | 100% | | | |
| 1920×1080 | 150% | | | |

---

## 4B. Stress e benchmark de CPU

Nada aqui roda sozinho: o teste só começa quando você clica em **Iniciar stress**.

1. Abra **Stress** no menu.
2. Antes de iniciar, confirme que a página mostra `11 threads em uso` (o número varia com a máquina)
   e o gráfico com a mensagem de que não há amostra ainda.
3. Clique em **Iniciar stress**.
4. Observe durante 30 segundos:
   - A duração sobe a cada segundo (`0,0 s`, `1,0 s`, ...).
   - A temperatura da GPU começa a aparecer em °C.
   - A carga da CPU sobe para perto de 100%.
   - O botão **Parar** fica visível e **Iniciar** desaparece.
5. Clique em **Parar**. O teste termina imediatamente e aparece um resumo com a duração real, os
   ciclos concluídos e os erros de cálculo.

O que é esperado e **não** é falha:

| Situação | Por que acontece |
| --- | --- |
| "sem leitura" na CPU, com a causa escrita por extenso | O sensor do Ryzen devolve 0 ou 255, que são sentinelas. O app mostra a causa em vez de desenhar 0 °C. |
| A linha da CPU fica ausente enquanto a da GPU desenha | Mesmo motivo: só se desenha o que foi medido. |
| A faixa de alerta não aparece | Ela só aparece se a temperatura passar do limiar configurado (95 °C por padrão). |
| O teste para sozinho antes do tempo | Temperatura alta de confiança por 3 amostras consecutivas, teto de 2 min sem sensor ou teto de duração. A mensagem diz qual. |

5. Abra **Configurações**, mude a temperatura de alerta para `40`, salve e volte a **Stress**. Repita
   o teste: a faixa vermelha agora aparece assim que a GPU passa de 40 °C — e o teste **continua
   rodando** (o limiar de alerta é visual; quem para é o limite de parada, ver 4B.1). Volte para 95
   quando terminar.
6. Em **Configurações**, tente salvar a temperatura de alerta como `500`. O app precisa recusar com
   a mensagem "O limiar de alerta precisa estar entre 1 e 120 °C." e não pode gravar o valor.

> Aviso: o stress mantém o processador ocupado. Use em superfície ventilada e não deixe rodando sem
> acompanhar. O app tem limites automáticos (temperatura, teto sem sensor e duração), mas eles são
> redes de segurança, não garantia: o botão **Parar** continua sendo o controle principal.

## 4B.1 Parada automática do stress

Implementado, **aguardando validação no hardware**. Cada passo altera configuração: anote o valor
anterior antes de mexer.

- [ ] **1. Parada por temperatura.** Em **Configurações**, defina a temperatura de parada automática
      em `60` °C (valor baixo de propósito, para o teste não precisar esquentar de verdade). Salve,
      volte a **Stress**, inicie o teste e acompanhe.
      **Esperado:** o teste para sozinho e a mensagem diz que parou automaticamente, com a temperatura
      observada e o limite (formato "Parado automaticamente: X °C ≥ limite Y °C"). Se a CPU não tiver
      leitura de temperatura, este passo depende do item 4D.
- [ ] **2. Botão Parar sempre disponível.** Durante o teste do passo 1, clique em **Parar** antes de o
      limite ser atingido.
      **Esperado:** o teste encerra imediatamente com o motivo manual, sem mensagem de parada automática.
- [ ] **3. Teto sem sensor.** Remova a leitura de temperatura da CPU (ver 4D.5: driver desinstalado) e
      inicie o stress.
      **Esperado:** o app avisa que não há leitura de temperatura e que o teste é limitado a 2 minutos;
      aos 2 minutos ele para sozinho, dizendo que não há leitura de temperatura e oferecendo continuar
      só por confirmação explícita em diálogo.
- [ ] **4. Extensão registrada.** No aviso do passo 3, confirme a extensão.
      **Esperado:** o teste continua por até 10 minutos e o laudo registra que houve extensão.
- [ ] **5. Teto de duração.** Em **Configurações**, confira a duração máxima (padrão 30 min) e o aviso
      um minuto antes do fim. Testar de verdade o teto de 30 min é opcional; se testar, use a
      temperatura de parada alta para não esbarrar no passo 1.
      **Esperado:** aviso na tela um minuto antes do fim e encerramento pelo teto de duração.

Depois dos passos, **restaure** os valores originais (parada automática em 100 °C, alerta em 95 °C e a
duração que estava configurada).

Resultado observado por passo:

| Passo | Resultado | Observação |
|---|---|---|
| 1 — parada por temperatura | | |
| 2 — Parar manual | | |
| 3 — teto de 2 min sem sensor | | |
| 4 — extensão registrada | | |
| 5 — teto de duração | | |

## 4C. Benchmark de CPU

1. Abra **CPU** no menu e execute o benchmark (pelo botão da própria página).
2. Confira os dois números: "um núcleo" e "todos os núcleos".
3. O resultado de todos os núcleos deve ser **maior** que o de um núcleo. Se vier igual, menor, ou
   com aviso de instabilidade, repita com a máquina ociosa — a medição registrou interferência.

A pontuação é própria do Flavos Diag e não é comparável com a de outros programas.

---

## 4D. Sensores de temperatura — causa da CPU sem leitura

Esta seção existe por causa de um caso concreto: na máquina do dono (AMD Ryzen 5 8500G, Radeon 740M
integrada), o sensor `Core (Tctl/Tdie)` devolveu `Value = 0`, `Min = 0` e `Max = 0` — sentinela, não
medição. O app rejeita esse valor na leitura, diagnostica a causa e registra a amostra recusada
(`docs/DECISOES.md` D68, D70, D78).

### 4D.0 O que já foi apurado (24/10/2026)

Medido com o driver PawnIO instalado e em execução:

| Condição | `Core (Tctl/Tdie)` | `PawnIo.IsInstalled` |
|---|---|---|
| Aplicativo **sem** elevação | 0 (mín 0, máx 0) | `True` |
| Aplicativo **elevado** | **69 °C** (mín 68,875, máx 69,0) | `True` |

**A causa era a falta de elevação, não a ausência do driver.** Com o driver presente, mas sem
privilégio de administrador, a biblioteca devolve a sentinela 0.

O que **continua pendente**: o estresse real com a leitura funcionando. Nenhum teste de carga foi
executado automaticamente; isso exige o técnico na frente da máquina.

### 4D.1 Passo a passo no hardware do dono

Antes de começar: o driver de leitura de sensores usado pela biblioteca (PawnIO) foi **confirmado ausente
nesta máquina** (`PawnIo.IsInstalled = false`), e essa é a causa mais provável da falta de leitura. O app
**não instala** esse driver em nenhuma hipótese — a instalação é decisão e ação do dono.

- [ ] **1. Abrir como administrador.**
      Clique com o botão direito em `FlavosDiag.App.exe` → **Executar como administrador**. Abra a página
      **Stress** e a página **Resumo** e procure a temperatura da CPU.
      **Esperado:** com elevação, a temperatura da CPU aparece. Se não aparecer, uma faixa de aviso
      explica a causa — com o driver ausente, o texto diz que o driver de leitura de sensores (PawnIO)
      não está instalado. O app **não** pode dizer "requer elevação" estando elevado.
- [ ] **2. Instalar o driver pelo método oficial, se o passo 1 não trouxer leitura.**
      Antes de instalar, **confira os termos no site oficial do projeto PawnIO** (`https://pawnio.eu`).
      Depois, instale pelo site oficial ou pelo comando `winget install namazso.PawnIO -e` — copiado do
      aviso do app com o botão **Copiar** e executado por você, manualmente. Reabra o Flavos Diag como
      administrador e repita o passo 1.
      **Esperado:** a temperatura da CPU passa a aparecer em °C e a faixa de aviso some.
- [ ] **3. Exportar o diagnóstico de sensores — duas vezes.**
      Use **Configurações → "Exportar diagnóstico de sensores"** (ou `--dump-sensores` na linha de
      comando) e guarde os arquivos. Faça isso **antes e depois** do passo 2.
      **Esperado:** dois pares de arquivos `.json`/`.txt`. O primeiro diz que o PawnIO **não está
      instalado** e mostra o sensor da CPU **sem leitura**, com valor bruto, `Min`, `Max` e motivo. O
      segundo diz que o PawnIO **está instalado** e mostra o sensor com valor. Guarde os quatro arquivos.
- [ ] **4. Comparar as leituras com fontes independentes.**
      Compare com o monitor oficial da própria biblioteca de sensores e, com a máquina **em repouso**, com
      a tela de temperatura do BIOS/UEFI.
      **Esperado:** diferença de **1–2 °C** sob carga entre o Flavos Diag e o monitor da biblioteca; em
      repouso, o valor do BIOS/UEFI próximo do mostrado pelo app.
- [ ] **5. Confirmar a parada automática e o teto sem sensor.**
      Em **Configurações**, reduza o limite de parada automática para `60` °C e inicie o stress.
      **Esperado:** o teste para sozinho e a mensagem diz que parou automaticamente, com a temperatura e o
      limite. Depois **desinstale o driver** (pelo próprio Windows) e inicie o stress de novo.
      **Esperado:** o teste para aos 2 minutos dizendo que não há leitura de temperatura; continuar exige
      uma **confirmação explícita** em diálogo. Restaure o limite de parada automática em 100 °C ao final.

O que é esperado e **não** é falha:

| Situação | Por que acontece |
| --- | --- |
| "sem leitura", com a causa escrita por extenso | Sem driver ou sem elevação, o sensor devolve sentinela (0 ou 255). O app recusa na leitura e mostra a causa. |
| PawnIO aparece como "desconhecido" | A detecção não conseguiu confirmar presença nem ausência — o app informa a causa provável **sem afirmar** que é a causa. Não é o mesmo que "não instalado". |
| O veredito térmico sai **Inconclusivo** com a CPU sem temperatura | Sem temperatura de processador de confiança alta o app não aprova o comportamento térmico. Antes aprovava — e aprovava justamente quando não tinha como saber (D73). |
| O diagnóstico exportado não traz número de série | Os identificadores passam pelo mascaramento; o arquivo não contém número de série nem dado pessoal. |

> Aviso: o passo 5 reduz o limite de parada de propósito. Rode-o em superfície ventilada, acompanhe, e
> restaure os valores ao final. Os limites automáticos são redes de segurança, não garantia.

Resultado observado por passo:

| Passo | Resultado | Observação |
|---|---|---|
| 1 — temperatura com elevação | | |
| 2 — instalação do driver | | |
| 3 — exportação antes/depois | | |
| 4 — comparação com monitor oficial e BIOS | | |
| 5 — parada automática e teto de 2 min | | |

### Registro

| Data (dd/mm/aaaa) | Passo | Resultado (ok / falhou) | Observações |
|---|---|---|---|
| | 4D.1 | | |
| | 4D.2 | | |
| | 4D.3 | | |
| | 4D.4 | | |
| | 4D.5 | | |
| | 4B.1.1 | | |
| | 4B.1.3 | | |
| | | | |

## 5. Verificações de honestidade

Estas não dependem de hardware. São conferências de que o programa não "finge".

| # | Verificação | Como conferir |
|---|---|---|
| H1 | Sem dados fictícios fora do modo demonstração | Só `tests/` e `--modo-demo` contêm dados fictícios. `build/cleanroom.ps1` verifica. |
| H2 | Nenhum botão sem efeito | Cada botão das telas chama um comando que executa ação real ou mostra erro com motivo. |
| H3 | Ausência nunca vira OK | Todo item sem leitura aparece como "Inconclusivo" ou "Não disponível" com causa. |
| H4 | Nenhuma telemetria | O programa não abre conexão para fora, exceto para os alvos que o técnico configurar. |
| H5 | Pontuação não comparável | Todo número de benchmark vem acompanhado do aviso de pontuação própria. |

Resultado observado: ______________________________________________

---

## 6. O que fazer se algo falhar

1. **Não maquiar o resultado.** Anotar exatamente o que apareceu.
2. Verificar se a falha é de permissão (tentar com administrador).
3. Se for defeito do programa, registrar em `PROGRESS.md` com o texto do erro.
4. Se o mesmo problema se repetir por 3 tentativas de correção, marcar `[!] BLOQUEADO: motivo` em
   `PROGRESS.md` em vez de contornar com valor inventado.

---

## 7. Resumo do que está pendente

| Item | Situação |
|---|---|
| Atributos SMART ATA em hardware | não verificado (controlador recusou o comando) |
| Disco NVMe | não verificado (sem NVMe na máquina) |
| Bateria de notebook | não verificado (desktop, sem bateria) |
| Sensores de temperatura com elevação | não verificado (sem elevação) |
| Causa da CPU sem leitura e instalação do PawnIO | implementado, aguardando validação no hardware — manual (item 4D) |
| Exportação do diagnóstico de sensores | implementado, aguardando validação no hardware — manual (item 4D.3) |
| Comparação com monitor oficial da biblioteca e BIOS/UEFI | implementado, aguardando validação no hardware — manual (item 4D.4) |
| Parada automática por temperatura (limite de 60 °C) | implementado, aguardando validação no hardware — manual (item 4D.5 / 4B.1) |
| Teto de 2 min sem sensor e extensão confirmada | implementado, aguardando validação no hardware — manual (item 4D.5 / 4B.1) |
| Compatibilidade do driver com Isolamento de núcleo / antivírus | não verificado (depende da máquina; ver `RISCOS-LEGAIS.md` R12) |
| Temperatura da GPU com elevação | não verificado (sem elevação) |
| Detecção de artefato em GPU defeituosa | não verificado (não há GPU defeituosa) |
| Reinício de driver (TDR) real | não verificado (não ocorreu) |
| Estresse de CPU e teste de memória em hardware real | não verificado (proibido executar autonomamente) |
| Instalação em máquina limpa sem .NET | não verificado (o pacote roda nesta máquina) |
| SmartScreen / antivírus | não verificado (executável não assinado) |
| Clique real em cada item do menu (11 itens) | comandos cobertos por teste automatizado; o clique na tela é manual (item 4A.1) |
| Troca de tema vista a olho nu | chaves de cor cobertas por teste automatizado; o visual é manual (item 4A.2) |
| Cancelamento do diagnóstico pela tela | comando coberto por teste automatizado; o clique é manual (item 4A.3) |
| Janela em 1366×768 / 1920×1080 a 100% e 150% | depende da tela física; manual (item 4A.4) |
| Gráfico de temperatura ao vivo | desenho verificado por teste de renderização por pixel; a corrida térmica real é manual (item 4B) |
| Parada do stress pelo técnico | executada por UI Automation nesta máquina (5,9 s, 2.616 ciclos); o clique humano é manual (item 4B) |
| Aparência do gráfico (cores, legibilidade) | a olho nu é manual (item 4B) |
