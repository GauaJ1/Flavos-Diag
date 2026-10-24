# Flavos Diag — Decisões de projeto

> Toda dúvida resolvida sem perguntar ao usuário é registrada aqui: **D<nº> — decisão — motivo — impacto**.

| # | Decisão | Motivo | Impacto |
|---|---|---|---|
| D1 | Projeto em `Flavos-Diag/` separado do sistema web | Briefing ("projeto separado"). | Isolamento total; integração por arquivo. |
| D2 | Dispositivo com erro no Gerenciador: mostrar código + instrução própria, **sem link de fabricante** | Um link exige base de fabricantes mantida e atualizada; sem base, o link seria falso/de enfeite (viola B1). | A recomendação diz "procurar o suporte do fabricante do equipamento" (sem link). Pode virar D2b com base local se o dono mantiver. |
| D3 | Timings de memória (CAS/RAS/CL) exibidos como "Não disponível" | Nenhuma fonte confiável com licença compatível foi confirmada; ler SPD exige driver/admin e decodificação de padrões que merecem revisão de procedência. | Campo presente no laudo como `null` + limitação explicada. |
| D4 | Sensores sem admin: dependem da biblioteca LHM; se não houver acesso ao driver, resultado Inconclusivo | Sem instalar driver por conta própria (A2, C). | Estresse sem sensor fica limitado a 2 min. |
| D5 | smartctl **não** empacotado no MVP: SMART nativo + fallback Windows | Empacotar GPL exige processo separado, texto GPL completo e oferta de código; tem risco de antivírus. O caminho nativo entrega a função. | Se a leitura nativa falhar em disco no teste real, reavaliar (doc em PROGRESS). |
| D6 | `AMBIENTE: banco WMI/Fixtures` — fixtures de teste com dados fictícios ficam **somente** em `tests/`, nunca em `src/` | Impede que dados inventados cheguem à UI por acidente (B1). | Projeto com fakes apenas no projeto de testes/Providers.Fakes? — Decisão: fakes em `FlavosDiag.Providers.Fakes` (biblioteca separada), referenciada só por testes e pelo app quando `--modo-demo` é passado explicitamente com aviso visível. |
| D7 | App aceita `--modo-demo` para demonstração de interface | Permite testar UI sem hardware; precisa ser explicitamente passado. | A janela mostra faixa "Modo demonstração — dados fictícios" que **não** pode ser removida sem reiniciar. |
| D8 | Cores: verde-petróleo `#0F766E` como destaque; feedback semântico: OK `#15803D`, Atenção `#B45309`, Falha `#B91C1C`, Inconclusivo `#475569` (cinza) | Contraste AA em fundos claro/escuro. Inconclusivo cinza, não amarelo, para não parecer "quase OK". | Documentado em DESIGN.md. |
| D9 | Fonte: Geist se licença OFL confirmada (tem: SIL OFL 1.1), senão Inter (OFL). | Briefing pede Geist "se a licença permitir". | Empacotar OFL exige incluir o texto da licença. Verificado na Fase 5. |
| D10 | Não usar `PublishSingleFile` com WPF até validação; entrega em pasta | Mais confiável para WPF + D3D + DLL nativa do monitor de hardware. | Pasta portátil com `FlavosDiag.App.exe`. |
| D11 | Dados da bateria: parse do XML do `powercfg` (schema documentado) e `Win32_Battery` como complemento | XML dá capacidades e ciclos que o WMI não fornece de forma confiável. | Fixtures em `tests/fixtures/battery/`. |
| D12 | Teste de rede: N=20 pacotes ICMP, timeout 1 s; fallback TCP na porta configurada (padrão vazia) | Redes corporativas bloqueiam ICMP. | Se ambos falharem, rede = Inconclusivo. |
| D13 | Configurações em `%LOCALAPPDATA%\FlavosDiag\config.json` com escrita atômica (arquivo temporário + rename) | Evita configuração corrompida. | — |
| D14 | Histórico por máquina: chave = fabricante+modelo+serial da placa-mãe (mascarado) hasheado SHA-256 | Evita conflito e reduz exposição do serial. | Hash não é dado pessoal reversível, mas documentado em RISCOS. |
| D15 | Diagnóstico rápido: alvo 2–4 min com timeout por etapa; sensores 60 s | Briefing E11. | Mostra "etapa atual" e permite cancelar. |
| D16 | Ícones: desenhados como `Path` no XAML, traço 1,5 px, 20×20 | Sem dependência de pacote de ícones (licença + origem). | Todos os ícones são do projeto. |
| D17 | Estresse: parada por temperatura com 2 amostras consecutivas ≥ limite, com histerese | Evita parada por pico isolado. | Motivo registrado "Temperatura". |
| D18 | Throttling: comparação de mediana de clock segunda metade × primeira metade, ≥10 % | Heurística simples e explicável. | Limitação no laudo. |
| D19 | Pontuação benchmark = unidades de trabalho normalizadas; versão do algoritmo no laudo | Comparabilidade interna e reprodutibilidade. | Histórico grava versão; comparação só entre mesmas versões. |
| D20 | GPU: D3D11 com shader compilado em build via `Effect`? — usar D3DCompiler (`d3dcompiler_47.dll` do Windows) para compilar HLSL em runtime, evitando build tools extras | HLSL compilado em runtime com a DLL de sistema do Windows; sem `fxc` no CI. | Se DLL ausente, GPU = Inconclusivo. |
| D21 | Estrutura de testes: xUnit 2.9.3 + `Microsoft.NET.Test.Sdk` 17.x | Estável e MIT/Apache no dev-only. | — |
| D22 | Laudo JSON: `System.Text.Json` com `JsonSerializerOptions` camelCase, `JsonIgnoreCondition.WhenWritingNull` | Tamanho e legibilidade; campos ausentes explícitos (nunca null em campos essenciais: usamos `Leitura` serializada com estado). | `SCHEMA-LAUDO.md`. |
| D23 | Sensores CSV: separador `;` e UTF-8 com BOM (Excel pt-BR abre corretamente) | Usabilidade do técnico. | Data/hora ISO 8601 no CSV. |
| D24 | Elevação: `ShellExecute` com `runas`; nunca altera serviços/drivers; se o usuário recusar, segue em modo reduzido | C. | Mensagem clara. |
| D25 | App sem "instalar" nada: distribuição em ZIP/pasta | Portátil. | README. |
| D26 | Skills do projeto instaladas em `.skills/` (fora de `.claude/`) e carregadas manualmente | O agente desta sessão lê as skills por caminho; `.skills/` não colide com a pasta de outro assistente e não é removida por ferramentas que gerenciam `.claude/`. | Cópia versionada junto do projeto; a origem é o pacote `flavos-diag-skills.zip` entregue pelo dono. |
| D27 | Verificador de clean room portado para PowerShell (`build/cleanroom.ps1`) | Python não está instalado nesta máquina; o porte mantém a mesma lista de termos proibidos, as mesmas pastas varridas e a mesma lista de exceções. | O script `.py` original permanece em `.skills/` como referência. Os padrões do original foram escritos de forma fragmentada para que o próprio código do verificador não dispare um achado ao ser varrido. |
| D28 | `docs/RISCOS-LEGAIS.md` é a única exceção do verificador (`cleanroom_allow.txt`) | É o documento que precisa listar nominalmente as proibições. | Qualquer exceção nova exige linha aqui. |
| D29 | PDF do laudo usa a fonte Geist embutida como recurso do assembly de relatórios | A partir da versão 2026.9 o QuestPDF deixou de usar fontes do sistema e aborta a geração quando a família não está registrada; embutir a fonte garante o mesmo resultado em qualquer máquina. | Fonte SIL OFL 1.1 com texto de licença empacotado; ver LICENCAS.md. |
| D30 | Identificação de disco vem do WMI (`Win32_DiskDrive` + `MSFT_PhysicalDisk`), não dos IOCTL de descritor | Verificado nesta máquina: os IOCTL de descritor devolvem "acesso negado" sem elevação, e com elevação o WMI é igualmente suficiente. Assim o técnico sem administrador continua vendo modelo, série, barramento e tipo de mídia; só a saúde fica Inconclusiva. | Fonte real por campo no laudo (`STORAGE_DEVICE_DESCRIPTOR` vs `MSFT_PhysicalDisk.MediaType`). |
| D31 | Leitura SMART ATA exige abrir o disco com `GENERIC_WRITE` | Verificado empiricamente nesta máquina: com `GENERIC_READ` o IOCTL de pass-through devolve `ERROR_ACCESS_DENIED` (5) mesmo elevado; com `GENERIC_WRITE` é aceito. O comando enviado é **SMART READ DATA** (0xD0), somente leitura de dados; nenhum setor é escrito. | Registrado em comentário no código e em `docs/RISCOS-LEGAIS.md`. Não é contornar permissão: exige execução como administrador, como a skill manda. |
| D32 | Sem SMART acessível, o veredito usa a previsão de falha do Windows e o `HealthStatus` da API de armazenamento | São leituras reais do Windows, disponíveis sem elevação. Quando nenhuma responde, o disco fica Inconclusivo com a causa. | Nesta máquina os atributos SMART são recusados pelo controlador (status 0x51/erro 0x04 mesmo elevado); o `HealthStatus` responde. Mascarar isso como "saudável" seria falso. |
| D33 | Bateria: presença exige ao menos uma bateria no relatório ou no WMI | Verificado nesta máquina: o `powercfg` gera o XML com `<Batteries></Batteries>` vazio em desktop, então "o arquivo existe" não prova que há bateria. Desktop sem bateria é **Não se aplica**. | Antes do ajuste, o roteiro reportou "presente: sim" numa máquina sem bateria — falso positivo corrigido. |
| D34 | Rede: o adaptador "principal" é o que tem rota padrão | Verificado nesta máquina: havia 8 adaptadores (túneis de VPN, Npcap, Wi-Fi Direct) e o primeiro ativo com IP não tinha gateway; a latência do gateway saía vazia mesmo com internet. Priorizar gateway resolve sem inventar dado. | Wi-Fi consultado com caminho completo do Windows PowerShell (`System32\WindowsPowerShell\v1.0`), porque o `powershell.exe` não fica em `System32`. |
| D35 | Versões de pacote travadas em `packages.lock.json` por projeto, com `--locked-mode` na restauração | A skill de segurança exige cadeia de suprimentos determinística; sem o lock, uma versão nova publicada no NuGet entra sem revisão. | `RestorePackagesWithLockFile`, `NuGetAudit` e `NuGetAuditMode=all` no `Directory.Build.props`; `build/test.ps1` restaura travado antes de compilar. Os 6 arquivos de lock são versionados e cada um guarda o hash de conteúdo de cada pacote. |
| D36 | `build/test.ps1` resolve o `dotnet` por conta própria | Verificado: o script falhava com "No .NET SDKs were found" quando chamado de um shell sem a instalação por usuário no PATH — o script precisa funcionar de qualquer terminal, inclusive na máquina do dono. | Procura no PATH, em `%LOCALAPPDATA%\Microsoft\dotnet` e em `%ProgramFiles%\dotnet`, e falha com mensagem clara se não achar nenhum com SDK. |
| D37 | Teste de memória usa `GlobalMemoryStatusEx`/`ullAvailPhys`, não `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` | O segundo é um limite de política do coletor de lixo, não a memória física livre: superestima o que há de fato e pode fazer o PC do cliente paginar. | Fração 50 %, teto 2 GiB, piso 256 MiB (P3-09a). Memória livre injetável por construtor para os testes não dependerem da máquina. |
| D38 | Teste rápido de memória sem erro fica **Inconclusivo**, nunca OK | Um teste de segundos em um buffer não sustenta a afirmação "a memória está boa". A skill de vereditos manda tratar teste incompleto como Inconclusivo. | Uma divergência → Atenção (repetir); divergências em padrões distintos → Falha. Regra em `REGRAS-VEREDITO.md` 3.2.1. |
| D39 | Sensores e estresse não foram executados em hardware real durante o desenvolvimento | Regra C: estresse só com sensores fake no desenvolvimento autônomo, para não arriscar a máquina do dono. | A verificação real fica no `ROTEIRO-TESTE-MANUAL.md` da Fase 5 e está declarada como não verificada no PROGRESS.md. |
| D40 | GPU usa Vortice (MIT) em vez de P/Invoke escrito à mão | A primeira tentativa foi uma tabela de vtables COM escrita à mão; é frágil e cara de manter. Vortice.Direct3D11/DXGI/D3DCompiler 3.8.3 são MIT, maduras (2,2 mi+ downloads) e geradas a partir dos cabeçalhos oficiais. Registrado em `LICENCAS.md`. |
| D41 | `Compiler.Compile` recebe (fonte, entrada, **nomeDoArquivo**, **profile**) | A ordem é essa; a documentação de exemplo do projeto engana. Errar a ordem produz `error X3506: unrecognized compiler target`, que mascara o problema real. |
| D42 | `Vortice.DirectX` é referência explícita, não só transitiva | Verificado: sem a referência o app compila e falha em execução com `FileNotFoundException`. |
| D43 | Shader da GPU é determinístico; o número do quadro não entra no cálculo | A primeira execução real acusou 806 divergências em 806 quadros — o shader variava por projeto. Qualquer GPU seria reprovada. Após corrigir: 0 divergências em 794 quadros na mesma máquina. |
| D44 | O provider de GPU propaga a causa exata da falha | Antes devolvia "não determinado" sem motivo, violando a regra de dado ausente com causa. Agora `ResultadoGpu.CausaAusencia` carrega o texto e a categoria fica Inconclusivo. |
| D45 | `AllowUnsafeBlocks` só em `FlavosDiag.Providers` | Necessário apenas para copiar os pixels mapeados do Direct3D. Fica restrito a um projeto e o código não seguro se limita ao laço de cópia do buffer. |
| D46 | Rasterizador de software como reserva, registrado como limitação | Em máquina virtual sem aceleração, a prova ainda roda (mais lenta) em vez de virar Inconclusivo. Nesta máquina o caminho usado foi a GPU de hardware (AMD Radeon 740M). |
| D47 | Comando do menu captura a página; `ItensNav` é montada uma vez no construtor | O `Button` do `DataTemplate` não passava `CommandParameter` e o `RelayCommand<PaginaBase>` recebia `null`: nenhum item do menu navegava. Como comando genérico exige que o XAML coopere, cada item passa a ter um `RelayCommand` sem parâmetro que já conhece a sua página. Recriar a coleção a cada navegação reconstruía os botões e perdia o foco do teclado; agora o destaque é atualizado in loco em `OnPaginaAtualChanged`. |
| D48 | Configurações entra no destaque do menu, mas fora do `ItemsControl` | O botão fica no rodapé, onde não recebe o `DataTrigger` dos itens de categoria. Um conversor `SelecaoParaFundo` devolve o pincel do tema atual, para o destaque não depender de `StaticResource`. |
| D49 | `IncludeCancelCommand` no diagnóstico rápido, separado do comando de iniciar | O botão Cancelar estava ligado ao próprio comando de iniciar, que se recusava a rodar de novo (`if (Executando) return;`): não cancelava nada. Cancelar registra "cancelado" com veredito Inconclusivo, nunca Falha — interromper é escolha do técnico, não defeito do equipamento. |
| D50 | Tema escuro é uma sobreposição **apenas de cores** | Estilos duplicados no dicionário escuro congelam as cores do tema claro no momento da carga. Com `StaticResource` em Tema.xaml, os botões do rodapé ficavam no tema antigo depois da troca. Todos os 32 usos de cor foram para `DynamicResource` e o dicionário escuro ficou só com cores. |
| D51 | A chave `FlavosTextoNoDestaque` existe porque o tema escuro clareia o destaque | O texto do botão primário era `White` fixo; no escuro o verde-petróleo fica claro e o branco perderia contraste. A tinta sobre o destaque passou a ser um token de tema. |
| D52 | `App.AplicarTema` usa URI com nome de assembly | A forma `Theme/TemaEscuro.xaml` só resolve quando quem carrega é o próprio assembly do aplicativo; com `pack://application:,,,/FlavosDiag.App;component/...` a troca de tema funciona também a partir de código de teste. |
| D53 | Regra de faixa de temperatura mora no Core (`FaixaTemperatura`) | Tanto o veredito (`Regras`) quanto os coletores (`Providers`) precisam dela, e o Core não pode depender dos Providers. Uma definição só evita as duas camadas divergirem. |
| D54 | Atributo SMART de temperatura inválido continua listado, marcado `LeituraIndisponivel` | Esconder o atributo esconderia que o disco o reportou; transformá-lo em leitura geraria "255 °C". Listado e marcado, o técnico vê que existe e que não vale, e o avaliador não emite achado. |
| D55 | `LogSaudeNvme.TemperaturaCelsius` virou `int?` | Devolver `0` quando o campo não foi preenchido é a forma mais fácil de inventar uma medição. Nulo obriga quem consome a tratar a ausência. |
| D56 | Inventário de memória não é teste: sem teste o veredito é Inconclusivo | Ler a quantidade de memória diz o que está instalado, não que os módulos retêm dados. Um teste rápido limpo também não prova nada. Falha só com 2 ou mais execuções com erro; 1 erro vira Atenção para repetir. |
| D57 | Não foram aninhados dois `ScrollViewer` | Cada página já tem o seu. Um segundo quebraria a roda do mouse e a barra de rolagem. O que evita corte é a janela caber na tela, o que se resolve limitando o tamanho ao `WorkArea`. |
| D58 | `ResultadoTesteMemoria.ExecucoesComErro` tem padrão 1 | Chamadores já existentes só criavam o resultado quando havia erro; o padrão 1 preserva esse significado e não muda comportamento antigo. |
| D59 | Benchmark versão 2: mesma carga nos dois modos | Na versão 1 o modo multi somava ciclos de quatro cargas com pesos muito diferentes (matriz 192, crivo 2.000.000, hash 262.144, ponto flutuante 131.072), enquanto o single media só a primeira. Os dois números não mediam a mesma coisa e a relação "single × núcleos ≈ multi" nunca fechava. Agora ambos usam a mesma carga e a mesma unidade. |
| D60 | A escala da pontuação: 1 ponto = 1000 operações/s por thread | O código já dividia por `OperacoesPorPonto = 1000`; o comentário e o teste escritos nesta rodada afirmavam "1000 pontos", que estava errado. O comportamento foi preservado e a documentação corrigida — mudar a constante para casar com um comentário errado teria mudado todas as pontuações já gravadas. |
| D61 | `EficiênciaEscala` é nula quando não há medição válida | Dividir por zero daria `Infinity` na tela. Sem single, sem multi ou com uma só thread, a eficiência é `null` e a interface mostra a causa. |
| D62 | Gráfico desenhado à mão, sem biblioteca de gráficos | O app é offline e de um único executável; uma dependência de gráficos pesaria mais que o problema. Um `FrameworkElement` com `OnRender` desenha grade, faixas e curvas em poucas centenas de linhas. |
| D63 | Eixo Y fixo em 20–110 °C, nunca a partir de zero | Uma escala de 0 a 110 comprime a variação útil de 40 a 90 °C num terço da altura e faz qualquer curva parecer plana. Abaixo de 20 °C não há leitura plausível de componente em operação. |
| D64 | Buraco na série quebra a linha em vez de interpolar | Ligar dois pontos através de um período sem leitura inventaria uma temperatura que não foi medida. A linha para e recomeça, e a causa aparece escrita na tela. |
| D65 | O stress não tem limite que o encerre sozinho | Um sensor que devolve sentinela (0 ou 255) abortaria um teste saudável, e o técnico perderia a corrida térmica. O limiar de alerta (padrão 95 °C, configurável) apenas destaca a faixa no gráfico; quem decide parar é o técnico. O único encerramento automático é o teto de 4 h, que existe para não deixar carga esquecida. |
| D66 | O coletor passou a habilitar a GPU (`IsGpuEnabled`) | Estava desligado porque a GPU era tratada pela prova de vídeo. Mas a tela de stress promete temperatura da GPU, e o sensor respondia 48 °C enquanto a tela dizia "sem leitura". Um teste lê o fonte para travar essa configuração, porque abrir a sessão real exigiria driver e elevação. |
| D67 | Amostragem de 1 s num `DispatcherTimer` | O gráfico precisa de resolução suficiente para mostrar a subida térmica e não transformar 4 h de teste em 14 400 pontos. Uma amostra por segundo é o intervalo em que a curva ainda é legível. |

