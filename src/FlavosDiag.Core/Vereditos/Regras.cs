using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;

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
    /// <summary>
    /// Veredito da memória. Precedência: nenhum teste executado &gt; teste com duas ou mais execuções com erro
    /// (ou duas ou mais divergências) &gt; teste com uma divergência &gt; teste rápido sem divergência &gt;
    /// memória total abaixo de 4 GiB. Inventário não é teste: sem teste o veredito é sempre Inconclusivo,
    /// e um teste rápido sem divergência também não vira OK.
    /// </summary>
    public static RespostaRegra Memoria(InfoMemoria mem, ResultadoTesteMemoria? teste = null)
    {
        var evidencias = EvidenciasInventario(mem);
        var limitacoes = new List<string> { LimiteTesteRapido };

        // 1. Sem teste (ou teste que não chegou a executar) nada foi provado: inventário lido não é teste.
        if (teste is null || teste.Status == StatusTesteMemoria.NaoExecutado || teste.BytesTestados <= 0)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = mem.TotalBytes.Ausente
                    ? "Não foi possível ler os módulos de memória e nenhum teste de memória foi registrado."
                    : "Inventário de memória lido, mas nenhum teste de memória foi registrado: nada foi gravado e relido " +
                      "para provar que os módulos retêm o conteúdo.",
                Evidencias = evidencias,
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Executar o teste de memória do Flavos Diag (grava padrões e relê) e, se possível, o diagnóstico de memória completo no boot do Windows.",
                        Prioridade = PrioridadeRecomendacao.Recomendada,
                        Justificativa = "Ler o inventário mostra só a quantidade de memória; só um teste de gravação e leitura verifica se os módulos retêm dados.",
                    },
                },
                Limitacoes = mem.TotalBytes.Ausente
                    ? limitacoes.Append(mem.TotalBytes.Causa ?? "WMI não retornou módulos").ToList()
                    : limitacoes,
            };
        }

        evidencias.AddRange(EvidenciasTeste(teste));

        // 4. A confirmação depende de o defeito se repetir em EXECUÇÕES distintas, não de quantas
        // palavras divergiram. Duas palavras erradas na mesma passada são um evento, não dois: pode
        // ser um bit preso num endereço, que um segundo teste confirma ou descarta. Contar
        // divergências aqui transformava um único tropeço em "troque o módulo".
        if (teste.ExecucoesComErro >= 2)
        {
            var execucoes = teste.ExecucoesComErro;
            return new RespostaRegra
            {
                Veredito = Veredito.Falha,
                Resumo = $"O teste de memória encontrou divergência em {execucoes} execuções: o que foi gravado não voltou " +
                         "igual em mais de um momento do teste, então a memória não está retendo os dados com confiabilidade.",
                Evidencias = evidencias,
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Testar os módulos de memória individualmente e substituir o que apresentar divergência; antes da troca, fazer backup dos dados.",
                        Prioridade = PrioridadeRecomendacao.Urgente,
                        Justificativa = "Erros em execuções diferentes indicam defeito real do módulo, não falha isolada do teste.",
                    },
                },
                Limitacoes = limitacoes,
            };
        }

        // 3. Divergência em uma única execução: pode ser falso positivo ou defeito intermitente.
        // Vale para qualquer quantidade de divergências nessa passada — duas palavras erradas no
        // mesmo teste continuam sendo um evento, e o que decide é repetir.
        if (teste.ErrosDetectados >= 1)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Atencao,
                Resumo = "O teste de memória encontrou divergência em uma única execução. Repita o teste: um acerto isolado " +
                         "pode ser falso positivo ou defeito intermitente, e só uma nova execução mostra se o erro se repete.",
                Evidencias = evidencias,
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Repetir o teste de memória. Ele voltará a divergir se o problema for real.",
                        Prioridade = PrioridadeRecomendacao.Recomendada,
                        Justificativa = "Com uma só divergência não é possível separar defeito real de interferência de outro programa ou de um evento isolado.",
                    },
                },
                Limitacoes = limitacoes,
            };
        }

        // 2. Teste rápido sem divergência: continua Inconclusivo, com a ressalva de tamanho se houver.
        var total = mem.TotalBytes.Disponivel ? mem.TotalBytes.Valor : 0;
        var abaixoDe4GiB = total > 0 && total < 4L * 1024 * 1024 * 1024;
        var modulosIlegiveis = mem.Modulos.Any(m => m.CapacidadeBytes.Ausente || m.CapacidadeBytes.Valor <= 0);

        if (abaixoDe4GiB)
        {
            // 5. Regra de tamanho: só vale quando um teste realmente rodou.
            return new RespostaRegra
            {
                Veredito = Veredito.Atencao,
                Resumo = $"O teste de memória não encontrou divergências, mas a memória total de " +
                         $"{FormatacaoBr.Gib(total / 1024.0 / 1024 / 1024)} pode limitar o uso do computador.",
                Evidencias = evidencias,
                Limitacoes = limitacoes,
            };
        }

        if (modulosIlegiveis)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Atencao,
                Resumo = "O teste de memória não encontrou divergências, mas um ou mais módulos têm capacidade ilegível: " +
                         "o inventário não confirma a configuração instalada.",
                Evidencias = evidencias,
                Limitacoes = limitacoes,
            };
        }

        return new RespostaRegra
        {
            Veredito = Veredito.Inconclusivo,
            Resumo = "O teste rápido de memória não encontrou divergências, mas isso não prova que a memória está boa: " +
                     "ele cobre parte da memória por pouco tempo e não substitui um teste completo.",
            Evidencias = evidencias,
            Recomendacoes = new[]
            {
                new Recomendacao
                {
                    Acao = "Para conclusão definitiva, executar o diagnóstico de memória completo no boot do Windows.",
                    Prioridade = PrioridadeRecomendacao.Recomendada,
                    Justificativa = "Só o teste completo, mais longo e abrangendo toda a memória, permite afirmar que a memória está boa.",
                },
            },
            Limitacoes = limitacoes,
        };
    }

    private const string LimiteTesteRapido =
        "Teste rápido: grava padrões em parte da memória livre por pouco tempo; não cobre os módulos inteiros nem substitui o teste completo.";

    /// <summary>Evidências do inventário. Só entram valores lidos: ausência não vira número.</summary>
    private static List<Evidencia> EvidenciasInventario(InfoMemoria mem)
    {
        var evidencias = new List<Evidencia>();
        if (mem.TotalBytes.Disponivel && mem.TotalBytes.Valor > 0)
        {
            evidencias.Add(new Evidencia
            {
                Titulo = "Memória total",
                Valor = FormatacaoBr.Gib(mem.TotalBytes.Valor / 1024.0 / 1024 / 1024),
                Fonte = "Win32_PhysicalMemory",
            });
        }
        else
        {
            evidencias.Add(new Evidencia
            {
                Titulo = "Memória total",
                Valor = FormatacaoBr.NaoDisponivel(mem.TotalBytes.Causa),
                Fonte = "Win32_PhysicalMemory",
            });
        }

        if (mem.SlotsTotais.Disponivel && mem.SlotsTotais.Valor > 0)
        {
            evidencias.Add(new Evidencia
            {
                Titulo = "Slots ocupados",
                Valor = mem.SlotsUsados.Disponivel
                    ? $"{mem.SlotsUsados.Valor} de {mem.SlotsTotais.Valor}"
                    : FormatacaoBr.NaoDisponivel(mem.SlotsUsados.Causa),
                Fonte = "Win32_PhysicalMemoryArray",
            });
        }

        if (mem.Modulos.Any(m => m.CapacidadeBytes.Ausente || m.CapacidadeBytes.Valor <= 0))
        {
            evidencias.Add(new Evidencia { Titulo = "Módulo ilegível", Valor = "Um ou mais módulos sem capacidade válida" });
        }

        return evidencias;
    }

    /// <summary>Evidências do teste de memória executado.</summary>
    private static List<Evidencia> EvidenciasTeste(ResultadoTesteMemoria teste)
    {
        var evidencias = new List<Evidencia>
        {
            new()
            {
                Titulo = "Memória verificada",
                Valor = teste.BytesTestados > 0
                    ? $"{FormatacaoBr.Bytes(teste.BytesTestados)} em {teste.Padroes.Count} padrão(ões)"
                    : "Nenhum byte verificado",
                Fonte = "TesteMemoriaRunner",
            },
            new() { Titulo = "Divergências", Valor = teste.ErrosDetectados.ToString(), Fonte = "TesteMemoriaRunner" },
            new() { Titulo = "Execuções com erro", Valor = teste.ExecucoesComErro.ToString(), Fonte = "TesteMemoriaRunner" },
            new() { Titulo = "Duração do teste", Valor = FormatacaoBr.Duracao(teste.Duracao), Fonte = "TesteMemoriaRunner" },
        };

        if (teste.PrimeiroDeslocamentoErro is { } deslocamento)
        {
            evidencias.Add(new Evidencia { Titulo = "Primeira divergência", Valor = "deslocamento " + deslocamento, Fonte = "TesteMemoriaRunner" });
        }

        if (teste.Cancelado)
        {
            evidencias.Add(new Evidencia { Titulo = "Teste interrompido", Valor = "Cancelado pelo usuário: resultado parcial" });
        }

        return evidencias;
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

        // Limite térmico deste disco pela própria mídia: um HDD trabalha bem abaixo do que um NVMe
        // suporta, e um limite único para os dois aprovava mídia mecânica quente demais. O NVMe é
        // identificado pela presença do log de saúde, que só existe nesse barramento. Quando o disco
        // informar o próprio limiar, ele é preferido e a fonte é registrada — enquanto o modelo não
        // carrega esse campo, o padrão vale e sai rotulado como "limite genérico".
        var limiteDisco = LimitesTermicos.LimiteDoArmazenamentoComProcedencia(
            d.Midia.Disponivel ? d.Midia.Valor : TipoMidia.Desconhecido,
            nvme: saude.Fontes.Contains(FonteSaudeDisco.Nvme) || saude.Nvme is not null);

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

            // 0x00 e 0xFF são sentinelas ("sem leitura") e valores fora da faixa não são temperatura
            // de trabalho: nesses casos não há o que avisar. Sem esta checagem, um sensor mudo (255)
            // viraria "temperatura 255 °C" e o disco seria reprovado por um dado que não existe.
            var tempBruta = (int)(Bruto(194) & 0xFF);
            if (FaixaTemperatura.Valida(tempBruta) && tempBruta >= limiteDisco.Celsius)
            {
                atencao.Add($"temperatura {tempBruta} °C ({limiteDisco.Descrever()})");
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

            if (nvme.TemperaturaCelsius.Valor >= limiteDisco.Celsius)
            {
                atencao.Add($"temperatura {nvme.TemperaturaCelsius.Valor} °C ({limiteDisco.Descrever()})");
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
    /// <summary>
    /// Avalia a categoria Sensores.
    /// </summary>
    /// <remarks>
    /// Duas correções em relação à versão anterior:
    /// <list type="number">
    /// <item>Antes pegava a maior temperatura de <b>qualquer</b> componente e comparava com o limite
    /// da CPU. Um HD a 45 °C com um limite de 90 °C virava "OK" — e a temperatura do processador
    /// podia estar sem leitura, o que é justamente o defeito que se quer detectar.</item>
    /// <item>Sem leitura da CPU, a categoria era concluída como OK porque "algum" sensor respondeu.
    /// Agora o veredito é Inconclusivo e traz a causa.</item>
    /// </list>
    /// </remarks>
    public static RespostaRegra Sensores(
        IReadOnlyList<ResumoSensor> resumos,
        double limiteTemperaturaCelsius,
        IReadOnlyList<AmostraSensores>? amostras = null)
    {
        var temperaturas = resumos.Where(r => r.Categoria == CategoriaSensor.Temperatura).ToList();
        var ultima = amostras?.LastOrDefault();
        var diagnosticos = new List<string>();

        if (temperaturas.Count == 0)
        {
            var causa = ultima?.CausaAusencia
                        ?? "O hardware não expôs nenhum sensor de temperatura para esta execução.";
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = causa,
                Limitacoes = new[] { causa },
            };
        }

        // Temperatura da CPU só conta se vier do próprio processador.
        var daCpu = temperaturas
            .Where(r => r.TipoHardware == TipoHardware.Cpu && r.Qualidade == QualidadeSensor.Ok)
            .ToList();

        var evidencias = temperaturas
            .Select(r => new Evidencia
            {
                Titulo = r.Rotulo,
                Valor = $"{FormatacaoBr.Temperatura(r.Minimo)} a {FormatacaoBr.Temperatura(r.Maximo)} " +
                        $"({r.TipoHardware})",
            })
            .ToList();

        if (daCpu.Count == 0)
        {
            // Outros componentes podem até estar bem, mas isso não certifica a CPU. O veredito é
            // Inconclusivo, com a causa — nunca OK por causa de quem não é o processador.
            var causa = TemperaturaDaCpuAusente(ultima);
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = "Temperatura da CPU sem leitura; a categoria Sensores não pode ser concluída. " + causa,
                Evidencias = evidencias,
                Limitacoes = new[] { causa },
            };
        }

        // Cada componente é comparado com o seu próprio limite.
        var falhas = new List<string>();
        var atencoes = new List<string>();

        foreach (var r in temperaturas.Where(r => r.Qualidade == QualidadeSensor.Ok))
        {
            var limite = LimiteDoComponente(r.TipoHardware, limiteTemperaturaCelsius);
            var nome = string.IsNullOrWhiteSpace(r.Rotulo) ? r.Id : r.Rotulo;
            if (r.Maximo >= limite + LimitesTermicos.MargemFalhaSobreOLimiteCelsius)
            {
                falhas.Add($"{nome}: {FormatacaoBr.Temperatura(r.Maximo)} (limite {FormatacaoBr.Temperatura(limite)})");
            }
            else if (r.Maximo >= limite)
            {
                atencoes.Add($"{nome}: {FormatacaoBr.Temperatura(r.Maximo)} (limite {FormatacaoBr.Temperatura(limite)})");
            }
        }

        if (falhas.Count > 0)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Falha,
                Resumo = "Temperatura muito acima do limite: " + string.Join("; ", falhas) + ".",
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

        if (atencoes.Count > 0)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Atencao,
                Resumo = "Temperatura atingiu o limite do componente: " + string.Join("; ", atencoes) + ".",
                Evidencias = evidencias,
            };
        }

        var resumo = $"Temperaturas dentro dos limites (CPU {FormatacaoBr.Temperatura(daCpu.Max(r => r.Maximo))}).";
        if (diagnosticos.Count > 0)
        {
            resumo += " " + string.Join(" ", diagnosticos);
        }

        return new RespostaRegra
        {
            Veredito = Veredito.Ok,
            Resumo = resumo,
            Evidencias = evidencias,
        };
    }

    /// <summary>
    /// Limite térmico próprio de cada tipo de componente.
    /// </summary>
    /// <remarks>
    /// O cálculo mora em <see cref="LimitesTermicos.LimiteDoComponente"/>, que é a fonte única dos
    /// números. Esta sobrecarga continua existindo para não quebrar chamadores e para manter o nome
    /// que o laudo já usa; os valores não são repetidos aqui.
    /// </remarks>
    public static double LimiteDoComponente(TipoHardware tipo, double limiteCpu) =>
        LimitesTermicos.LimiteDoComponente(tipo, limiteCpu);

    /// <summary>Explica a ausência de temperatura de confiança alta da CPU.</summary>
    public static string TemperaturaDaCpuAusente(AmostraSensores? ultima)
    {
        if (ultima is null)
        {
            return "Nenhuma amostra de sensores foi coletada nesta sessão.";
        }

        var semLeitura = ultima.SemLeitura
            .Where(s => s.Categoria == CategoriaSensor.Temperatura && s.TipoHardware == TipoHardware.Cpu)
            .ToList();

        if (semLeitura.Count > 0)
        {
            var detalhes = string.Join("; ", semLeitura.Select(s => $"{s.NomeSensor}: {s.Motivo}"));
            return $"O processador expôs {semLeitura.Count} sensor(es) de temperatura sem leitura válida ({detalhes}).";
        }

        return "O sistema não expôs sensor de temperatura para o processador.";
    }

    // ---------------- CPU ----------------
    /// <summary>
    /// Avalia a categoria CPU.
    /// </summary>
    /// <remarks>
    /// O item térmico só existe com temperatura de <b>confiança alta</b> do próprio processador.
    /// Antes, um teste sem nenhuma leitura de temperatura saía como OK — o equipamento passava no
    /// item mais importante justamente quando não havia como saber se ele esquenta demais. Sem essa
    /// leitura, o veredito é Inconclusivo e a causa é registrada no laudo.
    /// </remarks>
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

        // Chegou aqui: sem erros e sem throttling. O único item que ainda pode barrar o OK é o
        // térmico, e ele exige leitura de confiança alta do próprio processador.
        var temperaturaConfiavel = sensores
            .Where(s => s.Categoria == CategoriaSensor.Temperatura &&
                        s.TipoHardware == TipoHardware.Cpu &&
                        s.Qualidade == QualidadeSensor.Ok)
            .ToList();

        if (!estresse.TemperaturaMaximaCelsius.Disponivel || temperaturaConfiavel.Count == 0)
        {
            var causa = estresse.TemperaturaMaximaCelsius.Causa ?? "Sem leitura de temperatura da CPU.";
            return new RespostaRegra
            {
                Veredito = Veredito.Inconclusivo,
                Resumo = "Estabilidade de CPU não pôde ser concluída: " + causa +
                         " Sem temperatura do processador não é possível afirmar que o resfriamento está adequado.",
                Evidencias = evidencias,
                Limitacoes = new[] { causa },
                Recomendacoes = new[]
                {
                    new Recomendacao
                    {
                        Acao = "Executar como administrador e conferir o diagnóstico de sensores (Configurações). A temperatura da CPU é necessária para concluir o item térmico.",
                        Prioridade = PrioridadeRecomendacao.Recomendada,
                    },
                },
            };
        }

        var maxCpu = estresse.TemperaturaMaximaCelsius.Valor;
        if (maxCpu >= limiteCpu)
        {
            return new RespostaRegra
            {
                Veredito = Veredito.Atencao,
                Resumo = $"Temperatura máxima de {FormatacaoBr.Temperatura(maxCpu)} atingiu o limite de {FormatacaoBr.Temperatura(limiteCpu)}.",
                Evidencias = evidencias,
            };
        }

        return new RespostaRegra
        {
            Veredito = Veredito.Ok,
            Resumo = "Teste de estabilidade concluído sem erros de cálculo, sem queda de clock e dentro do limite térmico.",
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
