# Flavos Diag — Riscos legais (para o dono revisar com um profissional)

> Este documento **não** afirma que nada é "legalmente seguro". Ele lista pontos de atenção. A decisão final é do dono do negócio, idealmente com um advogado.
> Classificação de cada item: **REVISAR** (precisa de decisão humana), **MONITORAR** (risco baixo, mas acompanhar), **RESOLVIDO INTERNAMENTE** (mitigado no projeto).

## R1 — Clean room e engenharia reversa — RESOLVIDO INTERNAMENTE
- **Regra:** nenhuma ferramenta de RE foi usada; nenhum binário de terceiro foi decompilado, desmontado ou depurado; nenhum código-fonte proprietário foi lido.
- **Situação:** o desenvolvimento seguiu apenas documentação Microsoft, padrões públicos e documentação de bibliotecas. Verificado: nenhuma ferramenta de RE executada no ambiente.
- **Risco residual:** o comportamento de ferramentas conhecidas (ex.: inventário, SMART) é genérico e amplamente documentado; as cargas e pontuações são nossas. Ainda assim, recomenda-se que o dono faça uma revisão independente do código antes da distribuição.

## R2 — QuestPDF Community License — REVISAR (decisão do dono)
- A Community License é **gratuita para negócios com receita bruta anual abaixo de US$ 1 milhão**, inclusive para uso comercial, e permite redistribuir a biblioteca compilada na aplicação. Entidades públicas e empresas de capital aberto não são elegíveis. Se o dono deixar de se qualificar, há um prazo de 90 dias para comprar a licença.
- **Ação do dono:** confirmar (autodeclaração) que a receita do MEI está abaixo do limite em dólares e documentar essa declaração (data e receita considerada). A elegibilidade é autodeclarada ao fornecedor da biblioteca.
- **Alternativa:** se o dono preferir não depender de licença que não é OSI, é possível trocar o gerador de PDF por uma biblioteca MIT/Apache ou implementação própria (custo: reescrever a geração do PDF). O código isola a geração de PDF em `FlavosDiag.Reports`.

## R3 — LibreHardwareMonitorLib (MPL-2.0) — MONITORAR
- Uso como dependência binária, sem modificação, com texto MPL-2.0 e aviso de disponibilidade do código-fonte incluídos na distribuição. O Flavos Diag não deriva dela, então não há obrigação de abrir nosso código.
- **Atenção:** a biblioteca carrega driver de kernel para sensores. Distribuí-la pode gerar falso positivo de antivírus. Recomenda-se assinatura de código (R5) e testar com os antivírus mais usados.
- **Ação:** manter a versão pinada em `docs/LICENCAS.md` e conferir se o pacote em si não embute binários de licença diferente.

## R4 — Nomes e marcas na interface — RESOLVIDO INTERNAMENTE
- A interface, textos, ícones e layout são nossos. Não há citação de marcas de ferramentas de diagnóstico na UI, no código ou em comentários. Nomes de fabricantes de hardware (Intel, AMD, NVIDIA, Samsung etc.) aparecem apenas como **dados lidos do equipamento**, o que é uso nominativo/informativo, não identidade visual.
- **Risco residual:** recomendações do tipo "procure a assistência da marca X" usam o nome da marca como referência factual. Revisar se o dono quer usar *nomes de marcas lidas do hardware* em recomendações do laudo.

## R5 — Distribuição sem assinatura de código — MONITORAR
- Sem certificado de assinatura, o Windows SmartScreen e antivírus podem mostrar alertas ou bloquear o executável. Testes de estresse e leitura de sensores (driver) aumentam falsos positivos.
- **Ação recomendada:** adquirir certificado de assinatura de código (EV ou OV) e assinar o `.exe` antes da distribuição; manter hash SHA-256 publicado para conferência; preparar um "como liberar no antivírus" no README.