## Entradas detalhadas

## D68 — A sentinela é tratada na origem, não na tela

O sensor `Core (Tctl/Tdie)` do processador, na máquina do dono (Ryzen 5 8500G), devolveu `Value = 0`, `Min = 0` e `Max = 0` — inclusive com carga real sobre o processador. Até aqui o aplicativo descartava apenas leitura `null` ou NaN. Um `0` numérico passava adiante, e um zero que atravessa o pipeline vira gráfico desenhado em 0 °C, laudo com "0 °C" e veredito tomado sobre uma temperatura que nunca foi medida: a mentira mais cara que este programa pode contar, porque é silenciosa e tem cara de medição.

A decisão é tratar a sentinela na origem. `TemperaturaFisica`, no Core, passou a ser a **única** regra que decide se um número é temperatura utilizável. Ficam classificados como *sem leitura*: `0`; `255`; qualquer valor fora de 1–120 °C; e qualquer sensor cujo `Min` seja igual ao `Max` — um sensor que nunca variou não está medindo, está repetindo uma constante. O provider registra essas amostras numa lista própria, `AmostraSensores.SemLeitura`, com tipo, nome do hardware, nome do sensor, valor bruto, `Min`, `Max` e o motivo escrito em pt-BR.

O raciocínio é simples: uma leitura ausente que carrega a causa visível é informação útil ao técnico; um zero falso é informação ativamente enganosa. O valor bruto e o par `Min`/`Max` ficam preservados justamente para que seja possível provar depois que aquilo era sentinela e não medição.

