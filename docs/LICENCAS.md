# Flavos Diag — Licenças e dependências

> Regra: **não use dependência com licença incerta ou incompatível com uso comercial.** Se incerta → implemente por conta própria ou marque BLOQUEADO.
> Este documento é revisado a cada fase. A data de verificação e a fonte (metadados do pacote NuGet / repositório oficial) estão registradas por item.

Data de verificação: 24/10/2026.

## 1. Dependências efetivamente usadas

| Pacote / ativo | Versão | Licença (verificada) | Onde é usado | Como é usado |
|---|---|---|---|---|
| WPF-UI (`WPF-UI`, lepo.co) | 4.2.1 | **MIT** — `license type=expression MIT` no nuspec | `FlavosDiag.App` | Tema Fluent (modo claro/escuro), controles base (`FluentWindow`, botões, etc.). Uso como dependência; nenhum trecho copiado. |
| CommunityToolkit.Mvvm (Microsoft) | 8.4.2 | **MIT** | `FlavosDiag.App`, ViewModels | `ObservableObject`, `RelayCommand`, geração de propriedades. Dependência. |
| QuestPDF (Marcin Ziąbek) | 2026.9.1 | **QuestPDF Community License v3.0** (source-available, **não é OSI**) | `FlavosDiag.Reports` | Geração do PDF do laudo. Ver item 2 abaixo (condição de elegibilidade). |
| LibreHardwareMonitorLib (LibreHardwareMonitor) | 0.9.7-pre771 | **MPL-2.0** — `license type=expression MPL-2.0` no nuspec | `FlavosDiag.Providers` | Leitura de sensores de temperatura/clock/potência/ventoinha. Dependência binária **não modificada**. Obrigações MPL: aviso de licença + disponibilização do código-fonte da biblioteca (link do repositório oficial). Nosso código não deriva dela, então a MPL não alcança o Flavos Diag (separação de arquivos). Texto incluído em `licenses/`. |
| DiskInfoToolkit (Blacktempel) | 2.2.2 | **MPL-2.0** (nuspec) | (transitivo da LHM, não chamado diretamente por nós) | Incluído na pasta de distribuição porque é dependência binária da LHM. |
| RAMSPDToolkit-NDD (Blacktempel) | 1.6.4 | **MPL-2.0** (nuspec) | (transitivo da LHM) | idem |
| HidSharp (James F. Bellinger) | 2.6.4 | **Apache-2.0** (arquivo `LICENSE.txt` no pacote; "HIDSharp now uses the Apache open-source license") | (transitivo da LHM) | idem |
| System.Management | 8.0.x (framework) / transitivo | **MIT** (Microsoft) | `FlavosDiag.Providers` | WMI/CIM. |
| Vortice.Direct3D11 (Amer Koleci) | 3.8.3 | **MIT** — `licenseExpression MIT` confirmado no registro do NuGet | `FlavosDiag.Providers` | Bindings da API pública Direct3D 11 do Windows, usados na prova de carga e detecção de artefatos da GPU. Dependência binária **não modificada**. |
| Vortice.DXGI | 3.8.3 | **MIT** | `FlavosDiag.Providers` | Enumeração de adaptadores e identificação da GPU. |
| Vortice.D3DCompiler | 3.8.3 | **MIT** | `FlavosDiag.Providers` | Compilação do shader HLSL próprio em tempo de execução. |
| Vortice.DirectX | 3.8.3 | **MIT** | `FlavosDiag.Providers` | Base comum dos bindings. Referenciado explicitamente porque, como dependência apenas transitiva, não era copiado para a pasta de saída. |
| System.IO.Ports | 10.0.13 (transitivo da LHM) | **MIT** (Microsoft) | transitivo | — |
| System.Threading.AccessControl / System.IO.FileSystem.AccessControl / System.Memory / Microsoft.Win32.Registry / Mono.Posix.NETStandard | diversas | MIT / MIT (Mono.Posix é MIT) | transitivos | — |
| Microsoft.Windows.CsWin32 *(candidato)* | 0.3.367 | **MIT** (Microsoft) | a avaliar em Fase 4 (GPU) | Gerador de P/Invoke. Se adotado, código gerado é nosso e não há atribuição requerida. |
| Fonte Inter (candidata) | 4.x | **SIL Open Font License 1.1** | `FlavosDiag.App` (empacotada) | OFL permite empacotamento e redistribuição; texto da OFL incluído em `licenses/`. Verificar se a fonte Geist (proposta no briefing) tem OFL — se sim, usar Geist; senão, Inter. |
| xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, coverlet | 2.9.x / 2.8.x / 17.x | **Apache-2.0** / **MIT** | `FlavosDiag.Tests` apenas (dev) | Não distribuído. |
| smartctl (smartmontools) *(opcional, Fase 2)* | 7.4 | **GPL-2.0** | `tools/` opcional, chamado como **processo separado** com `-j` | Só será empacotado se necessário e com texto GPL completo + oferta de código-fonte. Decisão em DECISOES.md. |

