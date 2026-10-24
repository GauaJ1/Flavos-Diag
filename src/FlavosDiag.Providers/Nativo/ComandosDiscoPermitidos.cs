namespace FlavosDiag.Providers.Nativo;

/// <summary>
/// Lista de permitidos dos comandos que o Flavos Diag pode enviar a um disco. Só entram aqui comandos
/// de <b>leitura</b>: identificação, leitura de atributos SMART, leitura de limiares SMART e leitura de
/// log. Qualquer outro byte é recusado em <see cref="RequisicaoDisco"/> — não existe caminho no
/// aplicativo que monte um comando por fora desta lista.
/// </summary>
/// <remarks>
/// <para>
/// O motivo desta restrição: o Windows só repassa certos IOCTL de pass-through a um handle aberto com
/// acesso de escrita (ver <c>docs/DECISOES.md</c> D31 e D78). O handle tem permissão de escrita, então a
/// única coisa que impede um comando de escrita de chegar ao disco do cliente é a lista abaixo. Ela é a
/// fronteira de segurança: os bytes de comando não vêm de chamador nenhum, vêm apenas daqui.
/// </para>
/// <para>
/// Os valores são os das especificações públicas e batem com os cabeçalhos oficiais:
/// ATA — <c>WIN_IDENTIFY = 0xEC</c>, <c>WIN_SMART = 0xB0</c>, <c>SMART_READ_VALUES = 0xD0</c>,
/// <c>SMART_READ_THRESHOLDS = 0xD1</c> (ATA/ATAPI-8; conferido em <c>include/uapi/linux/hdreg.h</c>);
/// NVMe — <c>nvme_admin_identify = 0x06</c>, <c>nvme_admin_get_log_page = 0x02</c>,
/// <c>nvme_admin_get_features = 0x0A</c>, <c>NVME_LOG_SMART = 0x02</c>
/// (NVMe Base Specification; conferido em <c>include/linux/nvme.h</c>).
/// </para>
/// <para>
/// Só constam os comandos que o código realmente envia hoje. Os demais (leitura de limiares, READ LOG EXT
/// 0x2F, NVMe Identify e Get Features) têm o opcode registrado aqui para ficar pronto, porém fora da
/// lista em vigor até existir chamador: um opcode "previsto" que já fosse aceito seria superfície extra
/// sem uso. Incluir um comando novo é editar esta lista de propósito e justificar em revisão.
/// </para>
/// </remarks>
public static class ComandosDiscoPermitidos
{
    // ---- Opcodes ATA (ATA/ATAPI-8) ----

    /// <summary>IDENTIFY DEVICE — devolve o bloco de identificação de 512 bytes. Somente leitura.</summary>
    public const byte AtaIdentifyDevice = 0xEC;

    /// <summary>SMART — comando genérico cujo subcomando vai no registrador Features. Somente leitura.</summary>
    public const byte AtaSmart = 0xB0;

    /// <summary>
    /// Subcomando SMART READ DATA (usado em Features quando o comando é <see cref="AtaSmart"/>).
    /// Também é o byte que o código envia diretamente no registrador Command, na forma tarefa de
    /// <c>SMART_RCV_DRIVE_DATA</c> que o driver de disco do Windows repassa.
    /// </summary>
    public const byte AtaSmartReadData = 0xD0;

    /// <summary>Subcomando SMART READ THRESHOLDS (usado em Features quando o comando é <see cref="AtaSmart"/>).</summary>
    public const byte AtaSmartReadThresholds = 0xD1;

    /// <summary>READ LOG EXT — leitura de log estendido (General Purpose Logging). Somente leitura.</summary>
    public const byte AtaReadLogExt = 0x2F;

    // ---- Opcodes NVMe (Admin Command Set) ----

    /// <summary>NVMe Identify — descreve o controlador/namespace. Somente leitura.</summary>
    public const byte NvmeIdentify = 0x06;

    /// <summary>NVMe Get Log Page — devolve uma página de log. Somente leitura.</summary>
    public const byte NvmeGetLogPage = 0x02;