Para que a amostra chegue à tela sabendo de onde veio, `ValorSensor` ganhou `TipoHardware` (`Cpu`, `Gpu`, `Placa`, `SuperIo`, `Memoria`, `Armazenamento`, `Outro`), `NomeHardware`, `NomeSensor` e `Qualidade` (`Ok`, `SemLeitura`, `Suspeito`). Todos têm valor padrão, para que amostras antigas já serializadas continuem desserializando sem erro.

## D69 — A escolha da temperatura é por tipo de hardware, não por texto do rótulo

O classificador anterior procurava o sensor da CPU com `Contains` sobre o rótulo. Isso funciona por coincidência enquanto os nomes não colidem — e colidem: a GPU também expõe um sensor chamado `Core`. Com a busca por texto, um sensor de placa-mãe ou da GPU podia ser lido como a temperatura do processador, e a partir daí todo o resto (gráfico, alerta, parada, veredito) herdava o erro sem nenhum sinal de que algo estava errado.

`ClassificadorTemperatura` agora filtra em três etapas: primeiro por `TipoHardware == Cpu` (ou `Gpu`, conforme o alvo); depois por preferência de nome (`Core (Tctl/Tdie)`, `CPU Package`, …) dentro do tipo já filtrado; e por fim rejeita o que não passar em `TemperaturaFisica.Valida`. O nome passou a ser critério de desempate **depois** da origem, não de identificação.