## R6 — "chkdsk /scan" e SMART só leitura — MONITORAR
- O app executa somente varredura somente leitura de sistema de arquivos (`chkdsk /scan`) e leitura de SMART via drivers do sistema.
- **Risco:** mesmo somente leitura, leitura intensiva em disco degradado pode agravar a falha. Mitigado pelo aviso obrigatório de backup, pela possibilidade de cancelar e pelo uso somente leitura.
- **Ação:** o aviso ao usuário deixa claro que a responsabilidade por backup é do técnico/cliente.
- **Controle adicional (T6/D78) — pass-through por lista de permitidos.** A leitura SMART/NVMe via `DeviceIoControl` exige que o disco seja aberto com `GENERIC_WRITE` (ver D31): o driver de armazenamento do Windows recusa o pass-through em handle somente leitura. Isso significa que o processo tem, naquele handle, permissão de escrita no disco do cliente — e a única coisa que impede um comando de escrita de ser enviado é o próprio código. Por isso o envio passou a ser fechado:
  - Existe **uma** lista de comandos permitidos (`ComandosDiscoPermitidos`), com apenas comandos de leitura: `IDENTIFY DEVICE` (0xEC), `SMART READ DATA` (0xD0) e `NVMe Get Log Page` (0x02). Escrita de setores (0x30), `SMART WRITE LOG` (0xD6) e afins não estão na lista.
  - Existe **uma** função (`RequisicaoDisco.Executar`) que monta o pedido. Ela recebe uma enumeração fechada, **não** um byte de comando: não há como passar um opcode arbitrário de fora.
  - A conferência da lista acontece **antes** de abrir o disco; sem elevação ou sem o aceite do usuário, o disco não é aberto e o veredito fica Inconclusivo com a causa.
  - Nenhum outro arquivo do `src/` abre `\\.\PhysicalDriveN` com escrita nem cita os IOCTL de pass-through; um teste de arquitetura varre o código-fonte e falha se isso acontecer.
- **Risco residual:** o handle ainda tem permissão de escrita (limitação do driver, não escolha nossa). O controle reduz a superfície a três comandos de leitura e impede a abertura fora do caminho único; ele **não** altera a permissão concedida pelo Windows ao handle. Um defeito no próprio kernel/driver continua fora do alcance do aplicativo.

## R7 — Testes de estresse em computadores de clientes — REVISAR
- Mesmo com limite de temperatura e duração, estresse pode revelar (ou causar) falhas em equipamentos em condição limítrofe. Um equipamento sem sensor de temperatura fica limitado a 2 minutos.
- **Ação do dono:** ter termo de consentimento/OS assinado pelo cliente antes de rodar estresse; documentar no laudo qual foi o teste.
- **Já implementado:** confirmação explícita antes de iniciar, botão de parada, logs de temperatura.

## R8 — LGPD / dados pessoais no laudo — REVISAR
- O laudo contém números de série, nome do computador, nome do técnico, número da OS e fotos do checklist. Números de série não identificam uma pessoa física por si só, mas podem identificar o aparelho de um cliente.
- **Já implementado:** mascaramento de seriais por padrão; dados gravados só em `%LOCALAPPDATA%` e na pasta de exportação escolhida; sem telemetria.
- **Ação do dono:** definir política de retenção dos laudos/fotos e base legal para tratamento dos dados (execução de contrato de serviço).

## R9 — Referências a normas privadas — MONITORAR
- Algumas ferramentas de diagnóstico citam padrões de fabricantes (ex.: identificação de chipsets de SSD, decodificação de atributos proprietários). O Flavos Diag **não** implementa decodificação proprietária de fabricante; exibe apenas valores brutos e atributos padronizados pelo ATA/NVMe.
- **Ação:** manter assim.

## R10 — Uso de bibliotecas de código aberto em produto comercial — MONITORAR
- Todas as licenças foram verificadas e estão em `docs/LICENCAS.md`. Revisar antes da distribuição se surgirem novas versões (versão pinada nos `.csproj`).
- **Ação:** rodar `dotnet list package --include-transitive --vulnerable` na entrega e revisar dependências transitivas.