    /// <summary>NVMe Get Features — lê o valor de uma feature. Somente leitura.</summary>
    public const byte NvmeGetFeatures = 0x0A;

    /// <summary>Identificador da página de log SMART/Health do NVMe.</summary>
    public const byte NvmeLogPageSmartHealth = 0x02;

    /// <summary>
    /// Comandos ATA aceitos, mapeados para o motivo legível. A chave é o byte do registrador Command.
    /// Apenas <see cref="AtaIdentifyDevice"/> e <see cref="AtaSmartReadData"/> são usados hoje.
    /// </summary>
    private static readonly IReadOnlyDictionary<byte, string> _ataEmVigor =
        new Dictionary<byte, string>
        {
            [AtaIdentifyDevice] = "ATA IDENTIFY DEVICE (leitura)",
            [AtaSmartReadData] = "ATA SMART READ DATA (leitura)",
        };

    /// <summary>
    /// Subcomandos aceitos em Features quando o comando é <see cref="AtaSmart"/>. Se o byte do subcomando
    /// não estiver aqui, o comando é recusado mesmo com o Command na lista.
    /// </summary>
    private static readonly IReadOnlyDictionary<byte, string> _subcomandosSmartEmVigor =
        new Dictionary<byte, string>
        {
            [AtaSmartReadData] = "ATA SMART READ DATA (leitura)",
            [AtaSmartReadThresholds] = "ATA SMART READ THRESHOLDS (leitura)",
        };

    /// <summary>Comandos NVMe aceitos (Admin Command Set). Apenas <see cref="NvmeGetLogPage"/> é usado hoje.</summary>
    private static readonly IReadOnlyDictionary<byte, string> _nvmeEmVigor =
        new Dictionary<byte, string>
        {
            [NvmeGetLogPage] = "NVMe Get Log Page (leitura)",
        };

    /// <summary>Opcode ATA do registrador Command permitido, ou <c>null</c> se o byte não estiver na lista.</summary>
    public static string? DescreverAta(byte comando) =>
        _ataEmVigor.TryGetValue(comando, out var descricao) ? descricao : null;

    /// <summary>
    /// Descreve um comando ATA SMART (<c>0xB0</c>) com o subcomando em Features, ou <c>null</c> quando o
    /// par não é permitido. Um subcomando de escrita (ex.: <c>0xD6</c> SMART WRITE LOG) é recusado.
    /// </summary>
    public static string? DescreverAtaSmart(byte feature) =>
        _subcomandosSmartEmVigor.TryGetValue(feature, out var descricao) ? descricao : null;

    /// <summary>Opcode NVMe permitido, ou <c>null</c> se o byte não estiver na lista.</summary>
    public static string? DescreverNvme(byte opcode) =>
        _nvmeEmVigor.TryGetValue(opcode, out var descricao) ? descricao : null;

    /// <summary>Todos os opcodes ATA aceitos hoje (para testes e para a mensagem de recusa).</summary>
    public static IReadOnlyCollection<byte> OpcodesAtaEmVigor => (IReadOnlyCollection<byte>)_ataEmVigor.Keys;

    /// <summary>Todos os subcomandos SMART aceitos hoje (Features de <c>0xB0</c>).</summary>
    public static IReadOnlyCollection<byte> SubcomandosSmartEmVigor => (IReadOnlyCollection<byte>)_subcomandosSmartEmVigor.Keys;

    /// <summary>Todos os opcodes NVMe aceitos hoje.</summary>
    public static IReadOnlyCollection<byte> OpcodesNvmeEmVigor => (IReadOnlyCollection<byte>)_nvmeEmVigor.Keys;

    /// <summary>
    /// Lista completa e legível do que está permitido — usada nas mensagens de recusa, para que o erro
    /// diga exatamente o que é aceitável em vez de apenas "comando inválido".
    /// </summary>
    public static IReadOnlyList<string> DescricaoDaLista =>
        _ataEmVigor.Values
            .Concat(_subcomandosSmartEmVigor.Values)
            .Concat(_nvmeEmVigor.Values)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