`CargaCpuRunner.TemperaturaDaCpu` tinha um defeito do mesmo tipo: tomava o máximo de qualquer temperatura acima de zero, incluindo a do SuperIO da placa-mãe. A consequência é grave porque é justamente a proteção que deixava de proteger — com a placa a 40 °C e o processador superaquecendo, o limite nunca era atingido e a carga continuava. Agora só conta o sensor do próprio processador. Sem ele, a temperatura é ausente, e ausência é tratada como ausência.

## D70 — A causa da falta de leitura é diagnosticada, não adivinhada

`DiagnosticoSensores`, no Core, recebe fatos por meio de records — se o processo está elevado, se o PawnIO está presente, ausente ou desconhecido, quantos sensores térmicos existem e quantos têm leitura — em vez de consultar o Windows diretamente. Assim a regra é testável em qualquer máquina e não depende de hardware ou de elevação para ser exercitada.

As regras são avaliadas nesta ordem estrita, e a ordem importa:

a) **não elevado** → pedir ao usuário que reabra como administrador;
b) **elevado, há sensores sem leitura, PawnIO ausente** → "O driver de leitura de sensores (PawnIO) não está instalado";
c) **elevado, PawnIO presente e ainda sem leitura** → a biblioteca reconheceu o processador mas não conseguiu lê-lo; sugerir atualizar a biblioteca ou exportar o diagnóstico;
d) **PawnIO desconhecido** → informar a causa provável **sem afirmar** que ela é a causa.

