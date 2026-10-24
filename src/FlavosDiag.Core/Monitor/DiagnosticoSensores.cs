namespace FlavosDiag.Core.Monitor;

/// <summary>
/// Estado do PawnIO no sistema. <see cref="NaoDeterminado"/> existe para não afirmar uma causa que
/// não foi verificada — dizer "falta o driver" quando não se sabe seria pior do que não dizer nada.
/// </summary>
public enum EstadoPawnIo
{
    NaoDeterminado = 0,
    Ausente = 1,
    Presente = 2,
}

/// <summary>Qual causa foi identificada para a ausência de leitura de temperatura.</summary>
public enum CausaSensor
{
    /// <summary>Há leitura: nenhuma causa a relatar.</summary>
    Nenhuma = 0,

    /// <summary>O processo não está elevado.</summary>
    SemElevacao = 1,

    /// <summary>Elevado, sensores sem leitura e PawnIO ausente.</summary>
    DriverAusente = 2,

    /// <summary>Elevado, PawnIO presente e ainda assim sem leitura.</summary>
    BibliotecaNaoLeu = 3,

    /// <summary>Não foi possível determinar se o PawnIO está instalado.</summary>
    Indeterminada = 4,

    /// <summary>O equipamento não expõe sensor de temperatura para o componente.</summary>
    SemSensor = 5,
}

/// <summary>
/// Fatos do ambiente usados para explicar a ausência de leitura. Separados numa interface para que
/// a regra seja testável sem Windows, sem driver e sem privilégio.
/// </summary>
public sealed record FatosSensores
{
    public required bool ProcessoElevado { get; init; }

    public required EstadoPawnIo PawnIo { get; init; }

    /// <summary>Quantos sensores de temperatura da CPU/GPU o coletor reconheceu (com e sem leitura).</summary>
    public required int SensoresTermicos { get; init; }

    /// <summary>Quantos desses entregaram valor utilizável.</summary>
    public required int SensoresTermicosComLeitura { get; init; }

    /// <summary>Mensagem original da abertura da sessão, quando houve falha.</summary>
    public string? MensagemAbertura { get; init; }
}

/// <summary>
/// Diagnóstico da leitura de sensores: dada a situação, diz qual é a causa provável e o que o
/// técnico pode fazer. Nada aqui instala driver nem eleva o processo — apenas informa.
/// </summary>
/// <remarks>
/// Decisão de projeto: as causas são avaliadas <b>nesta ordem</b> (elevação, driver, biblioteca,
/// indeterminada). Testar a elevação primeiro evita o erro que existia antes: a tela dizia
/// "requer elevação" mesmo quando o processo já estava elevado e o que faltava era o driver.
/// </remarks>
public static class DiagnosticoSensores
{
    /// <summary>Comando de instalação sugerido. O técnico deve conferir o site oficial antes de usar.</summary>
    public const string ComandoInstalacao = "winget install namazso.PawnIO -e";

    public const string UrlOficial = "https://pawnio.eu";

    public static FatosSensores Avaliar(
        bool processoElevado,
        bool? pawnIoInstalado,
        int sensoresTermicos,
        int sensoresTermicosComLeitura,
        string? mensagemAbertura = null) => new()
        {
            ProcessoElevado = processoElevado,
            PawnIo = pawnIoInstalado switch
            {
                true => EstadoPawnIo.Presente,
                false => EstadoPawnIo.Ausente,
                null => EstadoPawnIo.NaoDeterminado,
            },
            SensoresTermicos = sensoresTermicos,
            SensoresTermicosComLeitura = sensoresTermicosComLeitura,
            MensagemAbertura = mensagemAbertura,
        };

