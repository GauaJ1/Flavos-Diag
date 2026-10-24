using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Core.Vereditos;

/// <summary>Resultado de uma regra: veredito + resumo + evidências + recomendações.</summary>
public sealed record RespostaRegra
{
    public required Veredito Veredito { get; init; }

    public required string Resumo { get; init; }

    public IReadOnlyList<Evidencia> Evidencias { get; init; } = Array.Empty<Evidencia>();

    public IReadOnlyList<Recomendacao> Recomendacoes { get; init; } = Array.Empty<Recomendacao>();

    public IReadOnlyList<string> Limitacoes { get; init; } = Array.Empty<string>();
}

/// <summary>Regras de veredito — docs/REGRAS-VEREDITO.md §3.</summary>
public static class Regras
{
    public const string CategoriaSistema = "Sistema";
    public const string CategoriaMemoria = "Memória";
    public const string CategoriaDisco = "Disco";
    public const string CategoriaSensores = "Sensores";
    public const string CategoriaCpu = "CPU";
    public const string CategoriaGpu = "GPU";
    public const string CategoriaBateria = "Bateria";
    public const string CategoriaRede = "Rede";
    public const string CategoriaChecklist = "Checklist";

    // ---------------- Sistema ----------------
    public static RespostaRegra Sistema(InventarioSistema inv)
    {
        if (inv.Sistema.Nome.Ausente && inv.Cpu.Nome.Ausente)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = "Inventário essencial indisponível (SO e CPU não lidos).",
                Limitacoes = new[] { inv.Sistema.Nome.Causa ?? "SO não lido", inv.Cpu.Nome.Causa ?? "CPU não lida" },
            };
        }

        var problemas = inv.DispositivosComProblema;
        var evidencias = new List<Evidencia>
        {
            new() { Titulo = "Dispositivos com erro", Valor = problemas.Count.ToString(), Fonte = "Win32_PnPEntity" },
        };

        if (problemas.Count == 0)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Ok,
                Resumo = "Sistema inventariado sem dispositivos com erro.",
                Evidencias = evidencias,
            };
        }

        foreach (var p in problemas.Take(10))
        {
            evidencias.Add(new Evidencia { Titulo = "Código " + p.CodigoErro, Valor = p.Nome, Fonte = p.DeviceId });
        }

        return new RespostaRegra
        {
            Veredito = Veredito.Atencao,
            Resumo = $"{problemas.Count} dispositivo(s) com erro no Gerenciador de Dispositivos.",
            Evidencias = evidencias,
            Recomendacoes = new[]
            {
                new Recomendacao
                {
                    Acao = "Verificar os dispositivos listados: atualizar o driver pela página oficial do fabricante do equipamento ou testar em outro slot/porta.",
                    Prioridade = PrioridadeRecomendacao.Recomendada,
                    Justificativa = "Código de erro do Gerenciador de Dispositivos indica problema de driver ou conexão, não necessariamente defeito de hardware.",
                },
            },
        };
    }

    // ---------------- Memória ----------------
    public static RespostaRegra Memoria(InfoMemoria mem, ResultadoTesteMemoria? teste = null)
    {
        if (teste is { ErrosDetectados: > 0 })
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Falha,
                Resumo = $"Teste rápido de padrões encontrou {teste.ErrosDetectados} divergência(s) na memória.",
                Evidencias = new[] { new Evidencia { Titulo = "Erros de padrão", Valor = teste.ErrosDetectados.ToString() } },
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Executar o diagnóstico de memória nativo completo (reinicia o computador) e testar os módulos individualmente.",
                        Prioridade = PrioridadeRecomendacao.Urgente,
                        Justificativa = "Divergência de padrão indica memória instável.",
                    },
                },
            };
        }

        if (mem.TotalBytes.Ausente)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = "Não foi possível ler os módulos de memória.",
                Limitacoes = new[] { mem.TotalBytes.Causa ?? "WMI não retornou módulos" },
            };
        }

        var total = mem.TotalBytes.Valor;
        var atencao = total < 4L * 1024 * 1024 * 1024;
        var evidencias = new List<Evidencia>
        {
            new() { Titulo = "Memória total", Valor = FormatacaoBr.Gib(total / 1024.0 / 1024 / 1024), Fonte = "Win32_PhysicalMemory" },
        };

        if (mem.Modulos.Any(m => m.CapacidadeBytes.Ausente || m.CapacidadeBytes.Valor <= 0))
        {
            atencao = true;
            evidencias.Add(new Evidencia { Titulo = "Módulo ilegível", Valor = "Um ou mais módulos sem capacidade válida" });
        }

        return new RespostaRegra
        {
            Veredito = atencao ? Veredito.Atencao : Veredito.Ok,
            Resumo = atencao
                ? (total < 4L * 1024 * 1024 * 1024
                    ? "Memória abaixo de 4 GiB pode limitar o uso do computador."
                    : "Foram encontrados módulos com leitura inconsistente.")
                : $"Memória total de {FormatacaoBr.Gib(total / 1024.0 / 1024 / 1024)} lida com sucesso.",
            Evidencias = evidencias,
        };
    }

    // ---------------- Disco ----------------
    public static RespostaRegra Disco(IReadOnlyList<DiscoInfo> discos, IReadOnlyList<ResultadoVarreduraSuperficie>? varreduras = null)
    {
        if (discos.Count == 0)
        {
            return new RespostaRegra { Veredito = Veredito.Inconclusivo, Resumo = "Nenhum disco físico detectado." };
        }

        var pior = Veredito.NaoSeAplica;
        var resumos = new List<string>();
        var evidencias = new List<Evidencia>();
        var recomendacoes = new List<Recomendacao>();
        var limitacoes = new List<string>();

        foreach (var d in discos)
        {
            var r = DiscoIndividual(d, varreduras?.FirstOrDefault(v => v.CaminhoArquivo.StartsWith(d.Numero.ToString(), StringComparison.Ordinal)));
            pior = VereditoTexto.MaisGrave(pior, r.Veredito);
            resumos.Add(r.Resumo);
            evidencias.AddRange(r.Evidencias);
            recomendacoes.AddRange(r.Recomendacoes);
            limitacoes.AddRange(r.Limitacoes);
        }

        return new RespostaRegra
        {
            Veredito = pior,
            Resumo = string.Join(" ", resumos),
            Evidencias = evidencias,
            Recomendacoes = recomendacoes,
            Limitacoes = limitacoes,
        };
    }

    public static RespostaRegra DiscoIndividual(DiscoInfo d, ResultadoVarreduraSuperficie? varredura = null)
    {
        var nome = d.Modelo.Disponivel ? d.Modelo.Valor! : $"Disco {d.Numero}";
        var saude = d.Saude;
        var evidencias = new List<Evidencia>();
        var recomendacoes = new List<Recomendacao>();
        var limitacoes = saude.Limitacoes.Select(l => $"{nome}: {l.Motivo}").ToList();

        var falha = new List<string>();
        var atencao = new List<string>();

        var ata = saude.AtributosAta.ToDictionary(a => a.Id, a => a.Bruto);
        long Bruto(int id) => ata.TryGetValue(id, out var v) ? v : -1;

        if (saude.Fontes.Contains(FonteSaudeDisco.SmartAta))
        {
            CheckFalha(5, "setores realocados");
            CheckFalha(197, "setores pendentes");
            CheckFalha(198, "setores não corrigíveis");
            CheckFalha(187, "erros não corrigíveis");

            void CheckFalha(int id, string rotulo)
            {
                var v = Bruto(id);
                if (v > 0)
                {
                    falha.Add($"{rotulo} = {v}");
                    evidencias.Add(new Evidencia { Titulo = "SMART " + id, Valor = $"{rotulo}: {v}", Fonte = "SMART ATA" });
                }
            }

            var vida = Bruto(231);
            if (vida >= 80 && vida <= 100)
            {
                atencao.Add($"vida do SSD usada {vida} %");
            }

            var temp = Bruto(194) & 0xFF;
            if (temp >= 60)
            {
                atencao.Add($"temperatura {temp} °C");
            }
        }

        if (saude.Nvme is { } nvme)
        {
            if (nvme.AvisoCritico.Disponivel && nvme.AvisoCritico.Valor != 0)
            {
                falha.Add($"aviso crítico NVMe = {nvme.AvisoCritico.Valor}");
                evidencias.Add(new Evidencia { Titulo = "NVMe aviso crítico", Valor = nvme.AvisoCritico.Valor.ToString() });
            }

            if (nvme.ErrosIntegridadeMidia.Valor > 0)
            {
                falha.Add($"erros de integridade de mídia = {nvme.ErrosIntegridadeMidia.Valor}");
                evidencias.Add(new Evidencia { Titulo = "NVMe erros de mídia", Valor = nvme.ErrosIntegridadeMidia.Valor.ToString() });
            }

            if (nvme.PercentualVidaUsada.Valor >= 100)
            {
                falha.Add("vida usada 100 %");
            }
            else if (nvme.PercentualVidaUsada.Valor >= 80)
            {
                atencao.Add($"vida usada {nvme.PercentualVidaUsada.Valor} %");
            }

            if (nvme.EspacoDisponivelPercentual.Disponivel && nvme.EspacoDisponivelPercentual.Valor < 10)
            {
                atencao.Add($"espaço reservado disponível {nvme.EspacoDisponivelPercentual.Valor} %");
            }

            if (nvme.TemperaturaCelsius.Valor >= 60)
            {
                atencao.Add($"temperatura {nvme.TemperaturaCelsius.Valor} °C");
            }

            evidencias.Add(new Evidencia
            {
                Titulo = "Vida usada (NVMe)",
                Valor = nvme.PercentualVidaUsada.Disponivel ? nvme.PercentualVidaUsada.Valor + " %" : "não disponível",
            });
        }

        if (saude.Confiabilidade is { } conf)
        {
            if (conf.ErrosLeituraNaoCorrigidos.Valor > 0)
            {
                falha.Add($"erros de leitura não corrigidos = {conf.ErrosLeituraNaoCorrigidos.Valor}");
            }

            if (conf.DesgastePercentual.Valor >= 80)
            {
                atencao.Add($"desgaste {conf.DesgastePercentual.Valor} %");
            }
        }

        if (varredura is not null && varredura.BlocosComErro > 0)
        {
            falha.Add($"{varredura.BlocosComErro} bloco(s) com erro de leitura na superfície");
            evidencias.Add(new Evidencia { Titulo = "Superfície", Valor = $"{varredura.BlocosComErro} erro(s) de leitura" });
        }

        var midia = d.Midia.Disponivel ? d.Midia.Valor : TipoMidia.Desconhecido;
        if (falha.Count > 0)
        {
            if (midia == TipoMidia.Ssd)
            {
                recomendacoes.Add(new Recomendacao
                {
                    Acao = "Substituir o SSD e migrar os dados antes da troca.",
                    Prioridade = PrioridadeRecomendacao.Urgente,
                    Justificativa = "SSD com indício de problema de mídia deve ser trocado: não há reparo de setor.",
                });
            }
            else
            {
                recomendacoes.Add(new Recomendacao
                {
                    Acao = "Copiar os dados imediatamente (backup) e substituir o HDD.",
                    Prioridade = PrioridadeRecomendacao.Urgente,
                    Justificativa = "Setores realocados/pendentes tendem a aumentar; o disco não deve ser reparado, apenas isolado.",
                });
            }

            return new RespostaRegra
            {
                Veredito = Veredito.Falha,
                Resumo = $"{nome}: {string.Join("; ", falha.Distinct())}.",
                Evidencias = evidencias,
                Recomendacoes = recomendacoes,
                Limitacoes = limitacoes,
            };
        }

        if (saude.Fontes.Count == 0 || saude.Fontes.All(f => f == FonteSaudeDisco.Nenhuma))
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = $"{nome}: saúde não pôde ser verificada.",
                Limitacoes = limitacoes.Count > 0
                    ? limitacoes
                    : new[] { $"{nome}: nenhuma fonte de saúde (SMART/NVMe/Confiabilidade) disponível." },
            };
        }

        if (atencao.Count > 0)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Atencao,
                Resumo = $"{nome}: {string.Join("; ", atencao.Distinct())}.",
                Evidencias = evidencias,
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Acompanhar os indicadores e programar a troca quando conveniente.",
                        Prioridade = PrioridadeRecomendacao.Recomendada,
                    },
                },
                Limitacoes = limitacoes,
            };
        }

        return new RespostaRegra
        {
            Veredito = Veredito.Ok,
            Resumo = $"{nome}: indicadores de saúde dentro do esperado.",
            Evidencias = evidencias,
            Limitacoes = limitacoes,
        };
    }

    // ---------------- Sensores ----------------
    public static RespostaRegra Sensores(IReadOnlyList<ResumoSensor> resumos, double limiteTemperaturaCelsius, IReadOnlyList<AmostraSensores>? amostras = null)
    {
        var temperaturas = resumos.Where(r => r.Categoria == CategoriaSensor.Temperatura).ToList();
        if (temperaturas.Count == 0)
        {
            var causa = amostras?.LastOrDefault()?.CausaAusencia
                        ?? "O hardware não expôs nenhum sensor de temperatura para esta execução.";
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = causa,
                Limitacoes = new[] { causa },
            };
        }

        var max = temperaturas.Max(r => r.Maximo);
        var evidencias = temperaturas
            .Select(r => new Evidencia { Titulo = r.Rotulo, Valor = $"{FormatacaoBr.Temperatura(r.Minimo)} a {FormatacaoBr.Temperatura(r.Maximo)}" })
            .ToList();

        if (max >= limiteTemperaturaCelsius + 10)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Falha,
                Resumo = $"Temperatura máxima de {FormatacaoBr.Temperatura(max)} muito acima do limite de {FormatacaoBr.Temperatura(limiteTemperaturaCelsius)}.",
                Evidencias = evidencias,
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Verificar ventilação, limpeza de coolers e pasta térmica; não usar o equipamento sob carga até revisar.",
                        Prioridade = PrioridadeRecomendacao.Urgente,
                    },
                },
            };
        }

        if (max >= limiteTemperaturaCelsius)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Atencao,
                Resumo = $"Temperatura máxima de {FormatacaoBr.Temperatura(max)} atingiu o limite de {FormatacaoBr.Temperatura(limiteTemperaturaCelsius)}.",
                Evidencias = evidencias,
            };
        }

        return new RespostaRegra
        {
            Veredito = Veredito.Ok,
            Resumo = $"Temperaturas dentro do limite de {FormatacaoBr.Temperatura(limiteTemperaturaCelsius)}.",
            Evidencias = evidencias,
        };
    }

    // ---------------- CPU ----------------
    public static RespostaRegra Cpu(InventarioSistema inv, ResultadoEstresse? estresse, IReadOnlyList<ResumoSensor> sensores, double limiteCpu)
    {
        if (inv.Cpu.Nome.Ausente)
        {
            return new RespostaRegra { Veredito = Veredito.Inconclusivo, Resumo = "Processador não identificado." };
        }

        var nome = inv.Cpu.Nome.Valor!;
        if (estresse is null)
        {
            var temSensor = sensores.Any(s => s.Categoria == CategoriaSensor.Temperatura);
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = temSensor
                    ? $"{nome}: sensores lidos, mas nenhum teste de estabilidade foi executado."
                    : $"{nome}: nenhum teste de estabilidade e nenhuma temperatura lida.",
                Limitacoes = new[] { "Teste de estabilidade de CPU não executado nesta sessão." },
            };
        }

        var evidencias = new List<Evidencia>
        {
            new() { Titulo = "Duração do teste", Valor = FormatacaoBr.Duracao(estresse.DuracaoReal) },
            new() { Titulo = "Ciclos completados", Valor = estresse.CiclosTotais.ToString() },
            new() { Titulo = "Erros de cálculo", Valor = estresse.ErrosDeCalculo.ToString() },
            new() { Titulo = "Motivo da parada", Valor = estresse.MotivoParadaTexto },
        };

        if (estresse.ErrosDeCalculo > 0)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Falha,
                Resumo = $"O processador produziu {estresse.ErrosDeCalculo} resultado(s) incorreto(s) durante o teste.",
                Evidencias = evidencias,
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Testar memória, conferir temperatura/energia e avaliar troca da CPU ou placa-mãe.",
                        Prioridade = PrioridadeRecomendacao.Urgente,
                        Justificativa = "Erro de cálculo em carga determinística indica instabilidade.",
                    },
                },
            };
        }

        if (estresse.ThrottlingDetectado.Valor)
        {
            var tempAlta = estresse.TemperaturaMaximaCelsius.Disponivel && estresse.TemperaturaMaximaCelsius.Valor >= limiteCpu;
            return new RespostaRegra
            {
                Veredito = Veredito.Atencao,
                Resumo = tempAlta
                    ? $"Queda de clock detectada com temperatura de {FormatacaoBr.Temperatura(estresse.TemperaturaMaximaCelsius.Valor)}."
                    : "Queda de clock detectada durante a carga.",
                Evidencias = evidencias,
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Revisar refrigeração (limpeza, pasta térmica) e perfil de energia.",
                        Prioridade = PrioridadeRecomendacao.Recomendada,
                    },
                },
            };
        }

        return new RespostaRegra
        {
            Veredito = Veredito.Ok,
            Resumo = "Teste de estabilidade concluído sem erros de cálculo e sem queda de clock.",
            Evidencias = evidencias,
        };
    }

    // ---------------- GPU ----------------
    public static RespostaRegra Gpu(IReadOnlyList<InfoGpu> gpus, ResultadoGpu? resultado)
    {
        if (gpus.Count == 0 || gpus.All(g => g.Nome.Ausente))
        {
            return new RespostaRegra { Veredito = Veredito.Inconclusivo, Resumo = "Nenhum adaptador de vídeo identificado." };
        }

        var nome = gpus.First().Nome.Valor ?? "GPU";
        if (resultado is null)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = $"{nome}: inventário lido, mas nenhum teste de GPU executado.",
            };
        }

        if (resultado.Estresse is { } e)
        {
            if (e.ErrosDeCalculo > 0)
            {
                return new RespostaRegra { Veredito = Veredito.Falha, Resumo = $"{nome}: dispositivo de vídeo removido (erro de driver/TDR) durante o teste." };
            }
        }

        if (resultado.Benchmark is { PixelsDivergentes: > 0 } b)
        {
            return new RespostaRegra { Veredito = Veredito.Falha, Resumo = $"{nome}: {b.PixelsDivergentes} pixel(s) com artefato no teste." };
        }

        return new RespostaRegra { Veredito = Veredito.Ok, Resumo = $"{nome}: teste de GPU concluído sem artefatos." };
    }

    // ---------------- Bateria ----------------
    public static RespostaRegra Bateria(InfoBateriaDetalhe b)
    {
        if (!b.Presente)
        {
            return new RespostaRegra { Veredito = Veredito.NaoSeAplica, Resumo = "Equipamento sem bateria (desktop ou bateria removida)." };
        }

        if (b.CapacidadeProjetoMwh.Ausente || b.CapacidadeAtualMwh.Ausente || b.SaudePercentual.Ausente)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = "Capacidade da bateria não pôde ser lida.",
                Limitacoes = b.Limitacoes.Select(l => l.Motivo).ToList(),
            };
        }

        var saude = b.SaudePercentual.Valor;
        var evidencias = new List<Evidencia>
        {
            new() { Titulo = "Capacidade de projeto", Valor = b.CapacidadeProjetoMwh.Valor + " mWh" },
            new() { Titulo = "Capacidade atual", Valor = b.CapacidadeAtualMwh.Valor + " mWh" },
            new() { Titulo = "Saúde", Valor = saude.ToString("N1", FormatacaoBr.Cultura) + " %" },
        };

        if (b.Ciclos.Disponivel)
        {
            evidencias.Add(new Evidencia { Titulo = "Ciclos", Valor = b.Ciclos.Valor.ToString() });
        }

        var v = saude < 60 ? Veredito.Falha : saude < 80 ? Veredito.Atencao : Veredito.Ok;
        return new RespostaRegra
        {
            Veredito = v,
            Resumo = v switch
            {
                Veredito.Falha => $"Bateria com saúde de {saude:N1} %: substituir.",
                Veredito.Atencao => $"Bateria com saúde de {saude:N1} %: autonomia reduzida.",
                _ => $"Bateria com saúde de {saude:N1} %.",
            },
            Evidencias = evidencias,
            Recomendacoes = v == Veredito.Ok
                ? Array.Empty<Recomendacao>()
                : new[]
                {
                    new Recomendacao
                    {
                        Acao = "Substituir a bateria por uma do fabricante do equipamento.",
                        Prioridade = v == Veredito.Falha ? PrioridadeRecomendacao.Urgente : PrioridadeRecomendacao.Recomendada,
                    },
                },
            Limitacoes = b.Limitacoes.Select(l => l.Motivo).ToList(),
        };
    }

    // ---------------- Rede ----------------
    public static RespostaRegra Rede(InfoRede rede)
    {
        if (rede.Adaptadores.All(a => !a.Ativo))
        {
            return new RespostaRegra { Veredito = Veredito.Inconclusivo, Resumo = "Nenhum adaptador de rede ativo." };
        }

        var testes = rede.Alvos.Concat(rede.LatenciaGateway.Disponivel ? new[] { rede.LatenciaGateway.Valor! } : Array.Empty<ResultadoLatencia>()).ToList();
        if (testes.Count == 0)
        {
            return new RespostaRegra { Veredito = Veredito.Inconclusivo, Resumo = "Nenhum teste de latência executado." };
        }

        var pior = Veredito.Ok;
        var resumos = new List<string>();
        var evidencias = new List<Evidencia>();

        foreach (var t in testes)
        {
            evidencias.Add(new Evidencia
            {
                Titulo = "Alvo " + t.Alvo,
                Valor = $"perda {t.PerdaPercentual:N0} %, média {t.LatenciaMediaMs:N0} ms",
            });

            if (t.PerdaPercentual >= 100)
            {
                pior = VereditoTexto.MaisGrave(pior, t.TcpRespondido ? Veredito.Inconclusivo : Veredito.Falha);
                resumos.Add(t.TcpRespondido
                    ? $"{t.Alvo}: ICMP bloqueado, TCP respondeu (inconclusivo)."
                    : $"{t.Alvo}: 100 % de perda.");
            }
            else if (t.PerdaPercentual >= 20)
            {
                pior = Veredito.Falha;
                resumos.Add($"{t.Alvo}: perda de {t.PerdaPercentual:N0} %.");
            }
            else if (t.PerdaPercentual >= 5 || t.LatenciaMediaMs > 100)
            {
                pior = VereditoTexto.MaisGrave(pior, Veredito.Atencao);
                resumos.Add($"{t.Alvo}: perda {t.PerdaPercentual:N0} %, latência {t.LatenciaMediaMs:N0} ms.");
            }
            else
            {
                resumos.Add($"{t.Alvo}: perda {t.PerdaPercentual:N0} %, latência {t.LatenciaMediaMs:N0} ms.");
            }
        }

        return new RespostaRegra
        {
            Veredito = pior,
            Resumo = string.Join(" ", resumos),
            Evidencias = evidencias,
            Limitacoes = rede.Limitacoes.Select(l => l.Motivo).ToList(),
        };
    }

    // ---------------- Checklist ----------------
    public static RespostaRegra Checklist(IReadOnlyList<ItemChecklist> itens)
    {
        if (itens.Count == 0)
        {
            return new RespostaRegra { Veredito = Veredito.Inconclusivo, Resumo = "Checklist não preenchido." };
        }

        var falhas = itens.Where(i => i.Status == StatusChecklist.Falha).Select(i => i.Titulo).ToList();
        var pendentes = itens.Where(i => i.Status == StatusChecklist.Pendente).Select(i => i.Titulo).ToList();

        if (falhas.Count > 0)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Falha,
                Resumo = "Falha nos itens: " + string.Join(", ", falhas) + ".",
                Evidencias = falhas.Select(f => new Evidencia { Titulo = "Falha", Valor = f }).ToList(),
            };
        }

        if (pendentes.Count > 0)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = "Itens sem avaliação: " + string.Join(", ", pendentes) + ".",
            };
        }

        return new RespostaRegra { Veredito = Veredito.Ok, Resumo = "Todos os itens verificados." };
    }
}
