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
