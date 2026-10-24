namespace FlavosDiag.Tests;

/// <summary>
/// Travas de configuração do coletor de sensores, conferidas no código-fonte.
/// </summary>
/// <remarks>
/// O caso que motivou estes testes: o coletor criava a sessão com <c>IsGpuEnabled = false</c>, então
/// a tela de stress mostrava "sem leitura" para a GPU mesmo com o sensor respondendo 48 °C. Nenhum
/// teste pegava isso, porque o classificador de temperatura era testado isolado e funcionava.
/// A leitura do fonte é necessária aqui: abrir a sessão real exigiria driver e elevação, o que não
/// pode rodar num teste automatizado.
/// </remarks>
public static class SensoresConfiguracaoTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        yield return ("Sensores: o coletor liga a leitura de GPU", ColetorLigaGpu);
        yield return ("Sensores: o coletor liga a leitura de CPU", ColetorLigaCpu);
    }

    private static Task ColetorLigaGpu()
    {
        var texto = LerProvider();

        Assert.False(
            texto.Contains("IsGpuEnabled = false", StringComparison.Ordinal),
            "o gráfico de stress promete temperatura da GPU; com IsGpuEnabled = false a GPU fica 'sem leitura'");

        Assert.True(
            texto.Contains("IsGpuEnabled = true", StringComparison.Ordinal),
            "o coletor precisa habilitar a GPU para o gráfico de temperatura");
        return Task.CompletedTask;
    }

    private static Task ColetorLigaCpu()
    {
        var texto = LerProvider();
        Assert.True(
            texto.Contains("IsCpuEnabled = true", StringComparison.Ordinal),
            "a temperatura da CPU é o eixo principal do gráfico de stress");
        return Task.CompletedTask;
    }

    private static string LerProvider()
    {
        var raiz = EncontrarRaiz();
        var caminho = Path.Combine(
            raiz,
            "src",
            "FlavosDiag.Providers",
            "Sensores",
            "SensoresHardwareProvider.cs");

        Assert.True(File.Exists(caminho), $"arquivo do coletor não encontrado: {caminho}");
        return File.ReadAllText(caminho);
    }

    /// <summary>Sobe do diretório de saída até achar a raiz do repositório (a que contém src/).</summary>
    private static string EncontrarRaiz()
    {
        var diretorio = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && diretorio is not null; i++)
        {
            if (Directory.Exists(Path.Combine(diretorio, "src", "FlavosDiag.Providers")))
            {
                return diretorio;
            }

            diretorio = Path.GetDirectoryName(diretorio.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new InvalidOperationException(
            $"não foi possível localizar a raiz do repositório a partir de {AppContext.BaseDirectory}");
    }
}
