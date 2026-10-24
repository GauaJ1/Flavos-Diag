# Relatório final — Flavos Diag

**Versão:** 0.5.0
**Estado:** fases 0 a 5 entregues, com as limitações abaixo declaradas
**Data:** 24/10/2026

---

## 1. O que foi construído

Um aplicativo Windows de bancada para diagnóstico de computadores, em .NET 8 com interface WPF, separado
de qualquer sistema web. A integração com outros sistemas se dá **exclusivamente pelo arquivo de laudo**
(PDF + JSON).

| Área | O que o programa faz |
|---|---|
| **Sistema** | Lê fabricante, modelo, número de série, placa-mãe, BIOS, CPU, memória, GPUs e monitores. |
| **Disco** | Lê saúde pelos atributos do próprio disco (ATA), pelo registro NVMe e pelos contadores do Windows, com veredito por disco. |
| **Memória** | Inventário dos pentes e teste rápido de retenção de dados, com 5 padrões próprios. |
| **CPU** | Inventário, teste de estresse com limite de 90 °C e benchmark de pontuação própria. |
| **GPU** | Prova de carga em Direct3D 11 fora de tela, com detecção de artefato por hash de pixels, e benchmark. |
| **Bateria** | Capacidade de projeto e atual, ciclos, saúde — com "não se aplica" honesto em desktop. |
| **Rede** | Adaptadores, gateway, DNS, latência e perda. |
| **Checklist** | Itens de teste manual marcados pelo técnico e levados ao laudo. |
| **Laudo** | PDF + JSON versionado, com veredito por categoria, evidências, limitações e recomendações. |

## 2. Princípios que foram mantidos

| Princípio | Como foi cumprido |
|---|---|
| Somente leitura | Nenhuma escrita em registro, serviço, driver ou setor de disco. Os únicos arquivos escritos são configuração/laudos na pasta do usuário e o arquivo temporário do teste de disco, apagado ao final. |
| Dado ausente ≠ OK | Toda leitura é um `Leitura<T>` com estado e causa. Ausência vira Inconclusivo com motivo visível. |
| Sem invenção | Nenhum número, tela ou botão fictício fora do modo demonstração, que é explícito e marcado no laudo. |
| Sem telemetria | Nenhuma conexão para fora, exceto para alvos configurados pelo técnico. |
| Privacidade | Números de série mascarados por padrão. |
| Segurança do equipamento | Cargas exigem confirmação, têm limite de temperatura, parada manual e teto de 2 min sem sensor. |
| Originalidade | Cargas, shaders, pontuação, textos e ícones são próprios. |

## 3. Evidência de verificação

| Verificação | Comando | Resultado |
|---|---|---|
| Build de release estrito | `dotnet build FlavosDiag.sln -c Release -warnaserror` | 0 avisos, 0 erros |
| Testes automatizados | `build/test.ps1` | 93 de 93 aprovados |
| Sala limpa | `build/cleanroom.ps1` | nenhum achado |
| Codificação | `tools/ChecagemEncoding` | 0 arquivos com problema |
| Pacotes vulneráveis | `dotnet list ... --vulnerable --include-transitive` | nenhum |
| Pacote portátil | `build/publish.ps1` | 306 arquivos, 174 MB, 11 licenças |
| Execução do pacote | abrir o `.exe` da pasta `dist` | janela abriu e permaneceu |
| Prova de GPU em hardware real | roteiro de verificação | 794 quadros, 0 divergências (AMD Radeon 740M) |

## 4. Limitações declaradas

Estas não são suposições: são itens que **não foram verificados**, com o motivo.

| Limitação | Motivo |
|---|---|
| Sensores de temperatura com elevação | nenhum teste foi executado como administrador |
| Temperatura da GPU | sensores não responderam sem elevação |
| Atributos SMART ATA | o controlador desta máquina recusou o comando |
| Disco NVMe | a máquina não tem unidade NVMe |
| Bateria de notebook | o equipamento é um desktop |
| Estresse de CPU e teste de memória em hardware | proibido executar autonomamente por regra do projeto |
| Detecção de artefato em GPU defeituosa | não há GPU defeituosa disponível |
| Reinício de driver (TDR) real | não ocorreu |
| Instalação em máquina sem .NET | o teste rodou em máquina que já tem o runtime |
| SmartScreen e antivírus | executável não assinado |

