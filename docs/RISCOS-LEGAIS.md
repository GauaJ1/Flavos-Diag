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

## Itens a confirmar com o dono (checklist seguro)
- [ ] R2 — elegibilidade QuestPDF (receita bruta < US$ 1M/ano).
- [ ] R5 — certificado de assinatura de código.
- [ ] R7 — termo de consentimento para estresse.
- [ ] R8 — política de retenção de dados do cliente.
- [ ] Revisão jurídica geral do código e dos laudos.
