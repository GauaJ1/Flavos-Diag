using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;
using FlavosDiag.Providers.Sensores;

namespace FlavosDiag.App.Services;

/// <summary>
/// Reúne os fatos do ambiente e a última leitura para explicar por que a temperatura não aparece.
/// Fica num serviço para que Resumo, CPU e Stress mostrem exatamente a mesma causa — três textos
/// diferentes para o mesmo problema confundem mais do que ajudam.
/// </summary>
public sealed class ServicoDiagnosticoSensores
{
    private readonly Servicos _servicos;

    public ServicoDiagnosticoSensores(Servicos servicos) => _servicos = servicos;

    /// <summary>Fatos do ambiente, sem tocar em sensores (elevação e PawnIO).</summary>
    public FatosSensores FatosDaAbertura(AmostraSensores? ultima = null, string? mensagem = null)
    {
        var (termicos, comLeitura) = ContarTermicos(ultima);
        return DiagnosticoSensores.Avaliar(
            ProcessoElevado(),
            PawnIoInstalado(),
            termicos,
            comLeitura,
            mensagem);
    }

    /// <summary>
    /// Avalia a situação a partir da última amostra conhecida. Devolve o texto pronto para a tela
    /// ou <c>null</c> quando há leitura e nada precisa ser avisado.
    /// </summary>
    public string? Avaliar(AmostraSensores? ultima, string componente = "CPU")
    {
        var fatos = FatosDaAbertura(ultima);
        var texto = DiagnosticoSensores.TextoDeCausa(fatos, componente);
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    /// <summary>Fatos completos para exportação: inclui o que a última amostra mostrou.</summary>
    public FatosSensores FatosCompletos(AmostraSensores? ultima) => FatosDaAbertura(ultima);

    public bool ProcessoElevado()
    {
        try
        {
            return _servicos.Elevacao.Elevado;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    /// <summary>
    /// Consulta o PawnIO pela API pública da biblioteca. Devolve <c>null</c> quando não for possível
    /// saber — e nesse caso a tela diz "não determinado" em vez de afirmar uma causa.
    /// </summary>
    public bool? PawnIoInstalado() => SensoresHardwareProvider.ConsultarPawnIo();

    private static (int Termicos, int ComLeitura) ContarTermicos(AmostraSensores? amostra)
    {
        if (amostra is null)
        {
            return (0, 0);
        }

        var comLeitura = amostra.Valores.Count(v =>
            v.Categoria == CategoriaSensor.Temperatura &&
            v.TipoHardware is TipoHardware.Cpu or TipoHardware.Gpu &&
            v.Qualidade == QualidadeSensor.Ok);

        var semLeitura = amostra.SemLeitura.Count(s =>
            s.Categoria == CategoriaSensor.Temperatura &&
            s.TipoHardware is TipoHardware.Cpu or TipoHardware.Gpu);

        return (comLeitura + semLeitura, comLeitura);
    }
}
