---
name: flavos-diag-safety-rules
description: Regras de segurança do Flavos Diag, tanto para o equipamento do cliente (somente leitura, estresse com limites, nunca escrever em setores) quanto para o próprio aplicativo (execução de processos, caminhos de arquivo, privilégios de administrador, segredos, dados pessoais, dependências e assinatura). Use sempre que for implementar ou revisar teste de estresse, varredura ou benchmark de disco, chamada a smartctl ou powercfg, leitura de sensores, elevação de administrador, exportação de laudo com número de série, adição de dependência NuGet ou qualquer código que escreva em disco ou inicie processos.
---

# Regras de segurança do Flavos Diag

O aplicativo roda na máquina de um cliente, muitas vezes com hardware já defeituoso. O pior resultado possível é piorar o equipamento ou perder os dados dele. Por isso a regra de ouro é: **observar, não alterar**. O que não puder ser feito de forma reversível e com confirmação não entra.

## 1. Equipamento do cliente

**Somente leitura por padrão.** Não altere registro, serviços, configurações do Windows nem arquivos do usuário. A única escrita permitida são arquivos temporários próprios (benchmark de disco), em pasta controlada, com checagem de espaço livre antes e remoção ao final, inclusive em caso de erro ou cancelamento (`try/finally`).

**Nunca escrever em setores** para "consertar". O app detecta, registra e recomenda. Em SSD, o controlador já gerencia realocação, e em HDD defeituoso a escrita direta pode agravar a falha. A única verificação de sistema de arquivos permitida é a varredura online em modo somente leitura do Windows.

**Varredura de superfície**: somente leitura, cancelável, precedida por aviso exato: "Faça backup antes. Discos com defeito podem piorar durante a leitura." Ler um disco em estado crítico também o estressa; ofereça interromper ao detectar lentidão extrema ou erros de leitura repetidos.

**Teste de estresse** (CPU, GPU):
- Confirmação explícita antes de iniciar, mostrando duração e limites.
- Limite de temperatura configurável com parada automática (padrão CPU 90 °C, GPU 85 °C).
- Duração máxima obrigatória; botão de parada sempre visível e que funciona mesmo com a interface ocupada (a carga roda em threads de trabalho, nunca na thread da interface).
- Sem leitura de temperatura: limitar a 2 minutos e avisar.
- Registrar o motivo da parada (tempo, temperatura, usuário, erro). Encerrar também se o app fechar, o notebook entrar em modo de economia crítica ou a bateria cair abaixo de um limite sem o carregador.
- Em notebook, exigir carregador conectado para estresse.

**Proibido**: otimizador de registro, "otimização de memória", limpeza de USB, instalação automática de drivers, qualquer mudança de configuração. Ações que reiniciam o sistema (ex.: diagnóstico de memória nativo) só com confirmação clara.

## 2. Segurança do próprio aplicativo

**Processos externos (smartctl, powercfg, ping):**
- `ProcessStartInfo` com `UseShellExecute = false` e `ArgumentList` (nunca concatenar string de comando). Nada vindo da interface entra no comando sem validação por lista de permitidos.
- Caminho do executável fixo e verificado (pasta do app ou `System32`), nunca resolvido pelo PATH.
- Timeout obrigatório com `Kill(entireProcessTree: true)`, saída limitada em tamanho, código de saída tratado.
- Executáveis empacotados: conferir hash (SHA-256) registrado em `docs/LICENCAS.md` antes de executar.

**Arquivos e caminhos:**
- Normalizar com `Path.GetFullPath` e confirmar que o destino está dentro da pasta escolhida (evita `..\`). Nome de arquivo de laudo derivado de entrada do usuário passa por sanitização.
- Nunca seguir links simbólicos ao varrer ou apagar.

**Dados e privacidade:**
- Sem telemetria e sem rede por padrão. Teste de rede usa apenas o servidor configurado pelo usuário.
- Números de série, MAC e nome do usuário são dados pessoais ou identificáveis: oferecer mascaramento antes de exportar (ex.: manter só os 4 últimos caracteres) e mascarar nos logs.
- Logs em arquivo, sem conteúdo sensível, com rotação e limite de tamanho.
- Segredos (se um dia existirem) fora do código e fora do repositório; use DPAPI (`ProtectedData`) para o que precisar persistir localmente.

**Privilégios:**
- Manifesto `asInvoker`. O app funciona sem administrador em modo reduzido e declara claramente o que ficou "Inconclusivo: exige administrador". Eleve apenas quando o usuário ativar sensores ou SMART, reiniciando o app com `runas` e explicando por quê.
- Bibliotecas de sensores podem carregar driver de kernel. Registre isso em `docs/RISCOS-LEGAIS.md` e na documentação, e prefira versões mantidas ativamente.

**Dependências e cadeia de suprimentos:**
- Versões fixas (sem `*`), `packages.lock.json` com restauração bloqueada (`RestorePackagesWithLockFile` e `--locked-mode` na CI).
- Auditoria de vulnerabilidades em todo fechamento de fase: `dotnet list package --vulnerable --include-transitive` e o NuGet Audit do SDK (`NuGetAudit` ligado, `NuGetAuditMode=all`). Achado de severidade alta ou crítica em dependência usada vira BLOQUEADO até haver correção.
- Cada dependência nova: licença confirmada no repositório oficial, mantenedor ativo, sem etapa de instalação suspeita. Em dúvida, não adicione.
- Binários de terceiros empacotados (como smartctl) vêm do site oficial do projeto, com hash conferido e licença incluída. Não use pacotes "tudo em um" de origem duvidosa.

**Distribuição:**
- Publicar self-contained. Sem assinatura de código, o SmartScreen e alguns antivírus alertam; documente isso e, quando possível, assine. Nunca desative o antivírus do cliente nem peça isso ao usuário.

## 3. Regra para "dado ausente"
Sem leitura, sem permissão ou erro = **Inconclusivo** com a causa. Nunca assuma OK. Isso é uma regra de segurança também: um laudo que diz "disco saudável" sem ter lido o SMART leva o cliente a confiar em um disco que pode estar morrendo.

## 4. Revisão rápida antes de cada commit
- [ ] Algum código novo escreve fora de pasta temporária própria? Se sim, remover.
- [ ] Algum processo novo usa `ArgumentList`, caminho fixo, timeout e limite de saída?
- [ ] Estresse novo tem confirmação, limite de temperatura, limite de tempo e parada?
- [ ] Algum dado identificável novo foi para log ou laudo sem mascaramento opcional?
- [ ] Auditoria de pacotes executada e sem achados altos ou críticos?

Estas regras refletem boas práticas gerais e devem ser revisadas de tempos em tempos (versões de .NET, NuGet e bibliotecas mudam). Confira a documentação oficial atual antes de depender de um detalhe específico.
