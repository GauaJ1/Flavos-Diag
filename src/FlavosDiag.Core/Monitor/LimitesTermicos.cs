using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Core.Monitor;

/// <summary>
/// Fonte única dos limites térmicos numéricos do aplicativo.
/// </summary>
/// <remarks>
/// <para>
/// Antes desta classe o mesmo número aparecia em lugares diferentes e com valores diferentes: o
/// veredito da CPU comparava com 90 °C, o gráfico destacava o alerta a partir de 95 °C, o veredito
/// de sensores usava 60 °C para qualquer disco e a configuração prometia parar em 100 °C. Nenhum
/// desses lugares estava errado isoladamente — o erro era não haver um lugar que dissesse qual
/// número vale. O técnico via a tela avisar uma coisa e o laudo concluir outra.
/// </para>
/// <para>
/// Aqui ficam as constantes canônicas, os limites por tipo de componente e a única regra que
/// valida a combinação entre eles. Tudo o que consome um limite térmico lê daqui; nenhum outro
/// arquivo de <c>src/</c> pode declarar um número de temperatura próprio. Um teste de arquitetura
/// (<c>LimitesTermicosTests</c>) percorre o fonte e reprova quem tentar voltar a fazer isso.
/// </para>
/// <para>
/// A regra de plausibilidade (1–120 °C, sentinelas 0 e 255) <b>não</b> mora aqui: ela continua em
/// <see cref="TemperaturaFisica"/>, que decide se um número é uma temperatura utilizável. Esta
/// classe decide a partir de que temperatura um valor utilizável vira alerta, veredito ou parada.
/// </para>
/// </remarks>
public static class LimitesTermicos
{
    // ---------------------------------------------------------------- constantes canônicas

    /// <summary>
    /// Temperatura de veredito da CPU: a partir daqui o item térmico do veredito sai como Atenção.
    /// </summary>
    /// <remarks>
    /// Passou de 90 para 95 °C (D78). Um processador moderno sob carga plena sustenta 90 °C de
    /// projeto sem estar com problema de refrigeração, então o limite anterior reprovava máquina
    /// saudável — e um veredito que reprova o que está bem ensina o técnico a ignorar o veredito.
    /// </remarks>
    public const double VereditoCpuCelsius = 95;

    /// <summary>Temperatura a partir da qual o gráfico de stress destaca a faixa de alerta (só visual).</summary>
    public const double AlertaVisualCelsius = 95;

    /// <summary>Temperatura de confiança alta a partir da qual o stress para sozinho.</summary>
    public const double ParadaAutomaticaCelsius = 95;

    /// <summary>
    /// Leitura implausível (acima do máximo físico) que dispara a parada de emergência.
    /// </summary>
    /// <remarks>
    /// O número vem de <see cref="TemperaturaFisica.MaximoValidoCelsius"/> e não de um literal:
    /// a emergência e a fronteira de "isto não é medição" precisam ser o mesmo ponto. Se um dia a
    /// faixa válida mudar, a emergência acompanha em vez de ficar para trás.
    /// </remarks>
    public const double EmergenciaCelsius = TemperaturaFisica.MaximoValidoCelsius;

    /// <summary>Teto absoluto aceito para a parada automática, inclusive vindo da configuração.</summary>
    /// <remarks>
    /// Parar só acima de 110 °C é parar tarde demais para proteger qualquer componente: a essa
    /// altura o dano já pode estar em curso. O teto existe para que nem um arquivo de configuração
    /// editado à mão consiga desligar a proteção, não para ser escolhido como valor de trabalho.
    /// </remarks>
    public const double ParadaMaximaCelsius = 110;

    /// <summary>Piso aceito para a parada automática.</summary>
    public const double ParadaMinimaCelsius = 60;

    // ---------------------------------------------------------------- limites por tipo de componente

    /// <summary>Limite de referência para disco rígido mecânico (HDD).</summary>
    public const double HddCelsius = 55;

    /// <summary>Limite de referência para SSD SATA (ou SATA de 2,5").</summary>
    public const double SsdSataCelsius = 70;

    /// <summary>Limite de referência para SSD NVMe (M.2/PCIe).</summary>
    public const double NvmeCelsius = 80;

    /// <summary>Limite de referência para módulo de memória.</summary>
    public const double MemoriaCelsius = 85;

    /// <summary>Quanto o limite da GPU fica acima do limite da CPU.</summary>
    /// <remarks>
    /// A GPU é projetada para trabalhar mais quente que a CPU. Com o mesmo limite, uma placa de
    /// vídeo em operação normal geraria Atenção em todas as máquinas, e o aviso viraria ruído.
    /// </remarks>
    public const double MargemGpuCelsius = 5;

