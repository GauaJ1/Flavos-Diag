# Flavos Diag — Decisões de projeto

> Toda dúvida resolvida sem perguntar ao usuário é registrada aqui: **D<nº> — decisão — motivo — impacto**.

## Índice das decisões detalhadas

As entradas D68 a D84 são longas demais para caber numa linha de tabela e ficam em seções próprias neste
mesmo arquivo. Esta lista existe para que nenhuma delas fique fora do alcance de quem lê só a tabela.

| # | Decisão (resumo) | Seção |
|---|---|---|
| D68 | A sentinela é tratada na origem, não na tela | [D68](#d68--a-sentinela-é-tratada-na-origem-não-na-tela) |
| D69 | A escolha da temperatura é por tipo de hardware, não por texto do rótulo | [D69](#d69--a-escolha-da-temperatura-é-por-tipo-de-hardware-não-por-texto-do-rótulo) |
| D70 | A causa da falta de leitura é diagnosticada, não adivinhada | [D70](#d70--a-causa-da-falta-de-leitura-é-diagnosticada-não-adivinhada) |
| D71 | O aplicativo nunca instala driver | [D71](#d71--o-aplicativo-nunca-instala-driver) |
| D72 | A decisão sobre parada automática foi revista (substitui a D65 e a D17) | [D72](#d72--a-decisão-sobre-parada-automática-foi-revista-substitui-a-d65-e-a-d17) |
| D73 | Vereditos exigem temperatura de confiança alta | [D73](#d73--vereditos-exigem-temperatura-de-confiança-alta) |
| D74 | Diagnóstico de sensores exportável | [D74](#d74--diagnóstico-de-sensores-exportável) |
| D75 | A parada de segurança não depende da interface | [D75](#d75--a-parada-de-segurança-não-depende-da-interface) |
| D76 | Um único caminho de estresse, ou todos protegidos | [D76](#d76--um-único-caminho-de-estresse-ou-todos-protegidos) |
| D77 | Valor implausível não entra no laudo | [D77](#d77--valor-implausível-não-entra-no-laudo) |
| D78 | `Min == Max` não significa "sem leitura" | [D78](#d78--min--max-não-significa-sem-leitura) |
| D79 | Regra única do teste de memória: o que decide é a contagem de execuções com divergência | [D79](#d79--regra-única-do-teste-de-memória-o-que-decide-é-a-contagem-de-execuções-com-divergência) |
| D80 | Os limites térmicos ficam numa fonte única, `LimitesTermicos` | [D80](#d80--os-limites-térmicos-ficam-numa-fonte-única-limitestermicos) |
| D81 | O veredito térmico da CPU sobe de 90 para 95 °C | [D81](#d81--o-veredito-térmico-da-cpu-sobe-de-90-para-95-c) |
| D82 | Limite de disco por tipo de mídia, com procedência e rótulo "limite genérico" | [D82](#d82--limite-de-disco-por-tipo-de-mídia-com-procedência-e-rótulo-limite-genérico) |
| D83 | Configuração térmica inválida é corrigida com motivo, nunca aceita em silêncio | [D83](#d83--configuração-térmica-inválida-é-corrigida-com-motivo-nunca-aceita-em-silêncio) |
| D84 | Comando de disco vem de lista de permitidos, não do chamador | [D84](#d84--comando-de-disco-vem-de-lista-de-permitidos-não-do-chamador) |
| D85 | Sensor de CPU congelado sob carga vira \Suspeito\, não \SemLeitura\ | [D85](#d85--sensor-de-cpu-congelado-sob-carga-vira-suspeito-não-semleitura) |
| D86 | A emergência acima do máximo físico exige 2 amostras e exclui apenas os sentinelas conhecidos | [D86](#d86--a-emergência-acima-do-máximo-físico-exige-2-amostras-e-exclui-apenas-os-sentinelas-conhecidos) |
| D87 | Toda carga pesada passa por proteção: o benchmark também | [D87](#d87--toda-carga-pesada-passa-por-proteção-o-benchmark-também) |

**Status usados na tabela** (validados automaticamente por `tests/FlavosDiag.Tests/DocumentacaoTests.cs`):

| Status | Significado |
|---|---|
| `Vigente` | A decisão vale como está. |
| `Substituída por Dxx` | Outra decisão ocupou o lugar desta; a linha fica como registro histórico e não deve ser seguida. |
| `Revista por Dxx` | A decisão continua valendo no espírito, mas uma parte do texto foi corrigida pela decisão indicada. |

## Tabela de decisões

| # | Decisão | Motivo | Impacto | Status |
|---|---|---|---|---|
| D1 | Projeto em `Flavos-Diag/` separado do sistema web | Briefing ("projeto separado"). | Isolamento total; integração por arquivo. | Vigente |
| D2 | Dispositivo com erro no Gerenciador: mostrar código + instrução própria, **sem link de fabricante** | Um link exige base de fabricantes mantida e atualizada; sem base, o link seria falso/de enfeite (viola B1). | A recomendação diz "procurar o suporte do fabricante do equipamento" (sem link). Pode virar D2b com base local se o dono mantiver. | Vigente |
| D3 | Timings de memória (CAS/RAS/CL) exibidos como "Não disponível" | Nenhuma fonte confiável com licença compatível foi confirmada; ler SPD exige driver/admin e decodificação de padrões que merecem revisão de procedência. | Campo presente no laudo como `null` + limitação explicada. | Vigente |
| D4 | Sensores sem admin: dependem da biblioteca LHM; se não houver acesso ao driver, resultado Inconclusivo | Sem instalar driver por conta própria (A2, C). | Estresse sem sensor fica limitado a 2 min. | Vigente |
| D5 | smartctl **não** empacotado no MVP: SMART nativo + fallback Windows | Empacotar GPL exige processo separado, texto GPL completo e oferta de código; tem risco de antivírus. O caminho nativo entrega a função. | Se a leitura nativa falhar em disco no teste real, reavaliar (doc em PROGRESS). | Vigente |
| D6 | `AMBIENTE: banco WMI/Fixtures` — fixtures de teste com dados fictícios ficam **somente** em `tests/`, nunca em `src/` | Impede que dados inventados cheguem à UI por acidente (B1). | Fakes ficam em `FlavosDiag.Providers.Fakes`, biblioteca separada referenciada só pelo projeto de testes e pelo app quando `--modo-demo` é passado explicitamente com aviso visível; nenhum projeto de produção usa dado fictício fora desse modo. | Vigente |
| D7 | App aceita `--modo-demo` para demonstração de interface | Permite testar UI sem hardware; precisa ser explicitamente passado. | A janela mostra faixa "Modo demonstração — dados fictícios" que **não** pode ser removida sem reiniciar. | Vigente |
| D8 | Cores: verde-petróleo `#0F766E` como destaque; feedback semântico: OK `#15803D`, Atenção `#B45309`, Falha `#B91C1C`, Inconclusivo `#475569` (cinza) | Contraste AA em fundos claro/escuro. Inconclusivo cinza, não amarelo, para não parecer "quase OK". | Documentado em DESIGN.md. | Vigente |
| D9 | Fonte: Geist se licença OFL confirmada (tem: SIL OFL 1.1), senão Inter (OFL). | Briefing pede Geist "se a licença permitir". | Empacotar OFL exige incluir o texto da licença. Verificado na Fase 5. | Vigente |
| D10 | Não usar `PublishSingleFile` com WPF até validação; entrega em pasta | Mais confiável para WPF + D3D + DLL nativa do monitor de hardware. | Pasta portátil com `FlavosDiag.App.exe`. | Vigente |
| D11 | Dados da bateria: parse do XML do `powercfg` (schema documentado) e `Win32_Battery` como complemento | XML dá capacidades e ciclos que o WMI não fornece de forma confiável. | Fixtures em `tests/fixtures/battery/`. | Vigente |
| D12 | Teste de rede: N=20 pacotes ICMP, timeout 1 s; fallback TCP na porta configurada (padrão vazia) | Redes corporativas bloqueiam ICMP. | Se ambos falharem, rede = Inconclusivo. | Vigente |
| D13 | Configurações em `%LOCALAPPDATA%\FlavosDiag\config.json` com escrita atômica (arquivo temporário + rename) | Evita configuração corrompida. | — | Vigente |
| D14 | Histórico por máquina: chave = fabricante+modelo+serial da placa-mãe (mascarado) hasheado SHA-256 | Evita conflito e reduz exposição do serial. | Hash não é dado pessoal reversível, mas documentado em RISCOS. | Vigente |
| D15 | Diagnóstico rápido: alvo 2–4 min com timeout por etapa; sensores 60 s | Briefing E11. | Mostra "etapa atual" e permite cancelar. | Vigente |
| D16 | Ícones: desenhados como `Path` no XAML, traço 1,5 px, 20×20 | Sem dependência de pacote de ícones (licença + origem). | Todos os ícones são do projeto. | Vigente |
| D17 | Estresse: parada por temperatura com amostras consecutivas ≥ limite, com histerese | Evita parada por pico isolado. | Motivo registrado "Temperatura". A contagem passou de 2 para **3** amostras em `ProtecaoTermica.AmostrasConsecutivasParaParar`, e a histerese na direção contrária deixou de existir; o comportamento em vigor está na D72. | Substituída por D72 |
| D18 | Throttling: comparação de mediana de clock segunda metade × primeira metade, ≥10 % | Heurística simples e explicável. | Limitação no laudo. | Vigente |
| D19 | Pontuação benchmark = unidades de trabalho normalizadas; versão do algoritmo no laudo | Comparabilidade interna e reprodutibilidade. | Histórico grava versão; comparação só entre mesmas versões. | Vigente |
| D20 | GPU: HLSL compilado em runtime com `d3dcompiler_47.dll` do Windows (pacote `Vortice.D3DCompiler`), sem `Effect` e sem `fxc` no build | Compilar em runtime dispensa ferramenta de build no desenvolvimento e mantém o HLSL visível no repositório; a DLL é a de sistema do Windows. A via `Effect` e a de `fxc` ficaram descartadas por exigirem build tools extras sem trazer vantagem. | Se a DLL ou a compilação falhar, GPU = Inconclusivo com a causa (D44). | Vigente |
| D21 | Estrutura de testes: xUnit 2.9.3 + `Microsoft.NET.Test.Sdk` 17.x | Estável e MIT/Apache no dev-only. | — | Vigente |
| D22 | Laudo JSON: `System.Text.Json` com `JsonSerializerOptions` camelCase, `JsonIgnoreCondition.WhenWritingNull` | Tamanho e legibilidade; campos ausentes explícitos (nunca null em campos essenciais: usamos `Leitura` serializada com estado). | `SCHEMA-LAUDO.md`. | Vigente |
| D23 | Sensores CSV: separador `;` e UTF-8 com BOM (Excel pt-BR abre corretamente) | Usabilidade do técnico. | Data/hora ISO 8601 no CSV. | Vigente |
| D24 | Elevação: `ShellExecute` com `runas`; nunca altera serviços/drivers; se o usuário recusar, segue em modo reduzido | C. | Mensagem clara. | Vigente |
| D25 | App sem "instalar" nada: distribuição em ZIP/pasta | Portátil. | README. | Vigente |
| D26 | Skills do projeto instaladas em `.skills/` (fora de `.claude/`) e carregadas manualmente | O agente desta sessão lê as skills por caminho; `.skills/` não colide com a pasta de outro assistente e não é removida por ferramentas que gerenciam `.claude/`. | Cópia versionada junto do projeto; a origem é o pacote `flavos-diag-skills.zip` entregue pelo dono. | Vigente |
| D27 | Verificador de clean room portado para PowerShell (`build/cleanroom.ps1`) | Python não está instalado nesta máquina; o porte mantém a mesma lista de termos proibidos, as mesmas pastas varridas e a mesma lista de exceções. | O script `.py` original permanece em `.skills/` como referência. Os padrões do original foram escritos de forma fragmentada para que o próprio código do verificador não dispare um achado ao ser varrido. | Vigente |
| D28 | `docs/RISCOS-LEGAIS.md` é a única exceção do verificador (`cleanroom_allow.txt`) | É o documento que precisa listar nominalmente as proibições. | Qualquer exceção nova exige linha aqui. | Vigente |
| D29 | PDF do laudo usa a fonte Geist embutida como recurso do assembly de relatórios | A partir da versão 2026.9 o QuestPDF deixou de usar fontes do sistema e aborta a geração quando a família não está registrada; embutir a fonte garante o mesmo resultado em qualquer máquina. | Fonte SIL OFL 1.1 com texto de licença empacotado; ver LICENCAS.md. | Vigente |
| D30 | Identificação de disco vem do WMI (`Win32_DiskDrive` + `MSFT_PhysicalDisk`), não dos IOCTL de descritor | Verificado nesta máquina: os IOCTL de descritor devolvem "acesso negado" sem elevação, e com elevação o WMI é igualmente suficiente. Assim o técnico sem administrador continua vendo modelo, série, barramento e tipo de mídia; só a saúde fica Inconclusiva. | Fonte real por campo no laudo (`STORAGE_DEVICE_DESCRIPTOR` vs `MSFT_PhysicalDisk.MediaType`). | Vigente |
| D31 | Leitura SMART ATA exige abrir o disco com `GENERIC_WRITE` | Verificado empiricamente nesta máquina: com `GENERIC_READ` o IOCTL de pass-through devolve `ERROR_ACCESS_DENIED` (5) mesmo elevado; com `GENERIC_WRITE` é aceito. O comando enviado é **SMART READ DATA** (0xD0), somente leitura de dados; nenhum setor é escrito. | Registrado em comentário no código e em `docs/RISCOS-LEGAIS.md`. Não é contornar permissão: exige execução como administrador, como a skill manda. | Vigente |
| D32 | Sem SMART acessível, o veredito usa a previsão de falha do Windows e o `HealthStatus` da API de armazenamento | São leituras reais do Windows, disponíveis sem elevação. Quando nenhuma responde, o disco fica Inconclusivo com a causa. | Nesta máquina os atributos SMART são recusados pelo controlador (status 0x51/erro 0x04 mesmo elevado); o `HealthStatus` responde. Mascarar isso como "saudável" seria falso. | Vigente |
| D33 | Bateria: presença exige ao menos uma bateria no relatório ou no WMI | Verificado nesta máquina: o `powercfg` gera o XML com `<Batteries></Batteries>` vazio em desktop, então "o arquivo existe" não prova que há bateria. Desktop sem bateria é **Não se aplica**. | Antes do ajuste, o roteiro reportou "presente: sim" numa máquina sem bateria — falso positivo corrigido. | Vigente |
| D34 | Rede: o adaptador "principal" é o que tem rota padrão | Verificado nesta máquina: havia 8 adaptadores (túneis de VPN, Npcap, Wi-Fi Direct) e o primeiro ativo com IP não tinha gateway; a latência do gateway saía vazia mesmo com internet. Priorizar gateway resolve sem inventar dado. | Wi-Fi consultado com caminho completo do Windows PowerShell (`System32\WindowsPowerShell\v1.0`), porque o `powershell.exe` não fica em `System32`. | Vigente |
| D35 | Versões de pacote travadas em `packages.lock.json` por projeto, com `--locked-mode` na restauração | A skill de segurança exige cadeia de suprimentos determinística; sem o lock, uma versão nova publicada no NuGet entra sem revisão. | `RestorePackagesWithLockFile`, `NuGetAudit` e `NuGetAuditMode=all` no `Directory.Build.props`; `build/test.ps1` restaura travado antes de compilar. Os 6 arquivos de lock são versionados e cada um guarda o hash de conteúdo de cada pacote. | Vigente |
| D36 | `build/test.ps1` resolve o `dotnet` por conta própria | Verificado: o script falhava com "No .NET SDKs were found" quando chamado de um shell sem a instalação por usuário no PATH — o script precisa funcionar de qualquer terminal, inclusive na máquina do dono. | Procura no PATH, em `%LOCALAPPDATA%\Microsoft\dotnet` e em `%ProgramFiles%\dotnet`, e falha com mensagem clara se não achar nenhum com SDK. | Vigente |
| D37 | Teste de memória usa `GlobalMemoryStatusEx`/`ullAvailPhys`, não `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` | O segundo é um limite de política do coletor de lixo, não a memória física livre: superestima o que há de fato e pode fazer o PC do cliente paginar. | Fração 50 %, teto 2 GiB, piso 256 MiB (P3-09a). Memória livre injetável por construtor para os testes não dependerem da máquina. | Vigente |
| D38 | Teste rápido de memória sem erro fica **Inconclusivo**, nunca OK | Um teste de segundos em um buffer não sustenta a afirmação "a memória está boa". A skill de vereditos manda tratar teste incompleto como Inconclusivo. | A contagem de erro passa a ser a da **regra única** de `REGRAS-VEREDITO.md` 3.2.1: 1 execução com divergência → Atenção; 2 ou mais execuções com divergência → Falha. | Revista por D79 |
| D39 | Sensores **já foram lidos em hardware real**: a CPU sem leitura é falha de driver, não falta de verificação; o que continua sem verificação é a leitura térmica da CPU com o driver, o stress real e o teste de memória em hardware | A leitura de sensores deixou de ser suposição: `--dump-sensores` foi executado nesta máquina (Ryzen 5 8500G, Windows 11) e o diagnóstico respondeu com dados reais — `Core (Tctl/Tdie)` devolvendo `0` com `Min = 0`/`Max = 0`, o `GPU Core` entre 46 e 48 °C e os demais sensores listados, cada um com valor, mínimo e máximo. Isso provou o pipeline de leitura (abertura da sessão, classificação por tipo de hardware, tratamento de sentinela, exportação) e descartou a hipótese de que o código simplesmente não lia sensores. A causa da CPU sem leitura ficou identificada como ausência do driver (`PawnIo.IsInstalled = false`), que é um achado de ambiente, não um buraco de verificação. | Continua **não verificado**: (a) leitura térmica da CPU **com** o driver instalado, isto é, que instalar o PawnIO faz o `Core (Tctl/Tdie)` responder valores plausíveis — a D71 permanece "implementada, aguardando validação no hardware"; (b) stress real com o driver ativo: a parada automática em 3 amostras consecutivas acima do limite de parada (hoje 95 °C, ver D81) e o teto de 2 min sem sensor foram exercitados só com amostras construídas e relógio simulado; (c) teste de memória em hardware e em módulo defeituoso (só rodou com tamanhos pequenos e memória livre injetada); (d) prova de GPU com sensor de temperatura de GPU de confiança, porque sem elevação o sensor não responde. O roteiro para fechar (a) a (d) está em `docs/ROTEIRO-TESTE-MANUAL.md`, seção 4D, e a situação está declarada no `PROGRESS.md`. | Vigente |
| D40 | GPU usa Vortice (MIT) em vez de P/Invoke escrito à mão | A primeira tentativa foi uma tabela de vtables COM escrita à mão; é frágil e cara de manter. Vortice.Direct3D11/DXGI/D3DCompiler 3.8.3 são MIT, maduras (2,2 mi+ downloads) e geradas a partir dos cabeçalhos oficiais. Registrado em `LICENCAS.md`. | Camada de GPU restrita ao projeto `FlavosDiag.Providers`, sem tabela de vtables mantida à mão. | Vigente |
| D41 | `Compiler.Compile` recebe (fonte, entrada, **nomeDoArquivo**, **profile**) | A ordem é essa; a documentação de exemplo do projeto engana. Errar a ordem produz `error X3506: unrecognized compiler target`, que mascara o problema real. | A chamada fica num único ponto (`GpuD3D11Provider`), com comentário sobre a ordem dos parâmetros. | Vigente |
| D42 | `Vortice.DirectX` é referência explícita, não só transitiva | Verificado: sem a referência o app compila e falha em execução com `FileNotFoundException`. | Referência explícita no projeto que usa Direct3D, para o pacote chegar à pasta de saída. | Vigente |
| D43 | Shader da GPU é determinístico; o número do quadro não entra no cálculo | A primeira execução real acusou 806 divergências em 806 quadros — o shader variava por projeto. Qualquer GPU seria reprovada. Após corrigir: 0 divergências em 794 quadros na mesma máquina. | Mesmo shader e mesmos parâmetros produzem o mesmo hash de pixels; o número do quadro só muda o alvo. | Vigente |
| D44 | O provider de GPU propaga a causa exata da falha | Antes devolvia "não determinado" sem motivo, violando a regra de dado ausente com causa. Agora `ResultadoGpu.CausaAusencia` carrega o texto e a categoria fica Inconclusivo. | Toda ausência de prova de GPU chega ao laudo com o texto da causa. | Vigente |
| D45 | `AllowUnsafeBlocks` só em `FlavosDiag.Providers` | Necessário apenas para copiar os pixels mapeados do Direct3D. Fica restrito a um projeto e o código não seguro se limita ao laço de cópia do buffer. | Nenhum outro projeto compila código não seguro. | Vigente |
| D46 | Rasterizador de software como reserva, registrado como limitação | Em máquina virtual sem aceleração, a prova ainda roda (mais lenta) em vez de virar Inconclusivo. Nesta máquina o caminho usado foi a GPU de hardware (AMD Radeon 740M). | Quando a reserva é usada, o laudo registra a limitação em vez de apresentar o resultado como equivalente à GPU real. | Vigente |
| D47 | Comando do menu captura a página; `ItensNav` é montada uma vez no construtor | O `Button` do `DataTemplate` não passava `CommandParameter` e o `RelayCommand<PaginaBase>` recebia `null`: nenhum item do menu navegava. Como comando genérico exige que o XAML coopere, cada item passa a ter um `RelayCommand` sem parâmetro que já conhece a sua página. Recriar a coleção a cada navegação reconstruía os botões e perdia o foco do teclado; agora o destaque é atualizado in loco em `OnPaginaAtualChanged`. | Navegação funciona com clique e com teclado; coberta por `InterfaceTests`. | Vigente |
| D48 | Configurações entra no destaque do menu, mas fora do `ItemsControl` | O botão fica no rodapé, onde não recebe o `DataTrigger` dos itens de categoria. Um conversor `SelecaoParaFundo` devolve o pincel do tema atual, para o destaque não depender de `StaticResource`. | O item Configurações destaca nos dois temas. | Vigente |
| D49 | `IncludeCancelCommand` no diagnóstico rápido, separado do comando de iniciar | O botão Cancelar estava ligado ao próprio comando de iniciar, que se recusava a rodar de novo (`if (Executando) return;`): não cancelava nada. Cancelar registra "cancelado" com veredito Inconclusivo, nunca Falha — interromper é escolha do técnico, não defeito do equipamento. | Cancelar interrompe em menos de 2 s e não emite veredito. | Vigente |
| D50 | Tema escuro é uma sobreposição **apenas de cores** | Estilos duplicados no dicionário escuro congelam as cores do tema claro no momento da carga. Com `StaticResource` em Tema.xaml, os botões do rodapé ficavam no tema antigo depois da troca. Todos os 32 usos de cor foram para `DynamicResource` e o dicionário escuro ficou só com cores. | `TemaEscuro.xaml` com 0 estilos; teste garante a cobertura de todas as chaves de cor. | Vigente |
| D51 | A chave `FlavosTextoNoDestaque` existe porque o tema escuro clareia o destaque | O texto do botão primário era `White` fixo; no escuro o verde-petróleo fica claro e o branco perderia contraste. A tinta sobre o destaque passou a ser um token de tema. | Nenhuma tela usa `White`, `Black` ou `#RRGGBB` fixo em propriedade de cor (há teste). | Vigente |
| D52 | `App.AplicarTema` usa URI com nome de assembly | A forma `Theme/TemaEscuro.xaml` só resolve quando quem carrega é o próprio assembly do aplicativo; com `pack://application:,,,/FlavosDiag.App;component/...` a troca de tema funciona também a partir de código de teste. | A troca de tema é testável sem abrir a janela. | Vigente |
| D53 | Regra de faixa de temperatura mora no Core (`FaixaTemperatura`) | Tanto o veredito (`Regras`) quanto os coletores (`Providers`) precisam dela, e o Core não pode depender dos Providers. Uma definição só evita as duas camadas divergirem. | Sentinela decidida num único lugar (`FaixaTemperatura`/`TemperaturaFisica`). | Vigente |
| D54 | Atributo SMART de temperatura inválido continua listado, marcado `LeituraIndisponivel` | Esconder o atributo esconderia que o disco o reportou; transformá-lo em leitura geraria "255 °C". Listado e marcado, o técnico vê que existe e que não vale, e o avaliador não emite achado. | Atributo visível na tela e ausente do cálculo de saúde. | Vigente |
| D55 | `LogSaudeNvme.TemperaturaCelsius` virou `int?` | Devolver `0` quando o campo não foi preenchido é a forma mais fácil de inventar uma medição. Nulo obriga quem consome a tratar a ausência. | Consumidores tratam `null` como "sem leitura", com a causa no laudo. | Vigente |
| D56 | Inventário de memória não é teste: sem teste o veredito é Inconclusivo | Ler a quantidade de memória diz o que está instalado, não que os módulos retêm dados. Um teste rápido limpo também não prova nada. A contagem de erro é a de **execuções com divergência** (padrões distintos), não a de palavras divergentes: 1 execução com erro → Atenção; 2 ou mais → Falha. | Regra unificada em `REGRAS-VEREDITO.md` 3.2.1. O critério antigo ("Falha só com 2 ou mais execuções") foi substituído pela tabela por caso dessa regra única. | Substituída por D79 |
| D57 | Não foram aninhados dois `ScrollViewer` | Cada página já tem o seu. Um segundo quebraria a roda do mouse e a barra de rolagem. O que evita corte é a janela caber na tela, o que se resolve limitando o tamanho ao `WorkArea`. | `MainWindow` limita largura e altura ao `WorkArea`; há teste de que a página tem um só `ScrollViewer`. | Vigente |
| D58 | `ResultadoTesteMemoria.ExecucoesComErro` tem padrão 1 | Chamadores já existentes só criavam o resultado quando havia erro; o padrão 1 preserva esse significado e não muda comportamento antigo. | O padrão fica como valor **legado** do record, não como regra de veredito: a regra é a de `REGRAS-VEREDITO.md` 3.2.1 (D79). O runner sempre preenche o campo com a contagem real de padrões com divergência. | Substituída por D79 |
| D59 | Benchmark versão 2: mesma carga nos dois modos | Na versão 1 o modo multi somava ciclos de quatro cargas com pesos muito diferentes (matriz 192, crivo 2.000.000, hash 262.144, ponto flutuante 131.072), enquanto o single media só a primeira. Os dois números não mediam a mesma coisa e a relação "single × núcleos ≈ multi" nunca fechava. Agora ambos usam a mesma carga e a mesma unidade. | `VersaoAlgoritmo = 2` no laudo e no histórico; comparação só entre mesmas versões. | Vigente |
| D60 | A escala da pontuação: 1 ponto = 1000 operações/s por thread | O código já dividia por `OperacoesPorPonto = 1000`; o comentário e o teste escritos nesta rodada afirmavam "1000 pontos", que estava errado. O comportamento foi preservado e a documentação corrigida — mudar a constante para casar com um comentário errado teria mudado todas as pontuações já gravadas. | Documentação e teste corrigidos; pontuações antigas continuam comparáveis. | Vigente |
| D61 | `EficienciaEscala` é nula quando não há medição válida | Dividir por zero daria `Infinity` na tela. Sem single, sem multi ou com uma só thread, a eficiência é `null` e a interface mostra a causa. | Campo anulável no laudo; a tela mostra a causa em vez de um número inventado. | Vigente |
| D62 | Gráfico desenhado à mão, sem biblioteca de gráficos | O app é offline e de um único executável; uma dependência de gráficos pesaria mais que o problema. Um `FrameworkElement` com `OnRender` desenha grade, faixas e curvas em poucas centenas de linhas. | Nenhum pacote de gráficos na distribuição. | Vigente |
| D63 | Eixo Y fixo em 20–110 °C, nunca a partir de zero | Uma escala de 0 a 110 comprime a variação útil de 40 a 90 °C num terço da altura e faz qualquer curva parecer plana. Abaixo de 20 °C não há leitura plausível de componente em operação. | Escala constante entre execuções, comparável na tela. | Vigente |
| D64 | Buraco na série quebra a linha em vez de interpolar | Ligar dois pontos através de um período sem leitura inventaria uma temperatura que não foi medida. A linha para e recomeça, e a causa aparece escrita na tela. | Nenhum trecho do gráfico atravessa período sem leitura. | Vigente |
| D65 | O stress não tem limite que o encerre sozinho | Um sensor que devolve sentinela (0 ou 255) abortaria um teste saudável, e o técnico perderia a corrida térmica. O limiar de alerta (padrão 95 °C, configurável) apenas destaca a faixa no gráfico; quem decide parar é o técnico. O único encerramento automático é o teto de 4 h, que existe para não deixar carga esquecida. | Revogada: voltaram a parada automática por temperatura de confiança alta (3 amostras consecutivas; o limite padrão é hoje 95 °C, ver D81), o teto de 2 min sem sensor e o teto de duração. Ver D72. | Substituída por D72 |
| D66 | O coletor passou a habilitar a GPU (`IsGpuEnabled`) | Estava desligado porque a GPU era tratada pela prova de vídeo. Mas a tela de stress promete temperatura da GPU, e o sensor respondia 48 °C enquanto a tela dizia "sem leitura". Um teste lê o fonte para travar essa configuração, porque abrir a sessão real exigiria driver e elevação. | A tela de stress e o laudo mostram a temperatura da GPU quando o sensor responde. | Vigente |
| D67 | Amostragem de 1 s num `DispatcherTimer` | O gráfico precisa de resolução suficiente para mostrar a subida térmica e não transformar 4 h de teste em 14 400 pontos. Uma amostra por segundo é o intervalo em que a curva ainda é legível. | Janela do gráfico de 60 s por padrão; a cadência já não é a única proteção (ver D75). | Vigente |
| D68 | A sentinela é tratada na origem, não na tela | `TemperaturaFisica` é a regra única de "isto é uma temperatura utilizável" e os sensores sem leitura vão para `AmostraSensores.SemLeitura` com a causa; texto completo na seção detalhada (D68). | A parte sobre `Min == Max` foi revista pela D78: hoje `Min == Max` **não** significa "sem leitura". | Revista por D78 |
| D69 | A escolha da temperatura é por tipo de hardware, não por texto do rótulo | `ClassificadorTemperatura` filtra por `TipoHardware`, depois por preferência de nome e por último por `TemperaturaFisica.Valida`; texto completo na seção detalhada (D69). | Nenhum sensor de GPU ou de placa é lido como temperatura da CPU (há teste). | Vigente |
| D70 | A causa da falta de leitura é diagnosticada, não adivinhada | `DiagnosticoSensores` recebe fatos por records e avalia quatro regras em ordem estrita; texto completo na seção detalhada (D70). | Testável em qualquer máquina, sem hardware nem elevação. | Vigente |
| D71 | O aplicativo nunca instala driver | Mostra a causa, o comando sugerido e um botão Copiar; não executa, não se eleva e não baixa nada; texto completo na seção detalhada (D71). | O comando só aparece quando instalar o driver é causa possível; a instalação do PawnIO continua pendente de validação no hardware (ver D39). | Vigente |
| D72 | A decisão sobre parada automática foi revista (substitui a D65 e a D17) | Três limites independentes (limite de parada por 3 amostras de confiança alta — hoje 95 °C, ver D81 —, 2 min sem sensor, teto de duração de 30 min configurável até 120 min) e o botão Parar sempre ativo; texto completo na seção detalhada (D72). | Substitui a D65 (nunca parar sozinho) e a D17 (2 amostras com histerese): a contagem passou a 3 amostras e uma leitura ausente não zera a sequência (ver `REGRAS-VEREDITO.md`, seção 3.10). | Vigente |
| D73 | Vereditos exigem temperatura de confiança alta | `Regras.Cpu` fica Inconclusivo sem temperatura de confiança alta e `Regras.Sensores` compara cada componente com o limite do próprio componente; texto completo na seção detalhada (D73). | Sem temperatura de confiança da CPU, nenhuma categoria térmica vira OK. | Vigente |
| D74 | Diagnóstico de sensores exportável | Botão "Exportar diagnóstico de sensores" e argumento `--dump-sensores`, gravando `.json` e `.txt` sem números de série; texto completo na seção detalhada (D74). | É o instrumento usado para registrar a situação dos sensores antes e depois de instalar um driver (ver D39). | Vigente |
| D75 | A parada de segurança não depende da interface | O predicado de parada roda em thread de trabalho e um vigia corta a carga se deixar de ser consultado por mais de 5 s; texto completo na seção detalhada (D75). | Fechar a janela encerra a carga (`Window.Closing` e `SessionEnding` ligados a `EncerrarTudo`). | Vigente |
| D76 | Um único caminho de estresse, ou todos protegidos | O botão de estresse da página CPU passou a abrir sessão própria de sensores, com o mesmo limite de parada, confirmação sem sensor, tetos e botão Parar; texto completo na seção detalhada (D76). | Os dois caminhos usam `CargaCpuRunner`, que aplica o limite térmico, o teto sem sensor e o teto de duração. | Vigente |
| D77 | Valor implausível não entra no laudo | Leitura crua acima de 120 °C para o teste, mas não entra em `TemperaturaMaximaCelsius`, na média nem na contagem de amostras; texto completo na seção detalhada (D77). | Nenhum número não medido chega ao laudo. | Vigente |
| D78 | `Min == Max` não significa "sem leitura" | `Min` e `Max` são acumulados desde a abertura da sessão, então `Min == Max` é verdade em toda primeira leitura e em sensor estável de resolução de 1 °C — o `GPU Core` desta máquina devolveu `46 (min 46, max 46)` numa leitura boa; texto completo na seção detalhada (D78). | A sentinela passa a ser decidida só pelo **valor** (`0`, `255` e fora de 1–120 °C). `TemperaturaFisica.EhSentinela` ignora `Min`/`Max` de propósito. | Vigente |
| D79 | Regra única do teste de memória: o que decide é a contagem de execuções com divergência | A D38, a D56 e a D58 divergiam entre si (ver "Por que D38, D56 e D58 não convivem", abaixo). Ficou uma regra só, publicada em `REGRAS-VEREDITO.md` 3.2.1: sem teste → Inconclusivo; teste rápido sem divergência → Inconclusivo; 1 execução com divergência → Atenção; 2 ou mais execuções com divergência → Falha. | A contagem é por **execução** (padrão distinto com divergência), não por palavra divergente. O campo `ExecucoesComErro` do record continua com padrão 1 por compatibilidade, e o runner sempre o preenche. | Vigente |
| D80 | Os limites térmicos ficam numa fonte única, `LimitesTermicos` | O mesmo número aparecia em pontos diferentes com valores diferentes: veredito da CPU a 90 °C, alerta do gráfico a 95 °C, veredito de sensores a 60 °C para qualquer disco e configuração prometendo parar em 100 °C. Nenhum estava errado isoladamente; o erro era não haver um lugar que dissesse qual vale. | `Core/Monitor/LimitesTermicos.cs` concentra constantes, limites por componente (com mídia do disco), procedência e validação. `Regras`, `CargaCpuRunner`, `Configuracao`, a tela de Configurações e a prova de GPU passaram a ler daqui. Um teste de arquitetura reprova literal térmico em `src/` fora dos dois módulos permitidos. | Vigente |
| D81 | O veredito térmico da CPU sobe de 90 para 95 °C | Um processador moderno sustenta 90 °C de projeto sob carga plena sem estar defeituoso. O limite de 90 reprovava máquina saudável, e veredito que reprova o que está bem ensina o técnico a ignorar o veredito — o custo é a credibilidade do laudo inteiro. | `VereditoCpuCelsius`, `AlertaVisualCelsius` e `ParadaAutomaticaCelsius` ficam todos em 95 °C, com `veredito <= alerta <= parada < emergência`. A parada automática voltou de 100 para 95 °C, fechando o intervalo em que já havia aviso e ainda havia carga total. | Vigente |
| D82 | Limite de disco por tipo de mídia, com procedência e rótulo "limite genérico" | Havia um único limite de 60 °C para qualquer disco. Um HDD a 80 °C está em sofrimento e um NVMe a 80 °C pode estar em operação normal: o limite único reprovava mídia mecânica quente demais e reprovava SSD/NVMe saudável. | HDD 55 °C, SSD SATA 70 °C e NVMe 80 °C são **valores de partida**, não especificação de fabricante — a própria frase vai no laudo. Quando o disco informa o próprio limiar de aviso/crítico, ele é preferido e a fonte é registrada; sem esse dado, o valor sai rotulado **limite genérico**. Mídia desconhecida usa o limite do HDD, o menor. | Vigente |
| D83 | Configuração térmica inválida é corrigida com motivo, nunca aceita em silêncio | A tela antiga aceitava qualquer combinação do alerta com a parada: dava para salvar alerta 119 °C e parada 60 °C, e o aviso simplesmente nunca apareceria. Uma proteção que o técnico acredita ter configurado e não tem é pior do que proteção nenhuma, porque ninguém mais confere. | `LimitesTermicos.Validar` impõe as invariantes, repõe cada campo inválido ao padrão e devolve o motivo em pt-BR. Na tela de Configurações os motivos são mostrados e nada é salvo (`LimiarAlertaValido`/`LimiteParadaValido` em falso); fora da tela, o motivo fica registrado. `docs/LIMITES-TERMICOS.md` publica a tabela canônica. | Vigente |
| D84 | Comando de disco vem de lista de permitidos, não do chamador | O pass-through ATA/NVMe exige abrir o disco com `GENERIC_WRITE` (D31). Nada é escrito, mas o processo fica com permissão de escrita no disco do cliente, e a única barreira contra um `WRITE SECTORS` futuro era um comentário. | `ComandosDiscoPermitidos` lista só comandos de leitura; `RequisicaoDisco.Executar` recebe pedido enumerado (`PedidoAta`/`PedidoNvme`) e não aceita byte de comando em nenhuma sobrecarga. A conferência da lista acontece **antes** do `CreateFileW`. Um teste de arquitetura reprova `\\.\PhysicalDrive`, os IOCTL de pass-through e `GENERIC_WRITE` fora do caminho único. | Vigente |
| D85 | Sensor de CPU congelado sob carga vira \Suspeito\, não \SemLeitura\ | Medida nesta máquina: o \GPU Core\ fica em 46 °C com mín == máx e é saudável; já um sensor de CPU parado no mesmo valor por 2 min com a máquina a 90 % de carga não está medindo a realidade. O valor continua no gráfico; o que ele perde é a condição de confiança alta. | Evita perder leitura boa (o erro da D68) sem confiar em número que ninguém viu mudar. Implementado em \DetectorCongelamento\. | Vigente |
| D86 | A emergência acima do máximo físico exige 2 amostras e exclui apenas os sentinelas conhecidos | 255 é maior que 120: uma regra só de "acima de 120 °C" tratava a sentinela clássica como superaquecimento e abortava teste saudável. Mas \EhSentinela\ responde "isto é medição utilizável?", e 130 °C reprova nessa pergunta — usar a mesma função faria a emergência ignorar todo valor alto. | Duas listas de sentinelas com propósitos distintos, ambas testadas com as séries 60/60/255/60 e 60/125/125. | Vigente |
| D87 | Toda carga pesada passa por proteção: o benchmark também | O benchmark rodava com predicado \() => false\, isto é, carga total em todos os núcleos sem ler temperatura nenhuma, e o chamador da página CPU passava \CancellationToken.None\. | O benchmark agora recebe leitura de sensores e teto, e a tela abre sessão própria e tem botão Parar. | Vigente |

### Por que D38, D56 e D58 não convivem

- **D38** mandava uma divergência para Atenção e falava em "divergências em padrões distintos". Era a regra
  publicada em `REGRAS-VEREDITO.md` 3.2.1, mas não dizia como contar as divergências.
- **D56** mandava Falha só com 2 ou mais **execuções** com erro, sem definir o que é uma execução e criando
  a leitura de que "1 erro nunca é Falha".
- **D58** deixou o record com `ExecucoesComErro = 1` por padrão, o que faz um resultado criado sem preencher
  o campo contar como uma execução com erro — isso é compatibilidade de serialização, não regra de veredito.

A D79 substitui as três, e a regra publicada é a de `REGRAS-VEREDITO.md` 3.2.1, com a tabela por caso. A
divergência entre a regra publicada e `Regras.Memoria` está registrada em `REGRAS-VEREDITO.md`, seção 3.10,
e **não** foi corrigida no código nesta tarefa.

## Entradas detalhadas

As entradas abaixo são as decisões longas. Cada uma também aparece na tabela, e o índice no topo do arquivo
leva até elas.

## D68 — A sentinela é tratada na origem, não na tela

O sensor `Core (Tctl/Tdie)` do processador, na máquina do dono (Ryzen 5 8500G), devolveu `Value = 0`, `Min = 0` e `Max = 0` — inclusive com carga real sobre o processador. Até aqui o aplicativo descartava apenas leitura `null` ou NaN. Um `0` numérico passava adiante, e um zero que atravessa o pipeline vira gráfico desenhado em 0 °C, laudo com "0 °C" e veredito tomado sobre uma temperatura que nunca foi medida: a mentira mais cara que este programa pode contar, porque é silenciosa e tem cara de medição.

A decisão é tratar a sentinela na origem. `TemperaturaFisica`, no Core, passou a ser a **única** regra que decide se um número é temperatura utilizável. Ficam classificados como *sem leitura*: `0`; `255`; e qualquer valor fora de 1–120 °C. O critério `Min == Max`, que chegou a ser tratado como ausência de leitura, foi **revisto pela D78** e não faz mais parte desta lista. O provider registra essas amostras numa lista própria, `AmostraSensores.SemLeitura`, com tipo, nome do hardware, nome do sensor, valor bruto, `Min`, `Max` e o motivo escrito em pt-BR.

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

## D72 — A decisão sobre parada automática foi revista (substitui a D65 e a D17)

A D65 removeu a parada automática por temperatura, com o argumento de que o técnico deve estar sempre no comando e que uma sentinela (0 ou 255) abortaria um teste saudável. A decisão foi revista, e o motivo é o que a D65 não pesou.

O código naquele estado usava `LimiteTemperaturaCelsius = double.MaxValue` e `DuracaoMaxima = 4 h`. Na prática: se o processador não tivesse nenhuma leitura de temperatura, o teste rodaria em carga máxima por até quatro horas **sem proteção alguma**. A "escolha do técnico" era na verdade ausência de teto, num caso em que o técnico nem sabe que está sem teto.

O argumento original também deixou de valer. A premissa era que a sentinela de 255 °C abortaria um teste saudável — e depois da D68 isso é impossível, porque 0, 255 e qualquer valor fora de 1–120 °C são rejeitados na leitura e nunca chegam à regra de parada. A justificativa caducou; manter a decisão seria manter a consequência de um problema que não existe mais.

Passam a existir três limites independentes:

1. **Parada automática por temperatura alta de confiança.** Quando a temperatura do processador ou da GPU com `Qualidade = Ok` fica em **`LimiteParadaAutomaticaCelsius` ou acima por 3 amostras consecutivas**, o teste para, com a mensagem "Parado automaticamente: X °C ≥ limite Y °C". Só temperatura de confiança alta conta; o filtro de origem é o que torna essa regra segura. O valor padrão era 100 °C nesta decisão e passou a **95 °C** pela D81; a constante canônica vive em `LimitesTermicos` (D80), não neste documento.
2. **Teto de 2 minutos sem sensor** (`TetoSemSensor`) quando a CPU não tem temperatura válida. O técnico pode estender apenas por confirmação explícita em diálogo; a extensão é registrada no laudo e limitada a 10 minutos. Sem confirmação, o teste para.
3. **Teto de duração** com padrão de **30 minutos**, configurável até **120 minutos**, com aviso um minuto antes do fim. O runner também impõe o teto absoluto `CargaCpuRunner.DuracaoMaximaPermitida` de 60 minutos, então na prática o campo aceita até 120 mas a carga nunca passa de 60 (divergência registrada em `REGRAS-VEREDITO.md`, seção 3.10).

O gráfico, o limiar de alerta (apenas visual) e o botão **Parar** continuam, e **Parar está sempre ativo** — a parada automática soma, não substitui o controle manual.

A D17 continua sendo o motivo de existir de uma parada por amostras consecutivas (um pico isolado não deve abortar um teste saudável), mas os números e o mecanismo dela mudaram. Hoje a contagem é de **3** amostras acima do limite (`ProtecaoTermica.AmostrasConsecutivasParaParar`), **sem histerese na direção contrária**: o contador é zerado por qualquer leitura **abaixo** do limite, não só por uma leitura abaixo de "limite − histerese". Uma leitura **ausente** não zera o contador, porque um sensor que alterna entre "muito quente" e "sem leitura" está falhando, não esfriando (D77). `CargaCpuRunner.AmostrasAcimaDoLimiteParaParar` repete o valor 3 no runner e é usado na parada do stress; o caminho de parada do **benchmark** escreve o literal `3` em vez da constante (mesma regra, mas mudar a constante não muda o benchmark — registrado em `REGRAS-VEREDITO.md` 3.10). O limite de emergência para leitura crua acima de 120 °C (`CargaCpuRunner.AmostrasConsecutivasEmergencia = 2`) tem contagem própria, porque ali a regra é "seguir um sinal de sensor em estado inválido", não "confirmar uma medição térmica".

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

## D78 — `Min == Max` não significa "sem leitura"

A D68 colocou na regra única de temperatura que um sensor cujo `Min` é igual ao `Max` não está medindo,
"porque nunca variou". A regra estava errada, e o erro foi medido nesta máquina: `Min` e `Max` são
acumulados desde a abertura da sessão, então `Min == Max` é verdade em **toda primeira leitura** e
continua verdade para qualquer sensor estável com resolução de 1 °C. O `GPU Core` desta máquina devolveu
`46 (min 46, max 46)` em três leituras ao longo de 5 s — uma leitura perfeitamente boa, que a regra antiga
descartava. O caso que motivou a D68 continua coberto, porque o valor `0` já é sentinela por si só,
independentemente de `Min` e `Max`.

A decisão é que a sentinela é decidida pelo **valor**, nunca por `Min == Max`: `TemperaturaFisica.EhSentinela`
recebe `Min` e `Max` por compatibilidade de assinatura e os ignora de propósito (os parâmetros existem, não
são usados e há comentário no código explicando o porquê). Não é temperatura utilizável: `null`, `NaN`,
infinito, `0`, `255` e qualquer valor fora de 1–120 °C.

Estabilidade só é suspeita quando é improvável — sensor de CPU, sob carga, com o valor idêntico por muito
tempo. Isso é `QualidadeSensor.Suspeito`, decidido pelo coletor ao longo do tempo, e não numa amostra isolada.
O par `Min`/`Max` continua sendo exportado e mostrado na tela (D74), porque é ele que permite provar depois
que um valor era sentinela; ele só deixou de decidir o que é leitura.

## D79 — Regra única do teste de memória: o que decide é a contagem de execuções com divergência

A D38, a D56 e a D58 diziam coisas parecidas com resultados diferentes, e o resultado era o pior possível: o
mesmo teste dava um veredito diferente conforme qual das três fosse lida. A regra que fica é uma só, e é a
publicada em `REGRAS-VEREDITO.md` 3.2.1:

1. **Sem teste executado** → Inconclusivo. Ler a quantidade de módulos não prova que eles retêm dados.
2. **Teste rápido sem divergência** → Inconclusivo. Um teste curto que passou não prova estabilidade.
3. **Uma execução com divergência** → Atenção. Pode ser falso positivo de condição passageira; a ação é
   repetir.
4. **Duas ou mais execuções com divergência** → Falha. O padrão do erro se repetiu, então ele é do módulo.

O que se conta é **execução** com divergência (um padrão distinto que produziu resultado errado), não palavra
divergente: contar palavras faria uma única execução muito errada virar Falha. O campo `ExecucoesComErro`
continua com padrão 1 no record por compatibilidade de serialização — isso é compatibilidade de dados, não
regra de veredito, e o runner sempre preenche o campo. A diferença que restou entre a regra publicada e
`Regras.Memoria` está registrada em `REGRAS-VEREDITO.md`, seção 3.10.

## D80 — Os limites térmicos ficam numa fonte única, `LimitesTermicos`

O mesmo número aparecia em pontos diferentes com valores diferentes. O veredito da CPU comparava com
**90 °C**; o gráfico destacava o alerta a partir de **95 °C**; o veredito de sensores usava **60 °C** para
qualquer disco (D73); a configuração prometia parar em **100 °C**. Nenhum desses valores estava errado quando
foi escolhido. O erro era de estrutura: não havia um lugar que dissesse qual número vale, então cada tela
respondia com o número que tinha à mão. O técnico via o gráfico avisar numa temperatura, o stress continuar em
carga e o laudo concluir outra coisa — e não tinha como saber qual dos três era o combinado.

A correção é `src/FlavosDiag.Core/Monitor/LimitesTermicos.cs`, no Core, sem dependência de Windows, WPF ou
biblioteca de sensores. Ele concentra três coisas:

1. **Constantes canônicas**, com o padrão de cada limite em um lugar só.
2. **Limites por tipo de componente**, incluindo a mídia do disco, calculados e não repetidos.
3. **A única regra de validação** da combinação, com correção e motivo.

Todos os consumidores passaram a ler daqui, sem literal próprio: `Regras.LimiteDoComponente` (que agora só
delega), os dois pontos de temperatura de disco em `Regras.DiscoIndividual`, o padrão de
`CargaCpuRunner.LimiteTemperaturaPadraoCelsius`, os padrões de `Configuracao`, a validação da tela de
Configurações e o limite da prova de GPU.

A regra de plausibilidade continua em `TemperaturaFisica`, e isso é deliberado: são perguntas diferentes.
`TemperaturaFisica` responde "este número é uma temperatura utilizável?", com a faixa 1–120 °C e as
sentinelas. `LimitesTermicos` responde "a partir de que valor utilizável isto vira alerta, veredito ou
parada?". Misturar as duas faria a emergência de 120 °C parecer um limite de segurança configurável, quando
ela é a fronteira de "isto não é medição" (D77). A ligação entre elas é explícita:
`EmergenciaCelsius = TemperaturaFisica.MaximoValidoCelsius`, para que nunca divirjam.

Para que a unificação não se desfaça na próxima alteração, um teste de arquitetura
(`tests/FlavosDiag.Tests/LimitesTermicosTests.cs`) percorre os `.cs` de `src/` e reprova literal numérico de
temperatura declarado fora de `LimitesTermicos` e `TemperaturaFisica`. A regra vale porque o projeto aceitou
perder a conveniência de um número solto: quem precisar de um limite novo acrescenta a constante certa, e o
mesmo teste obriga a documentá-la em `docs/LIMITES-TERMICOS.md`.

## D81 — O veredito térmico da CPU sobe de 90 para 95 °C

Um processador moderno sustenta 90 °C de projeto sob carga plena **sem estar com defeito**. O limite de
veredito de 90 °C tratava esse regime como Atenção, então máquina saudável saía reprovada no item térmico.
O custo disso não é um falso positivo isolado: um veredito que reprova o que está bem ensina o técnico a
ignorar o veredito, e o laudo inteiro perde o valor de discriminar.

Com a decisão, `VereditoCpuCelsius`, `AlertaVisualCelsius` e `ParadaAutomaticaCelsius` ficam todos em
**95 °C**. Os três no mesmo ponto não é coincidência: a invariante `veredito <= alerta <= parada < emergência`
existe para que a tela e o laudo apontem o mesmo lugar. A parada automática voltou de 100 para 95 °C por um
motivo concreto que a D72 deixou em aberto: com alerta em 95 e parada em 100, existia um intervalo em que o
gráfico já avisava e o teste continuava em carga total. Ou o aviso significa algo, ou não significa nada.

O limite continua configurável, e a faixa aceita é fechada por `ParadaMaximaCelsius = 110 °C`: parar só acima
disso é parar tarde demais para proteger o componente. O teto vale inclusive contra um `config.json` editado
à mão.

## D82 — Limite de disco por tipo de mídia, com procedência e rótulo "limite genérico"

A D73 separou o limite de cada componente, mas deixou o armazenamento com um único valor de 60 °C. O
armazenamento não é um componente homogêneo: HDD, SSD SATA e NVMe trabalham em regimes térmicos muito
diferentes. Um HDD a 80 °C está em sofrimento — problema mecânico iminente. Um NVMe a 80 °C pode estar
exatamente onde o controlador espera estar, e muitas unidades já limitam a própria velocidade por temperatura
antes de isso virar risco. Com o valor único, o aplicativo reprovava SSD e NVMe saudáveis e reprovava mídia
mecânica quente demais — os dois erros ao mesmo tempo, na mesma tela.

Os limites passam a ser HDD **55 °C**, SSD SATA **70 °C** e NVMe **80 °C**. Sobre eles, duas ressalvas que o
código e o laudo carregam juntos:

- **São valores de partida.** Nenhum dos três foi copiado de um datasheet; são pontos escolhidos para separar
  os três regimes. O laudo diz isso na própria frase do limite — "valor de partida — ajuste pela experiência
  de bancada e pela ficha do fabricante" — em vez de sugerir uma procedência que não existe.
- **O limiar do próprio componente tem precedência.** Quando a identificação ou o log do disco informa os
  limiares de aviso e crítico do controlador (no NVMe, os campos *warning* e *critical* do log SMART/Health),
  esses valores são preferidos como referência e a **fonte é registrada**. Sem esse dado, vale o padrão e ele
  sai rotulado **limite genérico**. A escolha entre os dois nunca é silenciosa: `LimiteTermico.Descrever()`
  sempre diz qual dos dois está em uso.

A forma de apresentar é parte da decisão, não enfeite. Um número do programa apresentado como se fosse do
fabricante é exatamente o tipo de afirmação que este projeto não faz — e é o mais difícil de perceber depois,
porque parece mais autorizada do que os outros números ao redor.

Hoje o modelo `SaudeNvme` ainda **não** carrega os campos de limiar do controlador, então todo NVMe é avaliado
pelo valor de partida e aparece como "limite genérico". O caminho de preferência está implementado e testado,
e passa a valer no dia em que o coletor trouxer o campo — o que falta é o dado, e o laudo diz que falta.

## D83 — Configuração térmica inválida é corrigida com motivo, nunca aceita em silêncio

A tela de Configurações aceitava o limiar de alerta e o limite de parada como dois campos independentes, cada
um com a sua faixa, e nunca comparava um com o outro. Dava para salvar alerta em 119 °C e parada em 60 °C. O
efeito: o stress parava em 60 °C e o gráfico nunca pintava a faixa de alerta, porque o limiar ficava acima de
tudo o que a máquina alcança. O técnico tinha a impressão de ter configurado uma proteção, e não tinha.

Uma proteção que o técnico acredita ter e não tem é pior do que proteção nenhuma, porque ninguém mais confere.
Por isso a validação trata os dois campos **juntos**: as invariantes são relações entre eles.

`LimitesTermicos.Validar` verifica `veredito <= alerta <= parada < emergência`, `parada <= 110 °C` e a faixa
utilizável de cada valor. Quando algo está errado, devolve os valores **corrigidos** — repondo ao padrão,
campo a campo, para que um número absurdo não derrube os que estavam corretos — e o **motivo em pt-BR** de cada
correção. Nunca aceita em silêncio, e nunca corrige em silêncio:

- na **tela de Configurações**, os motivos são mostrados, `LimiarAlertaValido` e `LimiteParadaValido` ficam
  em falso e **nada é salvo**. Aceitar o valor corrigido sem avisar trocaria um problema visível por um
  invisível: o técnico sairia da tela achando que a máquina vai parar em 119 °C;
- **fora da tela**, o valor inválido é reposto ao padrão e o motivo fica registrado, porque a alternativa é
  rodar carga com uma proteção que não funciona.

O resultado completo está publicado em `docs/LIMITES-TERMICOS.md`, que é a tabela canônica: limite, valor,
unidade, onde é aplicado, origem e justificativa. O documento e o código dizem a mesma coisa porque o teste de
arquitetura da D80 e os testes de invariante da D83 puxam cada um para o seu lado.

## D84 — Comando de disco vem de lista de permitidos, não do chamador

O Windows só repassa o pass-through ATA/NVMe a um disco aberto com `GENERIC_WRITE` (D31). Isso não
escreve nada — o comando enviado é de leitura — mas deixa o processo com permissão de escrita no disco
do cliente. Até aqui a única garantia disso era um comentário: bastava um bug futuro mandar `0x30`
(WRITE SECTORS) pelo mesmo caminho e o disco seria escrito. A permissão do handle é do driver; a escolha
do comando é nossa, e era a única parte que não estava fechada.

Agora ela está. A lista de permitidos (`ComandosDiscoPermitidos`) contém somente leitura: ATA
`IDENTIFY DEVICE` (0xEC), ATA `SMART READ DATA` (0xD0) — aceito tanto no registrador Command, forma
tarefa de `SMART_RCV_DRIVE_DATA` que o driver do Windows repassa e que o código sempre emitiu, quanto em
Features sob `SMART` (0xB0), forma canônica da ATA/ATAPI-8 — e NVMe `Get Log Page` (0x02) na página de
log SMART/Health (0x02). Os opcodes de `SMART READ THRESHOLDS` (0xD1), `READ LOG EXT` (0x2F), NVMe
`Identify` (0x06) e `Get Features` (0x0A) estão registrados como constantes nomeadas e documentadas,
mas **fora da lista em vigor**: nenhum chamador os usa hoje, e opcode aceito sem uso é superfície sem
necessidade. Incluir um comando novo passa a ser uma edição deliberada da lista, com justificativa.

O envio tem uma porta só: `RequisicaoDisco.Executar`. Ela recebe um **pedido enumerado**
(`PedidoAta`, `PedidoNvme`) e monta o task file internamente; não existe parâmetro de byte de comando,
opcode, feature ou task file em nenhuma sobrecarga. A ordem das conferências importa e agora é:
elevação, aceite do usuário, número do disco, **lista de permitidos** — e só então o `CreateFileW` com
escrita. A lista é conferida antes de abrir porque um pedido fora da enumeração não deve nem tocar o
dispositivo; a primeira versão conferia depois de abrir, e o teste pegou isso.

Sem elevação ou sem aceite, o disco não é aberto, o motivo vira `DadoAusente` e o veredito fica
Inconclusivo — nunca OK, porque nada foi lido. O aviso de leitura passou a existir de fato: antes dele
a leitura acontecia sem nenhuma confirmação, contrariando a regra do projeto de confirmar antes de agir
sobre o equipamento. O texto diz em maiúsculas que a consulta é **SOMENTE LEITURA** e que nada é
gravado no disco.

Os IOCTL de pass-through e os bytes de comando saíram de `ApiNativas` e existem apenas no caminho único:
é isso que dá sentido ao teste de arquitetura, que varre o `src/` e falha se outro arquivo citar
`\\.\PhysicalDrive`, os IOCTL de pass-through ou combinar `GENERIC_WRITE`. Dois riscos residuais ficam
registrados em `docs/RISCOS-LEGAIS.md` R6: o handle continua com permissão de escrita concedida pelo
driver, e um defeito dentro do próprio kernel sai do alcance do aplicativo.

**Divergência documentada (não alterada):** o código envia 0xD0 no registrador *Command*, enquanto a
ATA/ATAPI-8 põe 0xD0/0xD1 no registrador *Features* com Command 0xB0. A forma emitida é a que o driver
de disco do Windows repassa e a que a D31 registra; mudá-la agora seria mudar o comportamento em
produção para atender à leitura literal da norma, sem prova de que a outra forma funciona nos aparelhos.
A lista aceita as duas, e a divergência fica aqui registrada para quem for revisar a montagem.
## D85 — Sensor de CPU congelado sob carga vira `Suspeito`, não `SemLeitura`

Medido nesta máquina, no mesmo dia em que a D78 reviu a regra do `Min == Max`: o `GPU Core` devolve
46 °C com mínimo e máximo iguais em todas as leituras — leitura boa, que a regra antiga descartava.
Já um sensor de CPU parado no mesmo valor por dois minutos com a máquina a 90 % de carga não está
acompanhando a realidade.

Os dois casos têm a mesma aparência (valor parado) e significados opostos. A diferença está no
contexto, não no valor: componente que deveria esquentar, sob carga alta, por tempo demais.
`DetectorCongelamento` aplica os três critérios juntos — CPU, carga acima de 50 % e 120 s idênticos.

Enquanto suspeito, o valor **continua aparecendo** no gráfico e no laudo. O que ele perde é a
condição de confiança alta, então não pode sozinho disparar a parada automática nem sustentar um
veredito OK. Em repouso, estabilidade é normal e nada é marcado.

## D86 — A emergência acima do máximo físico exige 2 amostras e exclui apenas os sentinelas conhecidos

A D72 manda parar o teste quando a leitura passa de 120 °C. Só que 255 — a sentinela clássica de
"sem leitura" — é maior que 120: a regra, lida ao pé da letra, transformava ausência de leitura em
superaquecimento e abortava teste saudável. É exatamente o defeito que a D68 existia para evitar,
reintroduzido pela porta dos fundos.

A correção precisou separar duas perguntas que pareciam uma:

- `TemperaturaFisica.EhSentinela` responde **"isto é uma medição utilizável?"**. Para 130 °C a
  resposta é não.
- A emergência precisa da pergunta **"isto é um código fixo de erro?"**. Para 130 °C a resposta
  também é não — e é justamente o valor que ela precisa ver.

Reaproveitar a mesma função faria a emergência ignorar todo valor alto, que é o único caso em que
ela serve para alguma coisa. Por isso existem duas listas, e a da emergência é explicitamente a dos
códigos conhecidos (0, 255, -127, -128, NaN, infinito). Além disso a emergência passou a exigir
**duas amostras consecutivas**: um pico isolado não derruba o teste.

Vale para CPU e GPU. Sentinela conta como ausência de leitura e cai no teto sem sensor, nunca como
calor. A D77 continua valendo: leitura implausível não entra em máximo, média nem contagem do laudo.

## D87 — Toda carga pesada passa por proteção: o benchmark também

A D76 corrigiu a página CPU, mas a auditoria de todos os chamadores de carga mostrou que a lição não
tinha sido aplicada por inteiro. O benchmark é carga total em todos os núcleos por vários segundos e
rodava com o predicado `() => false` — ou seja, sem ler temperatura nenhuma — enquanto a tela que o
chamava passava `CancellationToken.None`.

Agora o benchmark recebe a leitura de sensores e um teto, aplica a mesma regra do estresse (parada
por temperatura sustentada e emergência por valor absurdo) e a tela abre sessão própria, respeita o
cancelamento e tem botão Parar.

A regra que fica registrada: existe **uma** coisa em comum entre estresse, benchmark, prova de GPU,
diagnóstico rápido e teste de memória — todos põem carga no equipamento do cliente. Nenhum deles pode
ser escrito sem responder às mesmas perguntas: quem lê a temperatura, quanto tempo no máximo, como o
técnico interrompe, e o que acontece quando a janela fecha.