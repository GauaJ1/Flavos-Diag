namespace FlavosDiag.Providers.Nativo;

/// <summary>
/// Leitura do log de saúde NVMe (NVMe Base Specification, "Get Log Page – SMART / Health Information").
/// O envio passa por <see cref="RequisicaoDisco"/>, o único caminho que abre o disco com acesso de
/// escrita e monta o comando a partir da lista de permitidos.
/// </summary>
public static class SmartNvme
{
    /// <summary>Identificador do log de saúde SMART/Health do NVMe.</summary>
    public const byte LogPageSmartHealth = ComandosDiscoPermitidos.NvmeLogPageSmartHealth;

    /// <summary>
    /// Lê o log de saúde. Devolve <c>null</c> quando o dispositivo não expõe o log por esta via ou quando
    /// a leitura foi recusada (sem elevação, sem consentimento); o chamador registra a limitação com o
    /// motivo devolvido em <paramref name="motivo"/>.
    /// </summary>
    /// <param name="numeroDisco">Índice do disco físico.</param>
    /// <param name="permissoes">Elevação e consentimento já conferidos pelo chamador.</param>
    /// <param name="motivo">Causa legível quando a leitura não acontece.</param>
    public static LogSaudeNvme? Ler(int numeroDisco, RequisicaoDisco.Permissoes permissoes, out string? motivo)
    {
        motivo = null;
        var resultado = RequisicaoDisco.Executar(numeroDisco, RequisicaoDisco.PedidoNvme.LerLogSaude, permissoes);
        if (!resultado.Sucesso || resultado.Dados is null)
        {
            motivo = resultado.Erro;
            return null;
        }

        return Interpretar(resultado.Dados);
    }

    /// <summary>
    /// Interpreta o log de saúde NVMe (função pura, testável com fixture de 512 bytes).
    /// Layout conforme a especificação: offsets em bytes a partir do início do log.
    /// </summary>
    public static LogSaudeNvme? Interpretar(byte[] dados)
    {
        if (dados is null || dados.Length < 512)
        {
            return null;
        }

        var avisoCritico = dados[0];
        var temperaturaKelvin = BitConverter.ToUInt16(dados, 1);
        var percentualUsado = dados[5];
        var espacoDisponivel = dados[3];
        var limiarEspaco = dados[4];
        var dadosLidos = BitConverter.ToUInt64(dados, 32) / 1000;      // unidades de 1000 setores *512B
        var dadosEscritos = BitConverter.ToUInt64(dados, 48) / 1000;
        var ciclos = BitConverter.ToUInt64(dados, 112);
        var horas = BitConverter.ToUInt64(dados, 128);
        var errosMidia = BitConverter.ToUInt64(dados, 160);

        return new LogSaudeNvme(
            AvisoCritico: avisoCritico,
            // "Sem leitura" precisa continuar sendo ausência: devolver 0 aqui viraria um "0 °C" real no laudo.
            TemperaturaCelsius: Temperatura.DeKelvinParaCelsius(temperaturaKelvin),
            PercentualVidaUsada: percentualUsado,
            EspacoDisponivelPercentual: espacoDisponivel,
            LimiarEspacoPercentual: limiarEspaco,
            DadosLidosGb: (long)dadosLidos,
            DadosEscritosGb: (long)dadosEscritos,
            HorasLigado: (long)horas,
            CiclosLigarDesligar: (long)ciclos,
            ErrosIntegridadeMidia: (long)errosMidia);
    }
}

/// <summary>Log de saúde NVMe já interpretado (unidades convertidas).</summary>
public sealed record LogSaudeNvme(
    int AvisoCritico,
    /// <summary>
    /// Temperatura em °C, ou <c>null</c> quando o controlador não informou uma leitura aproveitável
    /// (Kelvin &lt;= 273, sentinela 0xFFFF ou resultado fora de 1–120 °C). O <c>null</c> existe para que a
    /// ausência não seja confundida com uma medição real de 0 °C.
    /// </summary>
    int? TemperaturaCelsius,
    int PercentualVidaUsada,
    int EspacoDisponivelPercentual,
    int LimiarEspacoPercentual,
    long DadosLidosGb,
    long DadosEscritosGb,
    long HorasLigado,
    long CiclosLigarDesligar,
    long ErrosIntegridadeMidia)
{
    public bool PreveFalha => (AvisoCritico & (1 << 0)) != 0;

    public bool TemErroCritico => (AvisoCritico & 0xFE) != 0;
}