    /// <summary>Piso do limite de GPU, para que a soma nunca aconteça sobre um limite inválido.</summary>
    public const double MinimoGpuCelsius = 40;

    /// <summary>
    /// Acima do limite do componente em quanto o veredito deixa de ser Atenção e passa a Falha.
    /// </summary>
    /// <remarks>
    /// A distância separa "atingiu o limite" de "está muito acima dele": chegar ao limite é sinal para
    /// revisar a refrigeração, e passar dele com folga é sinal de resfriamento que não está dando conta.
    /// Era um <c>+ 10</c> solto dentro de <c>Regras.Sensores</c>; está aqui para que o veredito e o
    /// laudo não divirjam sobre onde fica a fronteira.
    /// </remarks>
    public const double MargemFalhaSobreOLimiteCelsius = 10;

    /// <summary>Rótulo do limite que vale quando o disco não informou a própria ficha.</summary>
    public const string RotuloLimiteGenerico = "limite genérico";

    // ---------------------------------------------------------------- ficha do componente

    /// <summary>
    /// Origem de um limite térmico: <b>de onde veio o número</b>. Sem isso, um 80 °C de disco não
    /// se distingue entre "padrão do programa" e "limite do fabricante", e o laudo afirmaria uma
    /// referência que não consultou.
    /// </summary>
    public enum OrigemLimite
    {
        /// <summary>Padrão de partida do programa, não confirmado em bancada nem no fabricante.</summary>
        PadraoDoPrograma = 0,

        /// <summary>Valor configurado pelo técnico.</summary>
        ConfiguradoPeloTecnico = 1,

        /// <summary>Limite declarado na ficha ou na identificação do próprio componente.</summary>
        Fabricante = 2,

        /// <summary>
        /// O componente relatou os próprios limiares de aviso/crítico por log (por exemplo, o
        /// <em>warning/critical temperature threshold</em> do NVMe). É a melhor referência possível.
        /// </summary>
        ProprioComponente = 3,
    }

    /// <summary>
    /// Comparação entre o limite adotado e a ficha do fabricante, ou a falta dela.
    /// </summary>
    /// <remarks>
    /// Existe para separar "não tenho a ficha" de "comparei e é diferente". Uma ausência declarada
    /// é informação; um silêncio não é.
    /// </remarks>
    public enum ComparacaoComFabricante
    {
        /// <summary>Não há ficha do fabricante disponível: o limite adotado é <see cref="RotuloLimiteGenerico"/>.</summary>
        Desconhecida = 0,

        /// <summary>A ficha do fabricante foi localizada e o limite adotado não é maior que ela.</summary>
        DentroDoFabricante = 1,

        /// <summary>A ficha do fabricante foi localizada e o limite adotado é maior: precisa de justificativa.</summary>
        AcimaDoFabricante = 2,
    }

    /// <summary>
    /// Limite térmico com procedência. O valor sozinho não basta: quem lê o laudo precisa saber se
    /// ele é o padrão do programa ou a especificação do fabricante.
    /// </summary>
    /// <param name="Celsius">O valor do limite, em graus Celsius.</param>
    /// <param name="Origem">De onde o número veio.</param>
    /// <param name="Fonte">Nome da fonte em linguagem de técnico (fabricante, campo do log, arquivo).</param>
    /// <param name="Rotulo">
    /// Como o limite deve ser apresentado quando é o padrão do programa —
    /// <see cref="RotuloLimiteGenerico"/> enquanto não houver dado do próprio componente.
    /// </param>
    /// <param name="Comparacao">Relação com a ficha do fabricante, quando conhecida.</param>
    public readonly record struct LimiteTermico(
        double Celsius,
        OrigemLimite Origem,
        string Fonte,
        string Rotulo,
        ComparacaoComFabricante Comparacao)
    {
        /// <summary>
        /// O limite é apenas um valor de partida, sem fonte do componente nem do fabricante?
        /// </summary>
        /// <remarks>
        /// O que decide é a <b>origem</b>, não a comparação: um limite declarado pelo próprio
        /// componente não é genérico só porque não foi confrontado com um datasheet. O padrão do
        /// programa é genérico mesmo que alguém tenha anotado uma comparação.
        /// </remarks>
        public bool EhGenerico => Origem == OrigemLimite.PadraoDoPrograma;

        /// <summary>
        /// Como o limite aparece no laudo, com a ressalva obrigatória quando é genérico.
        /// </summary>
        public string Descrever() => EhGenerico
            ? $"{Celsius:0} °C ({Rotulo}; valor de partida — ajuste pela experiência de bancada e pela ficha do fabricante)"
            : $"{Celsius:0} °C ({OrigemNome} — {Fonte})";

        private string OrigemNome => Origem switch
        {
            OrigemLimite.Fabricante => "ficha do fabricante",
            OrigemLimite.ProprioComponente => "limiar declarado pelo próprio componente",
            OrigemLimite.ConfiguradoPeloTecnico => "configuração do técnico",
            _ => "padrão do programa",
        };
    }