O passo a passo para fechar cada um está em `docs/ROTEIRO-TESTE-MANUAL.md`.

## 5. Defeitos reais encontrados pela verificação

Vale registrar porque mostra que a verificação não foi decorativa. Todos foram encontrados por execução
real, não por revisão de código:

1. **Toda GPU seria reprovada.** A primeira versão do shader variava com o número do quadro, então cada
   quadro divergia por projeto: 806 divergências em 806 quadros. Após a correção, 0 em 794.
2. **A causa da falha da GPU era descartada**, e a categoria ficaria "Inconclusivo" sem motivo — violando
   a regra de dado ausente com causa.
3. **O estresse de CPU não fazia trabalho algum** e o limite de temperatura nunca era avaliado: a proteção
   do equipamento estava inerte.
4. **A amostragem de temperatura comparava dois cronômetros diferentes**, e a diferença negativa bloqueava
   toda amostragem depois da primeira.
5. **O botão "Parar" virava falha** em vez de registrar o motivo da parada.
6. **Sete telas eram esqueletos** com promessas de recurso futuro, algumas citando fases já concluídas.
7. **O pacote de distribuição ia inteiro para o Git** (174 MB).

## 6. Enquadramento de origem

Nenhum programa de diagnóstico de terceiros foi desmontado, depurado, inspecionado ou usado como
referência de código, texto, ícone ou layout. Não havia software de terceiros a copiar e nada foi copiado.

As fontes usadas foram: documentação oficial da Microsoft (Win32, Direct3D 11, DXGI, WMI/CIM, ACPI,
powercfg), padrões públicos (ATA/SMART, NVMe, SMBIOS, PCI), documentação das bibliotecas de código aberto
adotadas, e técnicas gerais de programação. Cada item está registrado em `docs/PROCEDENCIA.md` com a
marca de documento público ou decisão própria.

As bibliotecas de terceiros entram apenas como **dependências binárias não modificadas**, com licença
verificada e texto incluído na distribuição: ver `docs/LICENCAS.md`.

> **Aviso:** este relatório descreve o que foi feito e como. Ele não constitui parecer jurídico e não
> afirma que o resultado seja "legalmente seguro" — essa avaliação cabe a um profissional habilitado.
> O documento `docs/RISCOS-LEGAIS.md` lista os pontos que merecem essa análise.

## 7. Cinco próximos passos

1. **Rodar o roteiro manual em equipamentos variados** — pelo menos um notebook com bateria, um com NVMe e
   uma máquina em que o controlador aceite a leitura de atributos SMART. É o que fecha a maior parte das
   limitações da seção 4, e cada item só muda de status com o resultado observado anotado.

2. **Verificar com elevação** e decidir, com os dados na mão, se o programa deve pedir elevação na
   inicialização ou continuar mostrando "Inconclusivo" nos itens que dependem de privilégio. A decisão
   atual (não pedir) é a mais segura, mas custa informação ao técnico.

3. **Assinar o executável.** Sem assinatura, o SmartScreen avisa na primeira execução e o técnico precisa
   explicar isso ao cliente. Um certificado resolve o atrito e é pré-requisito de distribuição ampla.

4. **Criar um instalador e um canal de atualização manual** (versão + changelog no laudo). Hoje o pacote é
   portátil e o técnico copia a pasta; um instalador simplifica o uso em bancada com vários computadores,
   e o número de versão no laudo permite rastrear qual build gerou cada resultado.

5. **Transformar as limitações em indicadores no laudo** — uma faixa explícita "o que este laudo não
   cobre" no PDF, gerada automaticamente a partir da lista de limitações. O técnico já vê isso no app; o
   cliente, que só recebe o PDF, hoje depende de o técnico repassar a informação.
