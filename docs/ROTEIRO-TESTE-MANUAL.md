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

- **Esperado:** para sozinho ao atingir 90 °C, ou em 2 min se não houver sensor; registra o motivo.
- **Nunca deve:** continuar com temperatura acima do limite; travar a interface.

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
| Temperatura da GPU com elevação | não verificado (sem elevação) |
| Detecção de artefato em GPU defeituosa | não verificado (não há GPU defeituosa) |
| Reinício de driver (TDR) real | não verificado (não ocorreu) |
| Estresse de CPU e teste de memória em hardware real | não verificado (proibido executar autonomamente) |
| Instalação em máquina limpa sem .NET | não verificado (o pacote roda nesta máquina) |
| SmartScreen / antivírus | não verificado (executável não assinado) |