    // ---------------------------------------------------------------- correção de configuração

    /// <summary>
    /// Resultado da validação de uma combinação de limites: os valores que valem e por quê.
    /// </summary>
    /// <param name="VereditoCpuCelsius">Limite efetivo do veredito da CPU.</param>
    /// <param name="AlertaVisualCelsius">Limite efetivo do alerta visual do gráfico.</param>
    /// <param name="ParadaAutomaticaCelsius">Limite efetivo da parada automática.</param>
    /// <param name="EmergenciaCelsius">Limite de emergência (sempre <see cref="EmergenciaCelsius"/>).</param>
    /// <param name="Corrigido">Algum valor precisou ser trocado pelo padrão.</param>
    /// <param name="Motivos">
    /// Motivos em pt-BR, um por correção. Vazio quando a combinação é válida. Nunca <c>null</c>.
    /// </param>
    public sealed record ResultadoLimites(
        double VereditoCpuCelsius,
        double AlertaVisualCelsius,
        double ParadaAutomaticaCelsius,
        double EmergenciaCelsius,
        bool Corrigido,
        IReadOnlyList<string> Motivos)
    {
        /// <summary>Motivos em uma linha, pronta para a tela.</summary>
        public string MotivosTexto => string.Join(" ", Motivos);
    }

    /// <summary>
    /// Verifica a combinação de limites e devolve <b>valores corrigidos</b> quando ela é inválida,
    /// junto com o motivo de cada correção.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As invariantes, todas obrigatórias:
    /// <list type="number">
    /// <item><c>veredito &lt;= alerta</c> — avisar depois de já ter reprovado não faz sentido;</item>
    /// <item><c>alerta &lt;= parada</c> — parar antes do aviso faria o aviso nunca aparecer;</item>
    /// <item><c>parada &lt; emergência</c> — a parada normal precisa agir antes da emergência;</item>
    /// <item><c>parada &lt;= </c><see cref="ParadaMaximaCelsius"/> — proteção tardia não protege;</item>
    /// <item>todo valor precisa ser finito e estar dentro de 1–120 °C.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Uma configuração inválida <b>nunca</b> é aceita em silêncio: o chamador recebe
    /// <see cref="ResultadoLimites.Corrigido"/> e os motivos, e deve mostrá-los. O padrão de cada
    /// campo é reposto individualmente, na ordem da cadeia, para que um número absurdo não derrube
    /// os outros campos que estavam corretos.
    /// </para>
    /// </remarks>
    public static ResultadoLimites Validar(
        double vereditoCpuCelsius,
        double alertaVisualCelsius,
        double paradaAutomaticaCelsius)
    {
        var motivos = new List<string>();

        var veredito = Valido(vereditoCpuCelsius)
            ? vereditoCpuCelsius
            : Repor("O limite de veredito da CPU", vereditoCpuCelsius, VereditoCpuCelsius, motivos);

        var alerta = Valido(alertaVisualCelsius)
            ? alertaVisualCelsius
            : Repor("O limiar de alerta visual", alertaVisualCelsius, AlertaVisualCelsius, motivos);

        var parada = Valido(paradaAutomaticaCelsius)
            ? paradaAutomaticaCelsius
            : Repor("O limite de parada automática", paradaAutomaticaCelsius, ParadaAutomaticaCelsius, motivos);

        // A cadeia é aplicada sobre os valores já normalizados, na ordem em que a sequência acontece.
        // Cada correção pode empurrar o seguinte, então o laço repete até estabilizar.
        for (var volta = 0; volta < 3; volta++)
        {
            var mudou = false;

            if (veredito > alerta)
            {
                motivos.Add($"O limite de veredito da CPU ({Numero(veredito)} °C) não pode ser maior que o " +
                            $"limiar de alerta visual ({Numero(alerta)} °C); o veredito foi reposto ao padrão " +
                            $"de {Numero(VereditoCpuCelsius)} °C.");
                veredito = VereditoCpuCelsius;
                mudou = true;
            }

            if (alerta > parada)
            {
                motivos.Add($"O limiar de alerta visual ({Numero(alerta)} °C) não pode passar do limite de " +
                            $"parada automática ({Numero(parada)} °C), senão o aviso nunca apareceria; o alerta " +
                            $"foi reposto ao padrão de {Numero(AlertaVisualCelsius)} °C.");
                alerta = AlertaVisualCelsius;
                mudou = true;
            }

            if (parada > ParadaMaximaCelsius)
            {
                motivos.Add($"O limite de parada automática ({Numero(parada)} °C) não pode passar de " +
                            $"{Numero(ParadaMaximaCelsius)} °C: acima disso a proteção chega tarde demais. " +
                            $"Foi reposto ao padrão de {Numero(ParadaAutomaticaCelsius)} °C.");
                parada = ParadaAutomaticaCelsius;
                mudou = true;
            }

            if (parada >= EmergenciaCelsius)
            {
                motivos.Add($"O limite de parada automática ({Numero(parada)} °C) precisa ser menor que a " +
                            $"emergência de {Numero(EmergenciaCelsius)} °C; foi reposto ao padrão de " +
                            $"{Numero(ParadaAutomaticaCelsius)} °C.");
                parada = ParadaAutomaticaCelsius;
                mudou = true;
            }

            if (!mudou)
            {
                break;
            }
        }

        // Garantia final. O laço acima conserta a combinação campo a campo, mas reposição individual
        // pode, em combinações hostis, terminar numa cadeia ainda quebrada (ex.: alerta 60 °C com
        // veredito reposto a 95 °C). Um limite que "quase" fecha a invariante é exatamente o tipo de
        // configuração que não pode valer, então aqui a combinação inteira volta ao padrão — que é
        // válido por construção (ver ValidarPadroes) e é o único conjunto em que se pode confiar.
        if (!CadeiaValida(veredito, alerta, parada))
        {
            motivos.Add($"A combinação de limites ({Numero(veredito)} °C de veredito, {Numero(alerta)} °C de " +
                        $"alerta e {Numero(parada)} °C de parada) não fecha a regra " +
                        $"veredito ≤ alerta ≤ parada < emergência ({Numero(EmergenciaCelsius)} °C); os três " +
                        $"limites voltaram aos padrões de {Numero(VereditoCpuCelsius)} °C, " +
                        $"{Numero(AlertaVisualCelsius)} °C e {Numero(ParadaAutomaticaCelsius)} °C.");
            veredito = VereditoCpuCelsius;
            alerta = AlertaVisualCelsius;
            parada = ParadaAutomaticaCelsius;
        }

        return new ResultadoLimites(veredito, alerta, parada, EmergenciaCelsius, motivos.Count > 0, motivos);
    }