    /// <summary>
    /// Classifica a situação. <paramref name="componente"/> é "CPU" ou "GPU" e só aparece no texto.
    /// </summary>
    public static CausaSensor Classificar(FatosSensores fatos, string componente = "CPU")
    {
        ArgumentNullException.ThrowIfNull(fatos);

        if (fatos.SensoresTermicosComLeitura > 0)
        {
            return CausaSensor.Nenhuma;
        }

        // (a) Sem elevação: quase sempre é isso, e é o que o técnico consegue resolver primeiro.
        if (!fatos.ProcessoElevado)
        {
            return CausaSensor.SemElevacao;
        }

        // Nenhum sensor reconhecido: o equipamento realmente não expõe temperatura.
        if (fatos.SensoresTermicos == 0)
        {
            return CausaSensor.SemSensor;
        }

        // (b)/(c)/(d) Sensores existem, mas nenhum entrega valor.
        return fatos.PawnIo switch
        {
            EstadoPawnIo.Ausente => CausaSensor.DriverAusente,
            EstadoPawnIo.Presente => CausaSensor.BibliotecaNaoLeu,
            _ => CausaSensor.Indeterminada,
        };
    }

    /// <summary>
    /// Texto para a tela e para o laudo: causa, o que fazer e — quando for o caso — o comando.
    /// </summary>
    public static string TextoDeCausa(FatosSensores fatos, string componente = "CPU")
    {
        ArgumentNullException.ThrowIfNull(fatos);
        var causa = Classificar(fatos, componente);
        var prefixo = $"Temperatura da {componente} sem leitura.";

        return causa switch
        {
            CausaSensor.Nenhuma =>
                string.Empty,

            CausaSensor.SemElevacao =>
                prefixo + " O aplicativo não está sendo executado como administrador, e a leitura de " +
                "sensores no Windows exige esse privilégio. Feche o aplicativo e abra com " +
                "\"Executar como administrador\".",

            CausaSensor.DriverAusente =>
                prefixo + " O aplicativo está elevado, mas o driver de leitura de sensores (PawnIO) não " +
                "está instalado. A biblioteca de leitura depende dele para acessar os registradores do " +
                $"processador. Instalação sugerida: {ComandoInstalacao} (confira {UrlOficial} antes). " +
                "O Flavos Diag não instala driver nenhum — a decisão é sua.",

            CausaSensor.BibliotecaNaoLeu =>
                prefixo + " O aplicativo está elevado e o driver PawnIO está instalado, mas a biblioteca " +
                "reconheceu o processador e não conseguiu lê-lo. Se o driver foi instalado agora, feche e " +
                "abra o Flavos Diag de novo: a biblioteca carrega o driver na abertura da sessão e não " +
                "percebe uma instalação feita depois. Se continuar sem leitura, use \"Exportar diagnóstico " +
                "de sensores\" em Configurações e verifique se há versão mais nova da biblioteca.",

            CausaSensor.Indeterminada =>
                prefixo + " O aplicativo está elevado, mas não foi possível confirmar se o driver de " +
                "leitura (PawnIO) está instalado. As duas causas possíveis são: driver ausente ou " +
                "privilégio insuficiente para carregá-lo. Confira a instalação do PawnIO e use " +
                "\"Exportar diagnóstico de sensores\" para registrar a situação.",

            CausaSensor.SemSensor =>
                prefixo + " Este equipamento não expõe sensor de temperatura para este componente; " +
                "não há driver nem privilégio que resolva isso. A placa pode não ter o sensor.",

            _ => prefixo + " Causa não identificada.",
        };
    }

    /// <summary>
    /// Se o técnico deve ver a faixa com o comando de instalação. Só aparece quando instalar o
    /// driver é de fato uma das causas possíveis.
    /// </summary>
    public static bool SugereInstalacaoDeDriver(FatosSensores fatos) =>
        Classificar(fatos) is CausaSensor.DriverAusente or CausaSensor.Indeterminada;

    /// <summary>Se faz sentido reabrir a sessão de sensores agora (mudou algo que dá para corrigir).</summary>
    public static bool ValeTentarDeNovo(FatosSensores fatos) =>
        Classificar(fatos) is CausaSensor.SemElevacao or CausaSensor.DriverAusente or CausaSensor.Indeterminada;
}