### 1.1 Transparência sobre pacotes consultados
O pacote `DiskInfoToolkit` (MPL-2.0) foi **baixado e inspecionado apenas como metadado de licença**. Não foi desmontado nem depurado. A regra de clean room (proibição de engenharia reversa de qualquer ferramenta de diagnóstico) é respeitada: qualquer uso é apenas como dependência transitiva do monitor de sensores, nunca para implementar SMART.

### 1.2 PawnIO — driver de terceiro **não redistribuído**

| Item | Situação |
|---|---|
| O que é | Driver de kernel de terceiros, distribuído separadamente pelo projeto PawnIO, usado pela biblioteca de sensores para acessar sensores em algumas plataformas. |
| Licença | **Termos próprios do projeto PawnIO.** Este documento **não declara** identificador de licença: a licença e os termos de uso precisam ser verificados no site oficial do projeto, `https://pawnio.eu`. |
| Onde é usado | Fora do Flavos Diag, como dependência opcional do ambiente (`LibreHardwareMonitorLib`). O app não contém nenhum binário do driver. |
| Como o Flavos Diag o trata | O app apenas **detecta** se o driver está instalado (presente, ausente ou desconhecido) e, quando a ausência é causa provável de falta de leitura, **exibe um comando sugerido** para o dono executar manualmente. Ver `DECISOES.md` D70 e D71. |

O Flavos Diag **não redistribui, não empacota, não instala e não baixa** o PawnIO. Não há cópia do instalador em `licenses/`, no pacote portátil nem na pasta de distribuição. A responsabilidade de verificar a licença e os termos antes de instalar é de quem instala — o dono do computador.

> A `LibreHardwareMonitorLib` informa, na própria documentação, que pode precisar desse driver para acessar sensores em algumas plataformas. Essa exigência é da biblioteca, não do Flavos Diag, e por isso o app se limita a reportar a ausência e a causa, sem tentar contorná-la.

## 2. QuestPDF Community License — condição de elegibilidade (IMPORTANTE)

Texto verificado no pacote `LICENSE.md` (versão 3.0, vigente em 06/07/2026). Resumo fiel das cláusulas relevantes:

- **Community License é gratuita** para: uso individual (incluindo projetos comerciais) com receita bruta anual abaixo do limite; aprendizado/avaliação; organizações beneficentes; instituições acadêmicas; projetos open source; **pequenas empresas com receita bruta anual abaixo de US$ 1.000.000**; usuários de dependência transitiva.
- **É permitido** construir aplicações comerciais e redistribuir a biblioteca como componente compilado da aplicação, sem royalties.
- **Entidades do setor público e empresas de capital aberto não são elegíveis**, independentemente da receita.
- A licença permite uso comercial **para quem se qualifica** — inclusive um MEI com faturamento abaixo de US$ 1 milhão/ano. **Confirmação necessária do dono:** a elegibilidade parte do próprio dono (autodeclaração), não de uma inferência do desenvolvedor. Se o dono qualificar, o uso e a redistribuição **são permitidos pela licença**. Se o faturamento passar de US$ 1 milhão, há 90 dias para comprar licença paga.
- A licença proíbe remover avisos proprietários da biblioteca; a redistribuição só é permitida como componente compilado.

**Classificação interna:** permitido para uso comercial com autodeclaração de receita < US$ 1M (ver `docs/RISCOS-LEGAL` item R2). O app não afirma "legalmente seguro"; a classificação acima é só uma leitura do texto da licença.

## 3. Ativos próprios (sem licença de terceiros)

| Ativo | Licença |
|---|---|
| Código do Flavos Diag | Proprietária do dono (ver `LICENSE`) |
| Ícones de traço (desenhados em `Path` XAML) | próprios |
| Paleta e layout | próprios |
| Cargas de CPU (`FlavosDiag.Workloads`) | próprias |
| Cena e shader de GPU | próprios |
| Fórmula de pontuação | própria |
| Textos da interface e do laudo | próprios |

## 4. Ferramentas de desenvolvimento

| Ferramenta | Licença | Nota |
|---|---|---|
| .NET SDK 8 (LTS) | MIT | compilador/runtime |
| WPF (parte do .NET) | MIT | runtime de interface |
| NuGet | Apache-2.0 | gerenciador |

## 5. Pendências de licença a verificar por fase
- [x] WPF-UI MIT (v4.2.1 confirmado no nuspec)
- [x] CommunityToolkit.Mvvm MIT
- [x] QuestPDF — condição de receita documentada
- [x] LibreHardwareMonitorLib MPL-2.0 + dependências
- [x] PawnIO — **não é dependência redistribuída**; termos próprios, sujeitos a verificação no site oficial (`https://pawnio.eu`) pelo dono. Ver 1.2.
- [ ] smartctl (se usado na Fase 2) — GPL-2.0 + obrigações completas
- [ ] Geist/Inter (Fase 5 — empacotamento de fonte)
- [ ] CsWin32 (se usado na Fase 4)