    /// <summary>
    /// A combinação fecha as invariantes? Usado tanto pela correção quanto pelos testes, para que não
    /// exista uma definição de "válido" no código e outra no teste.
    /// </summary>
    public static bool CadeiaValida(double vereditoCpuCelsius, double alertaVisualCelsius, double paradaAutomaticaCelsius) =>
        vereditoCpuCelsius <= alertaVisualCelsius &&
        alertaVisualCelsius <= paradaAutomaticaCelsius &&
        paradaAutomaticaCelsius < EmergenciaCelsius &&
        paradaAutomaticaCelsius <= ParadaMaximaCelsius;

    /// <summary>Valida a combinação atual dos padrões. Serve para provar a invariante do próprio código.</summary>
    public static ResultadoLimites ValidarPadroes() => Validar(VereditoCpuCelsius, AlertaVisualCelsius, ParadaAutomaticaCelsius);

    private static bool Valido(double celsius) =>
        !double.IsNaN(celsius) && !double.IsInfinity(celsius) &&
        celsius >= TemperaturaFisica.MinimoValidoCelsius &&
        celsius <= TemperaturaFisica.MaximoValidoCelsius;

    private static double Repor(string nome, double valor, double padrao, List<string> motivos)
    {
        motivos.Add($"{nome} ({Numero(valor)} °C) está fora da faixa utilizável de " +
                    $"{Numero(TemperaturaFisica.MinimoValidoCelsius)} a {Numero(TemperaturaFisica.MaximoValidoCelsius)} °C; " +
                    $"foi reposto ao padrão de {Numero(padrao)} °C.");
        return padrao;
    }

    private static string Numero(double valor) => valor.ToString("0.#", FormatacaoBr.Cultura);

    // ---------------------------------------------------------------- limites por componente