## R11 — Driver de kernel de terceiro instalável (PawnIO) — REVISAR (decisão do dono)
- O Flavos Diag **não empacota, não instala e não baixa** o PawnIO. Ele apenas detecta se o driver está presente e, quando a ausência é causa provável da falta de temperatura, exibe um comando sugerido (`winget install namazso.PawnIO -e`) com botão Copiar. **Quem decide instalar e quem executa o comando é o dono** do computador. Ver `DECISOES.md` D71 e `LICENCAS.md` 1.2.
- Um driver de kernel tem acesso privilegiado ao sistema. Os termos e a licença do PawnIO são próprios do projeto e devem ser verificados no site oficial: `https://pawnio.eu`.
- **Ação do dono:** ler os termos no site oficial antes de instalar; decidir se a instalação entra no procedimento de bancada e se o técnico precisa de autorização do cliente para isso.

## R12 — Memória protegida (Isolamento de núcleo / HVCI) e antivírus — MONITORAR
- Alguns drivers de kernel **não carregam** quando a integridade de memória (Isolamento de núcleo / HVCI) está habilitada no Windows, e drivers de terceiros podem ser sinalizados por software de segurança. Isso **precisa ser verificado pelo dono na própria máquina**; este documento **não afirma** que funciona nem que falha.
- Mitigação: o app informa o estado do PawnIO como **presente, ausente ou desconhecido**, em vez de adivinhar. Um driver instalado que não carrega aparece como estado desconhecido, com a causa provável no texto, e não como "não instalado".
- **Ação:** registrar o estado da integridade de memória e do antivírus na máquina de teste e anotar o resultado em `ROTEIRO-TESTE-MANUAL.md`.

## R13 — Execução com privilégio de administrador — RESOLVIDO INTERNAMENTE
- Para ler sensores, o app **pede** ao usuário que o reabra como administrador (percursos com `runas`, D24/D70). Ele **nunca se eleva sozinho e em silêncio**: a elevação é uma ação explícita do usuário, e recusá-la leva ao modo reduzido com a causa escrita na tela.
- Instalar driver, executar o comando sugerido e baixar arquivos estão fora do que o app faz com qualquer nível de privilégio.

## R14 — Diagnóstico de sensores exportado — REVISAR
- O arquivo `.json`/`.txt` do "Exportar diagnóstico de sensores" (D74) contém modelo de hardware, versão do Windows, estado do PawnIO e **identificadores dos sensores**. Não contém números de série (os identificadores passam pelo mascaramento existente) nem dados pessoais.
- O arquivo é gravado **localmente**, na pasta escolhida pelo técnico. Nada é enviado para fora. **Compartilhar o arquivo é escolha do dono.**
- **Ação do dono:** decidir a política de retenção e para quem o arquivo pode ser enviado (ex.: suporte da biblioteca de sensores).

## R15 — Estresse térmico em carga máxima — REVISAR
- O estresse agora tem proteções automáticas (parada por temperatura de confiança alta, teto sem sensor, teto de duração — ver `DECISOES.md` D72). Ainda assim, rodar carga máxima **carrega risco físico** ao equipamento. Os limites são redes de segurança, **não garantias**: um sensor que não mede não protege, e é por isso que a ausência de leitura reduz o teste a 2 minutos em vez de liberá-lo.
- **Ação do dono:** ver R7 (termo de consentimento). Os limites não substituem a autorização do dono do equipamento nem o acompanhamento do técnico.

## Itens a confirmar com o dono (checklist seguro)
- [ ] R2 — elegibilidade QuestPDF (receita bruta < US$ 1M/ano).
- [ ] R5 — certificado de assinatura de código.
- [ ] R7 — termo de consentimento para estresse.
- [ ] R8 — política de retenção de dados do cliente.
- [ ] R11 — termos de licença do PawnIO verificados no site oficial e decisão sobre a instalação no procedimento de bancada.
- [ ] R14 — política de retenção e compartilhamento do diagnóstico de sensores exportado.
- [ ] Revisão jurídica geral do código e dos laudos.