O defeito que isso corrige é concreto: o aplicativo dizia "requer elevação" mesmo quando já estava elevado. Diante de um problema de driver, mandava o técnico repetir a única coisa que ele já tinha feito, e a causa verdadeira ficava invisível. Cada regra agora só dispara quando os fatos que a sustentam estão presentes; quando o fato falta, o texto diz que falta.

## D71 — O aplicativo nunca instala driver

Diante da causa "driver ausente", o aplicativo mostra a causa e um comando sugerido (`winget install namazso.PawnIO -e`) com um botão **Copiar** — e só. Ele não executa o comando, não se eleva sozinho e não baixa nada. A razão não é técnica: um driver de kernel é uma decisão de segurança do dono da máquina, não do programa de diagnóstico. Quem responde por um driver carregado no kernel é quem o instalou, e essa pessoa precisa consultar o site oficial do projeto antes.

Por isso o comando só aparece quando instalar o driver é uma causa possível de fato. Sugerir instalação de driver para quem tem o sensor funcionando, ou para quem nem está elevado, seria empurrar uma mudança de sistema como solução para um problema que não é esse.

Registro do estado atual: na máquina do dono o driver foi confirmado ausente (`PawnIo.IsInstalled = false`), e essa é a causa mais provável da temperatura de CPU sem leitura. Ainda assim o caso permanece **implementado, aguardando validação no hardware** — confirmar a ausência do driver não é o mesmo que confirmar que a sua instalação resolve a leitura.

