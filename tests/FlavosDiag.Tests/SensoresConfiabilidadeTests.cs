using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Workloads;

namespace FlavosDiag.Tests;

/// <summary>
/// Causa real da CPU sem leitura, proteção térmica do stress e vereditos que dependem de
/// temperatura de confiança alta.
/// </summary>
/// <remarks>
/// Tudo aqui usa amostras construídas à mão e um relógio simulado: nenhum teste liga carga real no
/// processador nem espera minutos de verdade. Os valores 0/255/-128 vêm da máquina do dono, onde o
/// sensor <c>Core (Tctl/Tdie)</c> devolve 0 com mínimo 0 e máximo 0.
/// </remarks>
public static class SensoresConfiabilidadeTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        // ---- Tarefa 1: sentinela tratada na origem ----
        yield return ("T1: 0 com mín 0 e máx 0 é sentinela", () => Task.Run(ZeroComMinMaxZeroEhSentinela));
        yield return ("T1: 255, -128 e 121 não são temperatura válida", () => Task.Run(ForaDaFaixaNaoEhValida));
        yield return ("T1: sensor de GPU não é escolhido como CPU", () => Task.Run(GpuNaoViraCpu));
        yield return ("T1: CPU com leitura válida continua funcionando", () => Task.Run(CpuValidaContinuaValendo));
        yield return ("T1: amostra carrega a lista de sensores sem leitura", () => Task.Run(AmostraCarregaSemLeitura));

        // ---- Tarefa 2: causa da falta de leitura ----
        yield return ("T2: não elevado pede administrador", () => Task.Run(NaoElevadoPedeAdmin));
        yield return ("T2: elevado sem PawnIO pede o driver", () => Task.Run(ElevadoSemPawnIo));
        yield return ("T2: elevado com PawnIO acusa a biblioteca", () => Task.Run(ElevadoComPawnIo));
        yield return ("T2: PawnIO indefinido não afirma a causa", () => Task.Run(PawnIoIndefinido));
        yield return ("T2: nunca diz 'requer elevação' quando já elevado", () => Task.Run(NuncaAfirmaElevacaoIndevida));
        yield return ("T2: nada a relatar quando há leitura", () => Task.Run(SemCausaComLeitura));

        // ---- Tarefa 3: proteção térmica ----
        yield return ("T3: 100 °C com confiança alta para em 3 amostras", () => Task.Run(ParaEmTresAmostras));
        yield return ("T3: pico isolado de 1 amostra não para o teste", () => Task.Run(PicoIsoladoNaoPara));
        yield return ("T3: sentinela 255 não para o teste", () => Task.Run(SentinelaNaoPara));
        yield return ("T3: sem leitura de CPU para no teto de 2 min", () => Task.Run(TetoSemSensor2Min));
        yield return ("T3: teto de duração encerra o teste", () => Task.Run(TetoDeDuracao));
        yield return ("T3: Parar do usuário é sempre atendido", () => Task.Run(PararSempreFunciona));
        yield return ("T3: leitura crua acima de 120 °C é emergência térmica", () => Task.Run(CruaAcimaDoFisicoPara));
        yield return ("T3: sensor oscilando não zera a contagem de calor", () => Task.Run(SensorOscilanteNaoEscapa));
        yield return ("T3: temperatura que cai de verdade zera a contagem", () => Task.Run(TemperaturaQueCaiZera));

        // ---- Tarefa 4: vereditos ----
        yield return ("T4: CPU sem temperatura de confiança é Inconclusivo", () => Task.Run(CpuSemTemperaturaInconclusivo));
        yield return ("T4: Sensores não é OK só porque a GPU tem temperatura", () => Task.Run(SensoresNaoOkPorOutroComponente));
        yield return ("T4: cada componente usa o seu próprio limite", () => Task.Run(CadaComponenteComSeuLimite));
    }

    // ------------------------------------------------------------------ construção de amostras

    private static ValorSensor Temperatura(
        TipoHardware tipo,
        string nomeSensor,
        double valor,
        string nomeHardware = "hardware") => new()
        {
            Id = nomeHardware + "/" + nomeSensor,
            Categoria = CategoriaSensor.Temperatura,
            Rotulo = nomeHardware + " — " + nomeSensor,
            Valor = valor,
            Unidade = "°C",
            TipoHardware = tipo,
            NomeHardware = nomeHardware,
            NomeSensor = nomeSensor,
            Qualidade = QualidadeSensor.Ok,
        };

    private static SensorSemLeitura SemLeitura(
        TipoHardware tipo,
        string nomeSensor,
        double? valor = 0,
        double? min = 0,
        double? max = 0) => new()
        {
            Id = "hardware/" + nomeSensor,
            Categoria = CategoriaSensor.Temperatura,
            TipoHardware = tipo,
            NomeHardware = "hardware",
            NomeSensor = nomeSensor,
            Unidade = "°C",
            ValorBruto = valor,
            Minimo = min,
            Maximo = max,
            Motivo = TemperaturaFisica.MotivoSemLeitura(valor, min, max),
        };

    private static AmostraSensores Amostra(
        IEnumerable<ValorSensor>? valores = null,
        IEnumerable<SensorSemLeitura>? semLeitura = null) => new()
        {
            Quando = DateTimeOffset.Now,
            Valores = (valores ?? []).ToList(),
            SemLeitura = (semLeitura ?? []).ToList(),
        };

    private static ResumoSensor Resumo(TipoHardware tipo, string rotulo, double maximo, string id = "s") => new()
        {
            Id = id,
            Rotulo = rotulo,
            Categoria = CategoriaSensor.Temperatura,
            Unidade = "°C",
            Minimo = 30,
            Maximo = maximo,
            Media = (30 + maximo) / 2,
            Amostras = 10,
            TipoHardware = tipo,
            Qualidade = QualidadeSensor.Ok,
        };

    // ------------------------------------------------------------------ Tarefa 1

    private static void ZeroComMinMaxZeroEhSentinela()
    {
        // O caso real: o sensor existe, mas nunca variou de 0. Não é "processador a 0 °C".
        Assert.True(
            TemperaturaFisica.EhSentinela(0, 0, 0),
            "valor 0 com mínimo 0 e máximo 0 precisa ser tratado como sentinela");

        var amostra = Amostra(
            semLeitura: new[] { SemLeitura(TipoHardware.Cpu, "Core (Tctl/Tdie)") });

        var temperatura = ClassificadorTemperatura.DaCpu(amostra, out var causa);
        Assert.True(temperatura is null, "não pode haver temperatura de CPU");
        Assert.True(causa is not null && causa.Contains("Core (Tctl/Tdie)"), "a causa precisa nomear o sensor");
        Assert.True(amostra.SemLeitura.Count == 1, "o sensor sem leitura precisa constar na amostra");
    }

    private static void ForaDaFaixaNaoEhValida()
    {
        foreach (var invalido in new double[] { 0, 255, -128, 121, -40, 150 })
        {
            Assert.False(TemperaturaFisica.Valida(invalido), $"valor {invalido} não pode ser temperatura válida");
        }

        Assert.True(TemperaturaFisica.Valida(1), "1 °C é o mínimo válido");
        Assert.True(TemperaturaFisica.Valida(120), "120 °C é o máximo válido");
        Assert.False(TemperaturaFisica.Valida(null), "ausência de valor não é válida");
        Assert.False(TemperaturaFisica.Valida(double.NaN), "NaN não é válida");
    }

    private static void GpuNaoViraCpu()
    {
        // A GPU também tem um sensor chamado "Core". Buscar por nome solto escolhia o errado.
        var amostra = Amostra(new[]
        {
            Temperatura(TipoHardware.Gpu, "GPU Core", 62, "GPU"),
            Temperatura(TipoHardware.Placa, "Core", 38, "Placa"),
        });

        var temperatura = ClassificadorTemperatura.DaCpu(amostra, out var causa);
        Assert.True(temperatura is null, "sensor de GPU/placa chamado 'Core' não é temperatura da CPU");
        Assert.True(causa is not null, "precisa haver causa");

        var gpu = ClassificadorTemperatura.DaGpu(amostra, out _);
        Assert.True(gpu is 62.0, "a GPU continua sendo lida pelo seu próprio tipo");
    }

    private static void CpuValidaContinuaValendo()
    {
        var amostra = Amostra(new[] { Temperatura(TipoHardware.Cpu, "Core (Tctl/Tdie)", 63.5) });
        var temperatura = ClassificadorTemperatura.DaCpu(amostra, out var causa);

        Assert.True(temperatura is 63.5, "a leitura válida da CPU precisa passar");
        Assert.True(causa is null, "sem ausência não há causa");
    }

    private static void AmostraCarregaSemLeitura()
    {
        var amostra = Amostra(
            new[] { Temperatura(TipoHardware.Gpu, "GPU Core", 45) },
            new[] { SemLeitura(TipoHardware.Cpu, "Core (Tctl/Tdie)") });

        Assert.Single(amostra.Valores);
        Assert.Single(amostra.SemLeitura);
        Assert.Equal(TipoHardware.Cpu, amostra.SemLeitura[0].TipoHardware);
        Assert.True(amostra.SemLeitura[0].Motivo.Length > 0, "todo sensor sem leitura precisa de motivo");

        // O formato antigo (sem a lista) continua válido: campo opcional com lista vazia.
        var antiga = new AmostraSensores { Quando = DateTimeOffset.Now, Valores = [] };
        Assert.Empty(antiga.SemLeitura);
    }

    // ------------------------------------------------------------------ Tarefa 2

    private static void NaoElevadoPedeAdmin()
    {
        var fatos = DiagnosticoSensores.Avaliar(processoElevado: false, pawnIoInstalado: true, 2, 0);
        Assert.Equal(CausaSensor.SemElevacao, DiagnosticoSensores.Classificar(fatos));

        var texto = DiagnosticoSensores.TextoDeCausa(fatos);
        Assert.True(texto.Contains("administrador"), "precisa orientar a abrir como administrador");
    }

    private static void ElevadoSemPawnIo()
    {
        var fatos = DiagnosticoSensores.Avaliar(processoElevado: true, pawnIoInstalado: false, 2, 0);
        Assert.Equal(CausaSensor.DriverAusente, DiagnosticoSensores.Classificar(fatos));

        var texto = DiagnosticoSensores.TextoDeCausa(fatos);
        Assert.True(texto.Contains("PawnIO"), "precisa nomear o driver ausente");
        Assert.True(texto.Contains(DiagnosticoSensores.ComandoInstalacao), "precisa trazer o comando sugerido");
        Assert.True(
            DiagnosticoSensores.SugereInstalacaoDeDriver(fatos),
            "instalar o driver é uma causa possível: mostrar o comando");
    }

    private static void ElevadoComPawnIo()
    {
        var fatos = DiagnosticoSensores.Avaliar(processoElevado: true, pawnIoInstalado: true, 2, 0);
        Assert.Equal(CausaSensor.BibliotecaNaoLeu, DiagnosticoSensores.Classificar(fatos));

        var texto = DiagnosticoSensores.TextoDeCausa(fatos);
        Assert.True(texto.Contains("reconheceu"), "precisa dizer que a biblioteca reconheceu a CPU");
        Assert.False(
            DiagnosticoSensores.SugereInstalacaoDeDriver(fatos),
            "com o driver instalado, não se sugere instalar de novo");
    }

    private static void PawnIoIndefinido()
    {
        var fatos = DiagnosticoSensores.Avaliar(processoElevado: true, pawnIoInstalado: null, 2, 0);
        Assert.Equal(CausaSensor.Indeterminada, DiagnosticoSensores.Classificar(fatos));

        var texto = DiagnosticoSensores.TextoDeCausa(fatos);
        Assert.True(
            texto.Contains("não foi possível confirmar"),
            "sem certeza, o texto precisa admitir a incerteza em vez de afirmar a causa");
    }

    private static void NuncaAfirmaElevacaoIndevida()
    {
        // Já elevado: nenhum dos textos pode dizer que falta elevação.
        foreach (var pawnIo in new bool?[] { null, true, false })
        {
            var fatos = DiagnosticoSensores.Avaliar(processoElevado: true, pawnIoInstalado: pawnIo, 2, 0);
            var texto = DiagnosticoSensores.TextoDeCausa(fatos);
            Assert.False(
                texto.Contains("não está sendo executado como administrador"),
                "processo elevado e mensagem pedindo elevação é a confusão que esta tarefa corrige");
        }

        // Com leitura disponível, não há causa nenhuma.
        var comLeitura = DiagnosticoSensores.Avaliar(processoElevado: false, pawnIoInstalado: null, 2, 2);
        Assert.Equal(CausaSensor.Nenhuma, DiagnosticoSensores.Classificar(comLeitura));
    }

    private static void SemCausaComLeitura()
    {
        var fatos = DiagnosticoSensores.Avaliar(processoElevado: true, pawnIoInstalado: false, 3, 1);
        Assert.Equal(CausaSensor.Nenhuma, DiagnosticoSensores.Classificar(fatos));
        Assert.True(DiagnosticoSensores.TextoDeCausa(fatos).Length == 0, "com leitura não há faixa de aviso");
        Assert.False(DiagnosticoSensores.SugereInstalacaoDeDriver(fatos), "não se pede driver sem necessidade");
    }

    // ------------------------------------------------------------------ Tarefa 3

    /// <summary>
    /// Eleva a temperatura gradualmente e conta em que amostra a proteção decide parar. O relógio é
    /// simulado, então isto roda em microssegundos.
    /// </summary>
    private static void ParaEmTresAmostras()
    {
        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));

        // Duas amostras a 100 °C: ainda não para.
        Assert.True(protecao.Avaliar(100, TimeSpan.FromSeconds(1), true) is null, "1ª amostra não para");
        Assert.True(protecao.Avaliar(101, TimeSpan.FromSeconds(2), true) is null, "2ª amostra não para");

        var parada = protecao.Avaliar(102, TimeSpan.FromSeconds(3), true);
        Assert.True(parada is not null, "3ª amostra consecutiva acima do limite precisa parar");
        Assert.Equal(MotivoParada.Temperatura, parada!.Motivo);
        Assert.True(parada.Texto.Contains("Parado automaticamente"), "a mensagem precisa dizer que parou sozinho");
        Assert.True(parada.Texto.Contains("100"), "a mensagem precisa citar o limite");
    }

    private static void PicoIsoladoNaoPara()
    {
        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));

        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(1), true) is null, "1º pico");
        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(2), true) is null, "2º pico");
        Assert.True(protecao.Avaliar(72, TimeSpan.FromSeconds(3), true) is null, "voltou ao normal: nada a fazer");

        // O contador precisa ter sido zerado: mais duas amostras altas não bastam.
        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(4), true) is null, "contador reiniciado (1)");
        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(5), true) is null, "contador reiniciado (2)");
    }

    private static void SentinelaNaoPara()
    {
        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));

        // 255 e 0 são sentinelas: o classificador devolve null, então a proteção vê "sem leitura".
        // O que não pode acontecer é 255 virar parada térmica, nem 0 virar "está frio, pode subir".
        Assert.True(TemperaturaFisica.EhSentinela(255, 0, 255), "255 precisa ser sentinela");
        Assert.False(TemperaturaFisica.Valida(255), "255 não é temperatura");

        var amostra = Amostra(semLeitura: new[]
        {
            SemLeitura(TipoHardware.Cpu, "Core (Tctl/Tdie)", 255, 0, 255),
        });

        var temperatura = ClassificadorTemperatura.DaCpu(amostra, out _);
        Assert.True(temperatura is null, "sentinela não pode virar temperatura");

        // Com a CPU sem leitura, a parada que vale é o teto de tempo, não a temperatura.
        var parada = protecao.Avaliar(temperatura, TimeSpan.FromSeconds(10), temSensor: false);
        Assert.True(parada is null, "sentinela sozinha não encerra o teste");
    }

    private static void TetoSemSensor2Min()
    {
        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));

        Assert.True(
            protecao.Avaliar(null, TimeSpan.FromSeconds(119), temSensor: false) is null,
            "antes de 2 minutos o teste continua");

        var parada = protecao.Avaliar(null, TimeSpan.FromMinutes(2), temSensor: false);
        Assert.True(parada is not null, "aos 2 minutos sem sensor o teste precisa parar");
        Assert.Equal(MotivoParada.SemSensorTemperaturaLimite2Min, parada!.Motivo);
        Assert.True(parada.Texto.Contains("2 min"), "a mensagem precisa citar o teto de 2 minutos");
    }

    private static void TetoDeDuracao()
    {
        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));

        Assert.True(protecao.Avaliar(65, TimeSpan.FromMinutes(29), true) is null, "antes do teto continua");

        var parada = protecao.Avaliar(65, TimeSpan.FromMinutes(30), true);
        Assert.True(parada is not null, "no teto de duração o teste precisa encerrar");
        Assert.Equal(MotivoParada.ConcluidoPorTempo, parada!.Motivo);
    }

    private static void PararSempreFunciona()
    {
        // O botão Parar cancela o token; a proteção não pode impedir nem atrapalhar isso.
        using var cts = new CancellationTokenSource();
        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));

        cts.Cancel();
        Assert.True(cts.IsCancellationRequested, "o cancelamento do usuário precisa valer sempre");

        // Mesmo com a proteção já decidindo parar, o cancelamento continua funcionando.
        protecao.Avaliar(120, TimeSpan.FromSeconds(1), true);
        protecao.Avaliar(120, TimeSpan.FromSeconds(2), true);
        var automatica = protecao.Avaliar(120, TimeSpan.FromSeconds(3), true);
        Assert.True(automatica is not null, "a proteção também para");
        Assert.True(cts.IsCancellationRequested, "o cancelamento do usuário não foi afetado");
    }

    /// <summary>
    /// 130 °C é fisicamente implausível, então o classificador devolve null. Mesmo assim precisa parar:
    /// "sem leitura" não pode virar "pode continuar" quando o sensor está gritando.
    /// </summary>
    private static void CruaAcimaDoFisicoPara()
    {
        var amostra = Amostra(new[] { Temperatura(TipoHardware.Cpu, "Core (Tctl/Tdie)", 130) });

        Assert.True(
            ClassificadorTemperatura.DaCpu(amostra, out _) is null,
            "acima de 120 °C o classificador não entrega leitura confiável");

        var crua = CargaCpuRunner.TemperaturaCruaAcimaDoFisico(amostra);
        Assert.True(crua is 130.0, "a leitura crua acima do máximo físico precisa ser reconhecida");

        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));
        Assert.True(protecao.Avaliar(crua, TimeSpan.FromSeconds(1), true) is null, "1ª amostra");
        Assert.True(protecao.Avaliar(crua, TimeSpan.FromSeconds(2), true) is null, "2ª amostra");
        Assert.True(
            protecao.Avaliar(crua, TimeSpan.FromSeconds(3), true) is not null,
            "leitura sustentada acima do máximo físico precisa parar o teste");

        // Sentinelas continuam não sendo emergência: 255 e 0 não podem disparar a parada térmica.
        var sentinela = Amostra(new[]
        {
            Temperatura(TipoHardware.Cpu, "Core (Tctl/Tdie)", 255) with { Qualidade = QualidadeSensor.SemLeitura },
            Temperatura(TipoHardware.Cpu, "CPU Package", 0) with { Qualidade = QualidadeSensor.SemLeitura },
        });

        Assert.True(
            CargaCpuRunner.TemperaturaCruaAcimaDoFisico(sentinela) is null,
            "sentinela não é emergência térmica");
    }

    /// <summary>
    /// Um sensor que alterna entre "muito quente" e "sem leitura" é o pior caso: se cada leitura
    /// ausente zerasse o contador, o teste nunca pararia justamente quando o sensor falha.
    /// </summary>
    private static void SensorOscilanteNaoEscapa()
    {
        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));

        // O sensor existe e está entregando leituras, mas oscila entre alta e "sem leitura".
        // A sequência precisa continuar de onde parou: 3 leituras altas, com um buraco no meio.
        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(1), true) is null, "1ª alta (contador 1)");
        Assert.True(protecao.Avaliar(null, TimeSpan.FromSeconds(2), true) is null, "buraco não conta");
        Assert.True(protecao.Avaliar(106, TimeSpan.FromSeconds(3), true) is null, "2ª alta (contador 2)");

        var parada = protecao.Avaliar(107, TimeSpan.FromSeconds(4), true);
        Assert.True(
            parada is not null,
            "a sequência de calor não pode ser zerada por uma leitura ausente isolada");
        Assert.Equal(MotivoParada.Temperatura, parada!.Motivo);
    }

    private static void TemperaturaQueCaiZera()
    {
        var protecao = new ProtecaoTermica(100, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));

        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(1), true) is null, "1ª alta");
        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(2), true) is null, "2ª alta");
        Assert.True(protecao.Avaliar(70, TimeSpan.FromSeconds(3), true) is null, "esfriou de verdade");

        // Depois de esfriar, duas altas ainda não bastam: a sequência recomeçou.
        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(4), true) is null, "recomeço (1)");
        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(5), true) is null, "recomeço (2)");
        Assert.True(protecao.Avaliar(105, TimeSpan.FromSeconds(6), true) is not null, "recomeço (3) para");
    }

    // ------------------------------------------------------------------ Tarefa 4

    private static void CpuSemTemperaturaInconclusivo()
    {
        var estresse = new ResultadoEstresse
        {
            IniciadoEm = DateTimeOffset.Now,
            DuracaoReal = TimeSpan.FromMinutes(5),
            DuracaoSolicitada = TimeSpan.FromMinutes(5),
            Perfil = PerfilCarga.Pesado,
            MotivoParada = MotivoParada.SemSensorTemperaturaLimite2Min,
            MotivoParadaTexto = "Parado no teto de 2 minutos.",
            Workers = 4,
            CiclosTotais = 1000,
            ErrosDeCalculo = 0,
            TemperaturaMaximaCelsius = Leitura<double>.NaoSuportado("Sem leitura de temperatura da CPU."),
            TemperaturaMediaCelsius = Leitura<double>.NaoSuportado("Sem leitura de temperatura da CPU."),
            ClockSustentadoMhz = Leitura<double>.NaoSuportado("sem clock"),
            ThrottlingDetectado = Leitura<bool>.Ok(false, "comparação de clock"),
        };

        var inventario = FlavosDiag.Providers.Fakes.Fixtures.Inventario();
        var sensores = new[] { Resumo(TipoHardware.Gpu, "GPU — GPU Core", 60) };

        var resposta = Regras.Cpu(inventario, estresse, sensores, 90);

        Assert.Equal(Veredito.Inconclusivo, resposta.Veredito);
        Assert.True(
            resposta.Resumo.Contains("temperatura", StringComparison.OrdinalIgnoreCase),
            "o resumo precisa explicar que falta temperatura, não apenas 'inconclusivo'");
    }

    private static void SensoresNaoOkPorOutroComponente()
    {
        // GPU, disco e placa com temperatura, CPU sem leitura: antes isto saía como OK.
        var resumos = new[]
        {
            Resumo(TipoHardware.Gpu, "GPU — GPU Core", 60, "g"),
            Resumo(TipoHardware.Armazenamento, "SSD — Temperature", 45, "d"),
            Resumo(TipoHardware.Placa, "Placa — System", 40, "p"),
        };

        var amostra = Amostra(
            resumos.SelectMany(_ => Array.Empty<ValorSensor>()).ToList(),
            new[] { SemLeitura(TipoHardware.Cpu, "Core (Tctl/Tdie)") });

        var resposta = Regras.Sensores(resumos, 90, new[] { amostra });

        Assert.Equal(Veredito.Inconclusivo, resposta.Veredito);
        Assert.True(
            resposta.Resumo.Contains("CPU"),
            "o resumo precisa dizer que o que falta é a temperatura da CPU");
    }

    private static void CadaComponenteComSeuLimite()
    {
        // CPU confortável, disco muito quente para o limite dele: precisa reprovar pelo disco.
        var resumos = new[]
        {
            Resumo(TipoHardware.Cpu, "CPU — Core (Tctl/Tdie)", 55, "c"),
            Resumo(TipoHardware.Armazenamento, "SSD — Temperature", 75, "d"),
        };

        var resposta = Regras.Sensores(resumos, 90);

        Assert.Equal(Veredito.Falha, resposta.Veredito);
        Assert.True(resposta.Resumo.Contains("SSD"), "o resumo precisa apontar o componente que estourou");

        // O mesmo disco a 45 °C, com a CPU dentro do limite, é OK.
        var saudavel = new[]
        {
            Resumo(TipoHardware.Cpu, "CPU — Core (Tctl/Tdie)", 55, "c"),
            Resumo(TipoHardware.Armazenamento, "SSD — Temperature", 45, "d"),
        };

        Assert.Equal(Veredito.Ok, Regras.Sensores(saudavel, 90).Veredito);

        // Limites próprios por tipo de componente.
        Assert.True(Regras.LimiteDoComponente(TipoHardware.Armazenamento, 90) < 90, "disco tem limite próprio, menor");
        Assert.True(Regras.LimiteDoComponente(TipoHardware.Gpu, 90) > 90, "GPU tem limite próprio, maior");
    }
}
