# Flavos Diag

Ferramenta de **bancada** para diagnóstico rápido de computadores Windows, feita para uso de técnico de
informática (MEI). Ela **lê** o equipamento, **mede** o desempenho com cargas próprias e **conclui** com um
veredito por categoria, entregue em um laudo em PDF e JSON.

O Flavos Diag é independente de qualquer sistema web. A integração com outros sistemas acontece **apenas
pelo arquivo de laudo** — não há banco de dados compartilhado nem API.

---

## Princípios (o que o programa não faz)

| Princípio | Como aparece |
|---|---|
| **Somente leitura por padrão** | Não altera registro, serviços, drivers, arquivos do sistema nem setores de disco. Não existe otimização, limpeza ou "reparo automático". |
| **Dado ausente ≠ OK** | Todo valor é uma leitura com estado (disponível, não suportado, sem permissão, erro) e motivo. O que não foi medido aparece como **Inconclusivo**, nunca como aprovado. |
| **Sem invenção** | Nenhum número de exemplo, nenhuma tela falsa, nenhum botão sem efeito. |
| **Sem telemetria** | O programa não envia dados pela internet. |
| **Privacidade** | Números de série mascarados no laudo por padrão. |
| **Segurança do equipamento** | Testes de carga exigem confirmação, têm limite de temperatura com parada automática, botão de parada e duração máxima. Sem sensor de temperatura, o teto é de 2 minutos. |

---

## Requisitos

- Windows 10 ou 11, 64 bits.
- **Nenhuma instalação necessária** para usar: o pacote distribuído é autocontido.
- Para compilar: .NET 8 SDK.

---

## Como usar (pacote portátil)

1. Gere o pacote (ou peça o já gerado):

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build\publish.ps1
   ```

   A pasta aparece em `dist\FlavosDiag-<versao>-win-x64\`.

2. Copie a pasta inteira para o computador ou pendrive e abra `FlavosDiag.App.exe`.

3. Para ler mais dados (temperatura de sensores e alguns dados de disco), execute **como administrador**.
   Sem elevação, o programa funciona em modo reduzido e diz o motivo em cada item que não pôde ser lido.

4. Rode o **diagnóstico rápido**, confira as abas e gere o **laudo**.

---

## Compilar e testar

```powershell
# build
dotnet build FlavosDiag.sln -c Release --nologo

# testes
powershell -ExecutionPolicy Bypass -File .\build\test.ps1

# varredura de sala limpa (nomes proibidos, arquivos de terceiros indevidos)
powershell -ExecutionPolicy Bypass -File .\build\cleanroom.ps1
```

> Observação: o executor de testes é próprio (runner por reflexão dentro do projeto de testes), porque o
> `dotnet test` padrão não consegue iniciar o host de testes neste ambiente. A execução imprime a lista de
> casos e o total; a saída completa é o que conta como evidência.

---

## Organização do código

| Projeto | Responsabilidade |
|---|---|
| `FlavosDiag.Core` | Modelos, `Leitura<T>`, regras de veredito, formatação pt-BR, mascaramento. Sem dependência de Windows. |
| `FlavosDiag.Providers` | Coletores reais (WMI, APIs do Windows, Direct3D, sensores) atrás de interfaces. |
| `FlavosDiag.Workloads` | Cargas próprias de CPU, memória e disco, com verificação de resultado. |
| `FlavosDiag.Reports` | Geração de PDF e JSON. |
| `FlavosDiag.App` | Interface WPF (MVVM). |
| `FlavosDiag.Tests` | Testes automatizados com dados sintéticos controlados. |

Fluxo de dependência: `App → Providers → Core` e `App → Reports → Core`. O app nunca chama API do Windows
diretamente.

---

## Documentação

| Documento | Conteúdo |
|---|---|
| [Especificação](docs/ESPECIFICACAO.md) | O que o programa faz, recurso por recurso |
| [Regras de veredito](docs/REGRAS-VEREDITO.md) | Como cada categoria vira OK/Atenção/Falha/Inconclusivo |
| [Procedência](docs/PROCEDENCIA.md) | De onde vem cada técnica (documentação pública ou decisão própria) |
| [Licenças](docs/LICENCAS.md) | Bibliotecas usadas e as obrigações de cada licença |
| [Riscos legais](docs/RISCOS-LEGAIS.md) | Enquadramento e limites do que foi feito |
| [Decisões](docs/DECISOES.md) | Cada escolha de implementação e o motivo |
| [Schema do laudo](docs/SCHEMA-LAUDO.md) | Estrutura do JSON gerado |
| [Roteiro de teste manual](docs/ROTEIRO-TESTE-MANUAL.md) | O que ainda precisa de verificação humana |
| [Progresso](PROGRESS.md) | O que foi entregue e a evidência de cada fase |
| [Relatório final](docs/RELATORIO-FINAL.md) | Situação, limitações e próximos passos |

---

## Limitações conhecidas

Estas são reais e estão detalhadas no roteiro de teste manual:

- Temperatura de sensores e de GPU **podem exigir execução como administrador**; sem elevação aparece
  Inconclusivo com motivo.
- Em alguns controladores de disco, a leitura dos atributos do próprio disco é recusada; o programa usa
  então o estado de saúde informado pelo Windows e registra a limitação.
- Unidades NVMe e baterias de notebook **não puderam ser verificadas** no equipamento de desenvolvimento
  (desktop, discos SATA).
- O executável não é assinado digitalmente: o SmartScreen pode avisar na primeira execução.

---

## Aviso

Este programa **não substitui backup** e não autoriza deixar de fazer cópia de segurança. As pontuações de
benchmark são **próprias do Flavos Diag** e **não são comparáveis** com as de outros programas.