    /// <summary>
    /// Limite térmico do componente. CPU, GPU, memória e placa derivam do limite da CPU informado;
    /// armazenamento depende do tipo de mídia (ver <see cref="LimiteDoArmazenamento"/>).
    /// </summary>
    /// <param name="tipo">Tipo do componente que expôs o sensor.</param>
    /// <param name="limiteCpuCelsius">
    /// Limite da CPU em uso. Quando negativo ou não finito, o padrão <see cref="VereditoCpuCelsius"/>
    /// é usado — nunca se propaga um limite inválido adiante.
    /// </param>
    public static double LimiteDoComponente(TipoHardware tipo, double limiteCpuCelsius) => tipo switch
    {
        TipoHardware.Cpu => LimiteCpuEfetivo(limiteCpuCelsius),
        TipoHardware.Gpu => Math.Max(MinimoGpuCelsius, LimiteCpuEfetivo(limiteCpuCelsius) + MargemGpuCelsius),
        TipoHardware.Memoria => MemoriaCelsius,

        // Sem a mídia do disco, o limite mais conservador dos três é o que vale: um HDD confundido
        // com SSD passaria a ser aprovado 25 °C acima do que deveria.
        TipoHardware.Armazenamento => HddCelsius,

        // Placa e SuperIO medem sensores dispersos; o limite da CPU serve de teto folgado.
        _ => LimiteCpuEfetivo(limiteCpuCelsius),
    };

    /// <summary>
    /// Valor numérico do limite de armazenamento pela mídia, sem procedência. Para o laudo, prefira
    /// <see cref="LimiteDoArmazenamentoComProcedencia"/>, que carrega a origem do número.
    /// <paramref name="nvme"/> tem precedência sobre <paramref name="midia"/>, porque um NVMe é
    /// sempre SSD e o barramento é o que define o regime térmico.
    /// </summary>
    /// <param name="midia">Mídia informada pelo disco (<see cref="TipoMidia.Ssd"/>, <see cref="TipoMidia.Hdd"/>…).</param>
    /// <param name="nvme">O disco se identificou como NVMe?</param>
    public static double LimiteDoArmazenamento(TipoMidia midia, bool nvme) => nvme
        ? NvmeCelsius
        : midia switch
        {
            TipoMidia.Hdd => HddCelsius,
            TipoMidia.Ssd => SsdSataCelsius,

            // Mídia desconhecida: o menor limite entre os plausíveis. Na dúvida, o aviso chega cedo.
            _ => HddCelsius,
        };

    /// <summary>
    /// Procedência do limite de armazenamento. Quando o disco informa os próprios limiares de
    /// aviso/crítico (caso típico do NVMe), <paramref name="limiarDoComponenteCelsius"/> é
    /// <b>preferido</b> como referência e a fonte é registrada; sem esse dado, vale o padrão genérico,
    /// que sai rotulado como <see cref="RotuloLimiteGenerico"/>.
    /// </summary>
    /// <param name="midia">Mídia informada pelo disco.</param>
    /// <param name="nvme">O disco se identificou como NVMe?</param>
    /// <param name="limiarDoComponenteCelsius">
    /// Limiar de aviso declarado pelo próprio disco, quando conhecido. <c>null</c> quando o log ou a
    /// identificação não trouxe o dado.
    /// </param>
    /// <param name="fonteDoComponente">Campo ou comando de onde o limiar do componente foi lido.</param>
    public static LimiteTermico LimiteDoArmazenamentoComProcedencia(
        TipoMidia midia,
        bool nvme,
        double? limiarDoComponenteCelsius = null,
        string? fonteDoComponente = null)
    {
        if (limiarDoComponenteCelsius is { } doComponente && Valido(doComponente))
        {
            return new LimiteTermico(
                doComponente,
                OrigemLimite.ProprioComponente,
                string.IsNullOrWhiteSpace(fonteDoComponente) ? "identificação do próprio disco" : fonteDoComponente,
                RotuloLimiteGenerico,
                ComparacaoComFabricante.Desconhecida);
        }

        var padrao = LimiteDoArmazenamento(midia, nvme);
        var descricaoMidia = nvme ? "NVMe" : midia switch
        {
            TipoMidia.Hdd => "HDD",
            TipoMidia.Ssd => "SSD SATA",
            _ => "mídia não informada",
        };

        return new LimiteTermico(
            padrao,
            OrigemLimite.PadraoDoPrograma,
            $"padrão de partida para {descricaoMidia}",
            RotuloLimiteGenerico,
            ComparacaoComFabricante.Desconhecida);
    }

    /// <summary>Limite da CPU efetivamente aplicado, com o padrão quando o informado não serve.</summary>
    private static double LimiteCpuEfetivo(double limiteCpuCelsius) =>
        Valido(limiteCpuCelsius) ? limiteCpuCelsius : VereditoCpuCelsius;
}