## D72 — A decisão sobre parada automática foi revista (substitui a D65)

A D65 removeu a parada automática por temperatura, com o argumento de que o técnico deve estar sempre no comando e que uma sentinela (0 ou 255) abortaria um teste saudável. A decisão foi revista, e o motivo é o que a D65 não pesou.

O código naquele estado usava `LimiteTemperaturaCelsius = double.MaxValue` e `DuracaoMaxima = 4 h`. Na prática: se o processador não tivesse nenhuma leitura de temperatura, o teste rodaria em carga máxima por até quatro horas **sem proteção alguma**. A "escolha do técnico" era na verdade ausência de teto, num caso em que o técnico nem sabe que está sem teto.

O argumento original também deixou de valer. A premissa era que a sentinela de 255 °C abortaria um teste saudável — e depois da D68 isso é impossível, porque 0, 255 e qualquer valor fora de 1–120 °C são rejeitados na leitura e nunca chegam à regra de parada. A justificativa caducou; manter a decisão seria manter a consequência de um problema que não existe mais.

Passam a existir três limites independentes:

1. **Parada automática por temperatura alta de confiança.** Quando a temperatura do processador ou da GPU com `Qualidade = Ok` fica em **`LimiteParadaAutomaticaCelsius` ou acima (padrão 100 °C) por 3 amostras consecutivas**, o teste para, com a mensagem "Parado automaticamente: X °C ≥ limite Y °C". Só temperatura de confiança alta conta; o filtro de origem é o que torna essa regra segura.
2. **Teto de 2 minutos sem sensor** (`TetoSemSensor`) quando a CPU não tem temperatura válida. O técnico pode estender apenas por confirmação explícita em diálogo; a extensão é registrada no laudo e limitada a 10 minutos. Sem confirmação, o teste para.
3. **Teto de duração** com padrão de **30 minutos**, configurável até **2 horas**, com aviso um minuto antes do fim.

