using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;

namespace FlavosDiag.Tests;

/// <summary>
/// Fonte única dos limites térmicos (<c>LimitesTermicos</c>): invariantes, correção de configuração
/// inválida e o teste de arquitetura que impede um literal térmico de voltar a nascer fora dela.
/// </summary>
/// <remarks>
/// Os casos são síncronos e embrulhados em <see cref="Task.Run(Action)"/> de propósito: um <c>async</c>
/// sem <c>await</c> renderia CS1998, que vira erro no build em Release com <c>-warnaserror</c>.
/// </remarks>
public static class LimitesTermicosTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        // ---- invariantes dos padrões
        yield return ("Limites: os padrões respeitam veredito <= alerta <= parada < emergência", () => Task.Run(PadroesRespeitamCadeia));
        yield return ("Limites: a parada padrão cabe no teto de 110 °C", () => Task.Run(ParadaCabeNoTeto));
        yield return ("Limites: o veredito da CPU subiu para 95 °C", () => Task.Run(VereditoSubiuPara95));
        yield return ("Limites: a emergência é o máximo físico, não um literal", () => Task.Run(EmergenciaVemDoFisico));
        yield return ("Limites: todo padrão está dentro da faixa utilizável", () => Task.Run(PadroesNaFaixaUtilizavel));

        // ---- limites por componente
        yield return ("Limites: CPU, GPU, memória e placa derivam do limite da CPU", () => Task.Run(LimitesPorComponente));
        yield return ("Limites: disco muda com a mídia (HDD, SSD SATA, NVMe)", () => Task.Run(DiscoPorMidia));
        yield return ("Limites: NVMe tem precedência sobre a mídia informada", () => Task.Run(NvmeTemPrecedencia));
        yield return ("Limites: mídia desconhecida usa o limite mais conservador", () => Task.Run(MidiaDesconhecidaConservadora));
        yield return ("Limites: limite de CPU inválido não se propaga", () => Task.Run(LimiteCpuInvalidoNaoPropaga));

        // ---- procedência e rótulo
        yield return ("Limites: sem limiar do disco, o limite sai rotulado 'limite genérico'", () => Task.Run(DiscoSemFichaEhGenerico));
        yield return ("Limites: limiar do próprio disco é preferido e a fonte é registrada", () => Task.Run(LimiarDoDiscoTemPrecedencia));
        yield return ("Limites: o texto do limite genérico avisa que é valor de partida", () => Task.Run(TextoDoGenericoAvisa));

        // ---- correção de configuração inválida
        yield return ("Limites: configuração válida não é corrigida nem gera motivo", () => Task.Run(ConfiguracaoValidaPassa));
        yield return ("Limites: alerta acima da parada é corrigido com motivo", () => Task.Run(AlertaAcimaDaParadaCorrigido));
        yield return ("Limites: parada acima de 110 °C é corrigida com motivo", () => Task.Run(ParadaAcimaDoTetoCorrigida));
        yield return ("Limites: parada igual à emergência é corrigida", () => Task.Run(ParadaNaEmergenciaCorrigida));
        yield return ("Limites: veredito acima do alerta é corrigido", () => Task.Run(VereditoAcimaDoAlertaCorrigido));
        yield return ("Limites: valor fora da faixa é reposto ao padrão", () => Task.Run(ValorForaDaFaixaReposto));
        yield return ("Limites: NaN e infinito são corrigidos, nunca aceitos", () => Task.Run(NaoFinitoCorrigido));
        yield return ("Limites: toda correção devolve motivo não vazio em pt-BR", () => Task.Run(TodaCorrecaoTemMotivo));
        yield return ("Limites: a correção nunca devolve combinação inválida", () => Task.Run(CorrecaoDevolveCombinacaoValida));

        // ---- arquitetura
        yield return ("Arquitetura: nenhum literal térmico fora de LimitesTermicos/TemperaturaFisica", () => Task.Run(SemLiteraisTermicosFora));
        yield return ("Arquitetura: os consumidores leem de LimitesTermicos", () => Task.Run(ConsumidoresLeemDaLimites));
        yield return ("Arquitetura: o Core não referencia Windows nem WPF", () => Task.Run(CorePuro));
    }

    // ------------------------------------------------------------------ invariantes

    private static void PadroesRespeitamCadeia()
    {
        Assert.True(
            LimitesTermicos.VereditoCpuCelsius <= LimitesTermicos.AlertaVisualCelsius,
            "o veredito não pode passar do alerta: avisar depois de já ter reprovado não faz sentido");
        Assert.True(
            LimitesTermicos.AlertaVisualCelsius <= LimitesTermicos.ParadaAutomaticaCelsius,
            "o alerta não pode passar da parada: o aviso nunca apareceria");
        Assert.True(
            LimitesTermicos.ParadaAutomaticaCelsius < LimitesTermicos.EmergenciaCelsius,
            "a parada precisa agir antes da emergência");

        // O mesmo teste pelo método público: os padrões do próprio código precisam passar a validação.
        var validado = LimitesTermicos.ValidarPadroes();
        Assert.False(validado.Corrigido, "os padrões do código não podem precisar de correção");
        Assert.Empty(validado.Motivos);
        Assert.Equal(95.0, validado.VereditoCpuCelsius);
        Assert.Equal(95.0, validado.AlertaVisualCelsius);
        Assert.Equal(95.0, validado.ParadaAutomaticaCelsius);
    }

    private static void ParadaCabeNoTeto()
    {
        Assert.Equal(110.0, LimitesTermicos.ParadaMaximaCelsius);
        Assert.True(
            LimitesTermicos.ParadaAutomaticaCelsius <= LimitesTermicos.ParadaMaximaCelsius,
            "a parada padrão precisa caber no teto de 110 °C");
        Assert.True(
            LimitesTermicos.ParadaMaximaCelsius < LimitesTermicos.EmergenciaCelsius,
            "o teto da parada precisa ficar abaixo da emergência");
    }

    private static void VereditoSubiuPara95()
    {
        // D81: 90 °C de projeto é normal em processador moderno sob carga; reprovar isso
        // ensinava o técnico a ignorar o veredito.
        Assert.Equal(95.0, LimitesTermicos.VereditoCpuCelsius);
        Assert.True(LimitesTermicos.VereditoCpuCelsius > 90.0, "o veredito precisa ser maior que os 90 °C anteriores");
    }

    private static void EmergenciaVemDoFisico()
    {
        Assert.Equal(120.0, LimitesTermicos.EmergenciaCelsius);
        Assert.Equal(TemperaturaFisica.MaximoValidoCelsius, LimitesTermicos.EmergenciaCelsius);

        // A emergência e a fronteira de "isto não é medição" precisam ser o mesmo ponto (D77).
        Assert.False(
            TemperaturaFisica.Valida(LimitesTermicos.EmergenciaCelsius + 1),
            "acima da emergência o valor já não é medição");
    }

    private static void PadroesNaFaixaUtilizavel()
    {
        foreach (var (nome, valor) in Padroes())
        {
            Assert.True(TemperaturaFisica.Valida(valor), $"o padrão de {nome} ({valor}) precisa ser temperatura utilizável");
        }
    }

    private static IEnumerable<(string Nome, double Valor)> Padroes()
    {
        yield return ("veredito da CPU", LimitesTermicos.VereditoCpuCelsius);
        yield return ("alerta visual", LimitesTermicos.AlertaVisualCelsius);
        yield return ("parada automática", LimitesTermicos.ParadaAutomaticaCelsius);
        yield return ("emergência", LimitesTermicos.EmergenciaCelsius);
        yield return ("HDD", LimitesTermicos.HddCelsius);
        yield return ("SSD SATA", LimitesTermicos.SsdSataCelsius);
        yield return ("NVMe", LimitesTermicos.NvmeCelsius);
        yield return ("memória", LimitesTermicos.MemoriaCelsius);
    }

    // ------------------------------------------------------------------ por componente

    private static void LimitesPorComponente()
    {
        var cpu = LimitesTermicos.VereditoCpuCelsius;

        Assert.Equal(cpu, LimitesTermicos.LimiteDoComponente(TipoHardware.Cpu, cpu));
        Assert.Equal(cpu + LimitesTermicos.MargemGpuCelsius, LimitesTermicos.LimiteDoComponente(TipoHardware.Gpu, cpu));
        Assert.Equal(85.0, LimitesTermicos.LimiteDoComponente(TipoHardware.Memoria, cpu));

        // Placa e SuperIO usam o limite da CPU como teto folgado.
        Assert.Equal(cpu, LimitesTermicos.LimiteDoComponente(TipoHardware.Placa, cpu));
        Assert.Equal(cpu, LimitesTermicos.LimiteDoComponente(TipoHardware.SuperIo, cpu));

        // A GPU esquenta mais que a CPU por projeto: sem margem, toda placa daria Atenção.
        Assert.True(
            LimitesTermicos.LimiteDoComponente(TipoHardware.Gpu, cpu) > cpu,
            "a GPU precisa de limite próprio, acima do da CPU");
    }

    private static void DiscoPorMidia()
    {
        Assert.Equal(55.0, LimitesTermicos.LimiteDoArmazenamento(TipoMidia.Hdd, nvme: false));
        Assert.Equal(70.0, LimitesTermicos.LimiteDoArmazenamento(TipoMidia.Ssd, nvme: false));
        Assert.Equal(80.0, LimitesTermicos.LimiteDoArmazenamento(TipoMidia.Ssd, nvme: true));

        // A ordem dos regimes térmicos é o que a decisão D82 comprou: HDD < SSD SATA < NVMe.
        Assert.True(LimitesTermicos.HddCelsius < LimitesTermicos.SsdSataCelsius, "HDD trabalha abaixo do SSD SATA");
        Assert.True(LimitesTermicos.SsdSataCelsius < LimitesTermicos.NvmeCelsius, "SSD SATA trabalha abaixo do NVMe");
    }

    private static void NvmeTemPrecedencia()
    {
        // Um NVMe é sempre SSD; o barramento define o regime térmico, não o tipo de mídia.
        Assert.Equal(
            LimitesTermicos.NvmeCelsius,
            LimitesTermicos.LimiteDoArmazenamento(TipoMidia.Hdd, nvme: true));
    }

    private static void MidiaDesconhecidaConservadora()
    {
        // Mídia desconhecida não pode herdar o limite mais folgado: na dúvida, o aviso chega cedo.
        Assert.Equal(
            LimitesTermicos.HddCelsius,
            LimitesTermicos.LimiteDoArmazenamento(TipoMidia.Desconhecido, nvme: false));

        // Sem a mídia do disco, o mesmo conservadorismo vale no limite genérico por componente.
        Assert.Equal(
            LimitesTermicos.HddCelsius,
            LimitesTermicos.LimiteDoComponente(TipoHardware.Armazenamento, LimitesTermicos.VereditoCpuCelsius));
    }

    private static void LimiteCpuInvalidoNaoPropaga()
    {
        // Um limite inválido não pode virar NaN na tela nem "Infinity" no laudo.
        foreach (var invalido in new double[] { 0, -10, double.NaN, double.PositiveInfinity })
        {
            var limite = LimitesTermicos.LimiteDoComponente(TipoHardware.Cpu, invalido);
            Assert.True(
                TemperaturaFisica.Valida(limite),
                $"limite de CPU inválido ({invalido}) propagou {limite} em vez do padrão");
        }
    }

    // ------------------------------------------------------------------ procedência

    private static void DiscoSemFichaEhGenerico()
    {
        var limite = LimitesTermicos.LimiteDoArmazenamentoComProcedencia(TipoMidia.Ssd, nvme: true);

        Assert.Equal(LimitesTermicos.NvmeCelsius, limite.Celsius);
        Assert.Equal(LimitesTermicos.OrigemLimite.PadraoDoPrograma, limite.Origem);
        Assert.Equal(LimitesTermicos.RotuloLimiteGenerico, limite.Rotulo);
        Assert.Equal(LimitesTermicos.ComparacaoComFabricante.Desconhecida, limite.Comparacao);
        Assert.True(limite.EhGenerico, "sem ficha, o limite precisa ser marcado como genérico");
        Assert.True(limite.Descrever().Contains("limite genérico"), "o laudo precisa ler 'limite genérico': " + limite.Descrever());
    }

    private static void LimiarDoDiscoTemPrecedencia()
    {
        // O disco informou o próprio limiar de aviso do controlador: é ele que vale, com fonte.
        var limite = LimitesTermicos.LimiteDoArmazenamentoComProcedencia(
            TipoMidia.Ssd,
            nvme: true,
            limiarDoComponenteCelsius: 76,
            fonteDoComponente: "log SMART/Health, warning temperature threshold");

        Assert.Equal(76.0, limite.Celsius);
        Assert.Equal(LimitesTermicos.OrigemLimite.ProprioComponente, limite.Origem);
        Assert.False(limite.EhGenerico, "com limiar declarado, o limite não é genérico");
        Assert.True(
            limite.Descrever().Contains("log SMART/Health"),
            "a fonte do limiar precisa ir para o laudo: " + limite.Descrever());
        Assert.False(
            limite.Descrever().Contains("limite genérico"),
            "com fonte declarada não se escreve 'limite genérico': " + limite.Descrever());
    }

    private static void TextoDoGenericoAvisa()
    {
        var limite = LimitesTermicos.LimiteDoArmazenamentoComProcedencia(TipoMidia.Hdd, nvme: false);
        var texto = limite.Descrever();

        // A frase inteira é a ressalva exigida: um número do programa não pode parecer do fabricante.
        Assert.True(texto.Contains("valor de partida"), "precisa dizer que é valor de partida: " + texto);
        Assert.True(texto.Contains("experiência de bancada"), "precisa orientar o ajuste pela bancada: " + texto);
        Assert.True(texto.Contains("ficha do fabricante"), "precisa orientar o ajuste pela ficha do fabricante: " + texto);

        // Limiar implausível não pode substituir o padrão só porque "foi informado".
        foreach (var invalido in new double?[] { null, 0, 255, double.NaN })
        {
            var comInvalido = LimitesTermicos.LimiteDoArmazenamentoComProcedencia(
                TipoMidia.Ssd, nvme: true, limiarDoComponenteCelsius: invalido);
            Assert.Equal(LimitesTermicos.NvmeCelsius, comInvalido.Celsius);
            Assert.True(comInvalido.EhGenerico, "limiar implausível não pode ser aceito como ficha do disco");
        }
    }

    // ------------------------------------------------------------------ correção de configuração

    private static void ConfiguracaoValidaPassa()
    {
        var resultado = LimitesTermicos.Validar(95, 95, 95);

        Assert.False(resultado.Corrigido, "a combinação dos padrões é válida");
        Assert.Empty(resultado.Motivos);
        Assert.Equal(95.0, resultado.VereditoCpuCelsius);
        Assert.Equal(95.0, resultado.AlertaVisualCelsius);
        Assert.Equal(95.0, resultado.ParadaAutomaticaCelsius);

        // A faixa aceita precisa ter mais de um valor, senão o limite não seria configurável.
        Assert.False(LimitesTermicos.Validar(90, 90, 100).Corrigido, "90/90/100 é uma combinação válida");
        Assert.False(LimitesTermicos.Validar(95, 100, 110).Corrigido, "95/100/110 é a borda válida");
    }

    private static void AlertaAcimaDaParadaCorrigido()
    {
        // O caso que a tela antiga deixava salvar: alerta 119 °C com parada 60 °C. O aviso nunca apareceria.
        var resultado = LimitesTermicos.Validar(95, 119, 100);

        Assert.True(resultado.Corrigido, "alerta acima da parada precisa ser corrigido");
        Assert.True(
            resultado.AlertaVisualCelsius <= resultado.ParadaAutomaticaCelsius,
            "depois da correção o alerta precisa caber na parada");
        Assert.Equal(LimitesTermicos.AlertaVisualCelsius, resultado.AlertaVisualCelsius);
        Assert.True(resultado.MotivosTexto.Contains("alerta"), "o motivo precisa nomear o alerta: " + resultado.MotivosTexto);
    }

    private static void ParadaAcimaDoTetoCorrigida()
    {
        var resultado = LimitesTermicos.Validar(95, 95, 115);

        Assert.True(resultado.Corrigido, "parada acima de 110 °C precisa ser corrigida");
        Assert.True(
            resultado.ParadaAutomaticaCelsius <= LimitesTermicos.ParadaMaximaCelsius,
            "a parada corrigida precisa caber no teto de 110 °C");
        Assert.Equal(LimitesTermicos.ParadaAutomaticaCelsius, resultado.ParadaAutomaticaCelsius);
        Assert.True(resultado.MotivosTexto.Contains("110"), "o motivo precisa citar o teto: " + resultado.MotivosTexto);
    }

    private static void ParadaNaEmergenciaCorrigida()
    {
        var resultado = LimitesTermicos.Validar(95, 95, 120);

        Assert.True(resultado.Corrigido, "parada igual à emergência precisa ser corrigida");
        Assert.True(
            resultado.ParadaAutomaticaCelsius < resultado.EmergenciaCelsius,
            "a parada precisa ficar abaixo da emergência");
    }

    private static void VereditoAcimaDoAlertaCorrigido()
    {
        // Não há como configurar o veredito hoje, mas a invariante também é entre ele e os outros dois.
        // Aqui o alerta também é inválido para a parada, então a cadeia inteira volta ao padrão — e o
        // que o teste exige é que o resultado nunca fique com veredito acima do alerta.
        var resultado = LimitesTermicos.Validar(120, 90, 95);

        Assert.True(resultado.Corrigido, "veredito acima do alerta precisa ser corrigido");
        Assert.True(
            resultado.VereditoCpuCelsius <= resultado.AlertaVisualCelsius,
            "depois da correção o veredito precisa caber no alerta");
        Assert.True(resultado.MotivosTexto.Contains("veredito"), "o motivo precisa nomear o veredito: " + resultado.MotivosTexto);

        // Quando só o veredito está errado, a correção preserva o resto.
        var soVeredito = LimitesTermicos.Validar(100, 95, 95);
        Assert.True(soVeredito.Corrigido, "veredito 100 °C acima do alerta precisa ser corrigido");
        Assert.Equal(95.0, soVeredito.VereditoCpuCelsius);
        Assert.Equal(95.0, soVeredito.AlertaVisualCelsius);
        Assert.Equal(95.0, soVeredito.ParadaAutomaticaCelsius);
    }

    private static void ValorForaDaFaixaReposto()
    {
        var resultado = LimitesTermicos.Validar(95, 0, 200);

        Assert.True(resultado.Corrigido, "valores fora da faixa precisam ser corrigidos");
        Assert.Equal(LimitesTermicos.AlertaVisualCelsius, resultado.AlertaVisualCelsius);
        Assert.Equal(LimitesTermicos.ParadaAutomaticaCelsius, resultado.ParadaAutomaticaCelsius);
        Assert.True(resultado.Motivos.Count >= 2, "cada campo corrigido precisa de um motivo próprio");
    }

    private static void NaoFinitoCorrigido()
    {
        foreach (var naoFinito in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var resultado = LimitesTermicos.Validar(95, naoFinito, naoFinito);

            Assert.True(resultado.Corrigido, $"o valor {naoFinito} precisa ser corrigido, nunca aceito");
            Assert.True(double.IsFinite(resultado.AlertaVisualCelsius), "o alerta corrigido precisa ser finito");
            Assert.True(double.IsFinite(resultado.ParadaAutomaticaCelsius), "a parada corrigida precisa ser finita");
            Assert.False(resultado.Motivos.Count == 0, "correção sem motivo é aceitação silenciosa");
        }
    }

    private static void TodaCorrecaoTemMotivo()
    {
        foreach (var (alerta, parada) in new[] { (119.0, 100.0), (95.0, 115.0), (0.0, 95.0), (95.0, 200.0) })
        {
            var resultado = LimitesTermicos.Validar(95, alerta, parada);

            Assert.True(resultado.Corrigido, $"a combinação {alerta}/{parada} deveria ser corrigida");
            Assert.False(
                string.IsNullOrWhiteSpace(resultado.MotivosTexto),
                $"a combinação {alerta}/{parada} foi corrigida sem dizer por quê");
        }

        // Configuração intocada não inventa motivo nenhum.
        Assert.True(string.IsNullOrEmpty(LimitesTermicos.Validar(95, 95, 95).MotivosTexto), "sem correção, sem motivo");
    }

    private static void CorrecaoDevolveCombinacaoValida()
    {
        // A correção precisa ser idempotente e terminar sempre numa combinação válida, nunca num meio-termo.
        var entradas = new[]
        {
            (119.0, 100.0), (95.0, 115.0), (120.0, 95.0), (0.0, 0.0), (200.0, 200.0),
            (-5.0, 60.0), (95.0, 120.0), (110.0, 110.0),
        };

        foreach (var (alerta, parada) in entradas)
        {
            var primeiro = LimitesTermicos.Validar(95, alerta, parada);
            Assert.True(
                LimitesTermicos.CadeiaValida(
                    primeiro.VereditoCpuCelsius, primeiro.AlertaVisualCelsius, primeiro.ParadaAutomaticaCelsius),
                $"a correção de {alerta}/{parada} devolveu uma combinação inválida: " +
                $"{primeiro.VereditoCpuCelsius}/{primeiro.AlertaVisualCelsius}/{primeiro.ParadaAutomaticaCelsius}");

            // Corrigir a combinação já corrigida não muda mais nada.
            var segundo = LimitesTermicos.Validar(
                primeiro.VereditoCpuCelsius, primeiro.AlertaVisualCelsius, primeiro.ParadaAutomaticaCelsius);
            Assert.False(segundo.Corrigido, $"a correção de {alerta}/{parada} não estabilizou");
        }
    }

    // ------------------------------------------------------------------ arquitetura

    /// <summary>
    /// Percorre os <c>.cs</c> de <c>src/</c> e reprova literal numérico de temperatura fora dos dois
    /// módulos que podem declarar número térmico.
    /// </summary>
    /// <remarks>
    /// O teste existe porque a unificação não se mantém sozinha: sem ele, a próxima pressa acrescenta
    /// <c>if (t &gt;= 80)</c> em algum view model e os limites voltam a divergir em silêncio. A detecção
    /// é por proximidade — um literal numérico de dois ou três dígitos na mesma linha, antes ou depois,
    /// de um identificador com "Celsius", "Limiar"/"Limite" térmico ou sufixo <c>°C</c>.
    /// </remarks>
    private static void SemLiteraisTermicosFora()
    {
        var raiz = RaizRepositorio();
        var permitidos = new[]
        {
            "LimitesTermicos.cs",
            "TemperaturaFisica.cs",
        };

        var achados = new List<string>();
        foreach (var arquivo in ArquivosDeCodigo(Path.Combine(raiz, "src")))
        {
            if (permitidos.Contains(Path.GetFileName(arquivo), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var linhas = File.ReadAllLines(arquivo, Encoding.UTF8);
            for (var i = 0; i < linhas.Length; i++)
            {
                // Comentário e texto de interface não declaram limite: o número citado ali é descrição.
                if (EhComentario(linhas[i]))
                {
                    continue;
                }

                if (TemLiteralTermico(linhas[i]))
                {
                    achados.Add($"{Path.GetRelativePath(raiz, arquivo)}:{i + 1}: {linhas[i].Trim()}");
                }
            }
        }

        Assert.True(
            achados.Count == 0,
            "literal térmico fora de LimitesTermicos/TemperaturaFisica; leia os limites da fonte única:" +
            Environment.NewLine + "       " + string.Join(Environment.NewLine + "       ", achados));
    }

    private static void ConsumidoresLeemDaLimites()
    {
        var raiz = RaizRepositorio();

        // O padrão do runner e os padrões da configuração precisam vir da fonte única, não de literal.
        ContemReferencia(
            Path.Combine(raiz, "src", "FlavosDiag.Workloads", "CargaCpuRunner.cs"),
            "LimiteTemperaturaPadraoCelsius = LimitesTermicos.",
            "o padrão do runner precisa ler LimitesTermicos");

        ContemReferencia(
            Path.Combine(raiz, "src", "FlavosDiag.App", "Services", "Servicos.cs"),
            "= LimitesTermicos.",
            "os padrões de Configuracao precisam ler LimitesTermicos");

        ContemReferencia(
            Path.Combine(raiz, "src", "FlavosDiag.App", "ViewModels", "PaginasApoio.cs"),
            "LimitesTermicos.Validar(",
            "a validação da tela de Configurações precisa usar LimitesTermicos.Validar");

        ContemReferencia(
            Path.Combine(raiz, "src", "FlavosDiag.Core", "Vereditos", "Regras.cs"),
            "LimitesTermicos.LimiteDoComponente(",
            "o limite por componente do veredito precisa vir de LimitesTermicos");

        // A documentação canônica existe e cobre os limites obrigatórios.
        var documento = File.ReadAllText(Path.Combine(raiz, "docs", "LIMITES-TERMICOS.md"), Encoding.UTF8);
        foreach (var termo in new[] { "VereditoCpuCelsius", "AlertaVisualCelsius", "ParadaAutomaticaCelsius", "EmergenciaCelsius", "limite genérico", "valor de partida" })
        {
            Assert.True(
                documento.Contains(termo, StringComparison.OrdinalIgnoreCase),
                $"docs/LIMITES-TERMICOS.md precisa documentar \"{termo}\"");
        }
    }

    /// <summary>
    /// O Core precisa continuar puro: o limite térmico é regra de diagnóstico e tem de valer numa máquina
    /// sem Windows, sem WPF e sem biblioteca de sensores.
    /// </summary>
    private static void CorePuro()
    {
        var raiz = RaizRepositorio();
        var pastaCore = Path.Combine(raiz, "src", "FlavosDiag.Core");

        var proibidos = new[] { "System.Windows", "PresentationFramework", "LibreHardwareMonitor", "System.Management", "Microsoft.Win32.Registry" };
        var achados = new List<string>();
        foreach (var arquivo in ArquivosDeCodigo(pastaCore))
        {
            foreach (var linha in File.ReadAllLines(arquivo, Encoding.UTF8))
            {
                var limpa = linha.Trim();
                if (!limpa.StartsWith("using ", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var proibido in proibidos)
                {
                    if (limpa.Contains(proibido, StringComparison.Ordinal))
                    {
                        achados.Add(Path.GetRelativePath(raiz, arquivo) + ": " + limpa);
                    }
                }
            }
        }

        Assert.True(
            achados.Count == 0,
            "o Core não pode referenciar Windows, WPF nem biblioteca de sensores:" + Environment.NewLine +
            "       " + string.Join(Environment.NewLine + "       ", achados));

        // E o módulo novo tem de estar efetivamente dentro do Core.
        Assert.True(
            File.Exists(Path.Combine(pastaCore, "Monitor", "LimitesTermicos.cs")),
            "LimitesTermicos precisa morar em FlavosDiag.Core/Monitor");
    }

    private static void ContemReferencia(string arquivo, string trecho, string mensagem)
    {
        Assert.True(File.Exists(arquivo), "arquivo não encontrado: " + arquivo);
        var texto = File.ReadAllText(arquivo, Encoding.UTF8);
        Assert.True(texto.Contains(trecho, StringComparison.Ordinal), mensagem + " (procurei \"" + trecho + "\" em " + Path.GetFileName(arquivo) + ")");
    }

    // ------------------------------------------------------------------ varredura do fonte

    /// <summary>Linha inteira é comentário? Um número citado em comentário é descrição, não limite.</summary>
    private static bool EhComentario(string linha)
    {
        var limpa = linha.TrimStart();
        return limpa.StartsWith("//", StringComparison.Ordinal) ||
               limpa.StartsWith("///", StringComparison.Ordinal) ||
               limpa.StartsWith("*", StringComparison.Ordinal) ||
               limpa.StartsWith("/*", StringComparison.Ordinal);
    }

    /// <summary>
    /// Há um literal numérico de temperatura colado a um identificador térmico? Cobre <c>x &gt;= 85</c>,
    /// <c>temperaturaMaximaCelsius: 95</c>, <c>LimiteCpu = 90</c> e <c>limite + 10</c>.
    /// </summary>
    /// <remarks>
    /// Texto entre aspas é removido antes da checagem: uma mensagem ao técnico que explica a faixa
    /// utilizável ("fora de 1–120 °C") descreve um número, não declara um limite. O que se procura é
    /// número em <b>expressão</b>, que é onde um limite realmente entra em vigor.
    /// </remarks>
    private static bool TemLiteralTermico(string linha)
    {
        var codigo = SemTexto(linha);
        if (codigo.Length == 0)
        {
            return false;
        }

        // Literal decimal simples; constantes com nome não caem aqui de propósito.
        var literal = @"(?<![\w.])[0-9]{2,3}(?:[.,][0-9]+)?(?:[dDfFmM])?(?![\w])";

        // Um identificador que fala de temperatura ou de limite térmico.
        var termico = @"(?:[A-Za-z_][\w]*Celsius|Celsius[\w]*|[Ll]imiar[\w]*|[Ll]imite[\w]*|[Tt]emperatura[\w]*)";

        return Regex.IsMatch(codigo, termico + @"[^=;{}()]*?" + literal)
               || Regex.IsMatch(codigo, literal + @"[^=;{}()]*?" + termico);
    }

    /// <summary>Remove literais de texto (e strings interpoladas) da linha, preservando o resto do código.</summary>
    private static string SemTexto(string linha) =>
        Regex.Replace(linha, "\"[^\"\\n]*\"", "\"\"");

    private static IEnumerable<string> ArquivosDeCodigo(string pasta) =>
        Directory.EnumerateFiles(pasta, "*.cs", SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(a => a, StringComparer.Ordinal);

    /// <summary>
    /// Sobe de <see cref="AppContext.BaseDirectory"/> até a raiz do repositório (a pasta que tem
    /// <c>docs/LIMITES-TERMICOS.md</c> e <c>src</c>). Sem caminho fixo, para valer em qualquer máquina.
    /// </summary>
    private static string RaizRepositorio()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null)
        {
            if (Directory.Exists(Path.Combine(pasta.FullName, "src")) &&
                File.Exists(Path.Combine(pasta.FullName, "docs", "LIMITES-TERMICOS.md")))
            {
                return pasta.FullName;
            }

            pasta = pasta.Parent;
        }

        throw new FileNotFoundException(
            "raiz do repositório não encontrada a partir de " + AppContext.BaseDirectory +
            ": o teste precisa rodar dentro do Flavos Diag");
    }
}