O gráfico, o limiar de alerta (padrão 95 °C, apenas visual) e o botão **Parar** continuam, e **Parar está sempre ativo** — a parada automática soma, não substitui o controle manual.

Uma observação sobre o limite superior: uma leitura acima de 120 °C também encerra o teste, tratada como parada de emergência, ainda que não seja uma temperatura "válida" pela D68. Continuar rodando enquanto o sensor grita não é aceitável, e a inconsistência aparente é proposital: o valor é rejeitado como medição e aceito como sinal de risco.

## D73 — Vereditos exigem temperatura de confiança alta

`Regras.Cpu` devolvia **OK** quando o teste de estresse terminava sem erro de cálculo e sem throttling — inclusive quando não havia nenhuma leitura de temperatura. O efeito era o pior possível: o programa aprovava o equipamento exatamente no cenário em que não tinha como saber se ele superaquece. Sem temperatura de processador de confiança alta, o item térmico agora fica **Inconclusivo** e o laudo registra a causa.

`Regras.Sensores` tinha um defeito aparentado: pegava a temperatura máxima de **qualquer** componente e comparava com o limite da CPU. Um disco a 45 °C, com limite de 90 °C, produzia "OK" enquanto o processador estava sem leitura — novo caso de aprovação sem medição. Agora a categoria nunca fica OK só porque disco, placa ou GPU reportaram temperatura, e cada componente é comparado com o limite do próprio componente (`LimiteDoComponente`): CPU usa o limite configurado; GPU usa limite + 5; armazenamento, 60 °C; memória, 85 °C; placa/SuperIO, o limite da CPU.

## D74 — Diagnóstico de sensores exportável

**Configurações** ganhou o botão "Exportar diagnóstico de sensores", e a linha de comando ganhou o argumento `--dump-sensores`. Ambos gravam um `.json` e um `.txt` com: ambiente (elevado ou não, PawnIO presente/ausente/desconhecido, versão do Windows, versão da biblioteca de sensores); dados da CPU obtidos por WMI (`Win32_Processor`); e **todos** os sensores, inclusive os que não têm valor, cada um com tipo, nome, identificador, valor, `Min` e `Max`.

A razão é que a informação necessária para fechar o diagnóstico é justamente a que não cabia na tela: sem o par `Min`/`Max` é impossível provar que um valor era sentinela, e sem a lista completa não dá para distinguir "o sensor não existe" de "o sensor existe e não está lendo" — duas causas com soluções diferentes.

Não há número de série nos arquivos: os identificadores passam pelo mascaramento que o aplicativo já usa. `--dump-sensores` grava os arquivos e encerra sem abrir a janela, para que o dono possa registrar a situação antes e depois de instalar um driver com o mesmo ponto de referência.

## D75 — A parada de segurança não depende da interface

A decisão de parar por temperatura era tomada no tique do `DispatcherTimer`. Isso significava que, se
o dispatcher do WPF travasse (janela ocupada, modal, renderização pesada), ninguém cancelava o token e
a carga continuava até o teto de duração. A proteção existia, mas só enquanto a tela respondesse.

Agora a avaliação acontece no predicado de parada do runner, que roda em thread de trabalho, e existe
um vigia independente: se o predicado deixar de ser consultado por mais de
`IntervaloMaximoSemVerificacao` (5 s), a carga é cortada. Esse encerramento é registrado como
`MotivoParada.Erro`, com texto explícito de que o resultado não deve ser usado como veredito — um
teste sem ninguém avaliando a temperatura não é um teste válido.

Complemento necessário: fechar a janela passou a encerrar o trabalho. `Encerrar()` existia mas nunca
era chamado, então as threads de carga continuavam depois do aplicativo fechar. `Window.Closing` e
`SessionEnding` agora chamam `EncerrarTudo`. A liberação da sessão de sensores é feita sem bloquear:
o runner pode estar dentro de `Ler()`, segurando o bloqueio do provider, e esperar por ele travaria o
fechamento da janela.

## D76 — Um único caminho de estresse, ou todos protegidos

A página CPU tinha o seu próprio botão de estresse, com uma implementação paralela: ela passava
`Principal.AmostrasSensores[^1]` como fonte de temperatura. Esse valor é um instantâneo do
diagnóstico rápido, copiado antes do teste começar e nunca atualizado — o limite térmico comparava a
temperatura de antes da carga com o limite, para sempre. Além disso não havia teto sem sensor, nem
confirmação, nem botão de parar, e o token era `CancellationToken.None`.

A correção não foi remover o botão, e sim dar a ele as mesmas garantias da tela de Stress: sessão de
sensores própria, o mesmo `LimiteParadaAutomaticaCelsius`, confirmação explícita quando não há
leitura, os mesmos tetos de tempo e um botão **Parar**. A lição registrada: quando existem dois
caminhos para a mesma operação perigosa, os dois precisam da mesma proteção — um deles ficando para
trás é a forma mais fácil de um limite de segurança virar decoração.

## D77 — Valor implausível não entra no laudo

Uma leitura crua acima de 120 °C agora dispara a parada de emergência, mas não é uma medição. Ela é
excluída de `TemperaturaMaximaCelsius`, da média e da contagem de amostras: só serve para decidir
parar. Sem isso, o laudo poderia imprimir "Temperatura máxima de 130 °C" como se fosse um dado medido,
e o número seria inventado — exatamente o tipo de afirmação que este projeto não faz.

Ainda sobre a contagem: só uma leitura **abaixo** do limite zera a sequência de calor. Uma leitura
ausente não zera, porque um sensor que alterna entre "muito quente" e "sem leitura" está falhando, não
esfriando; zerar ali faria o teste nunca parar justamente no pior momento. O teto sem sensor continua
valendo em paralelo, então isso não produz teste infinito.