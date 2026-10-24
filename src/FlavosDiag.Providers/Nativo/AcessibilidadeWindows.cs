using System.IO;
using System.Runtime.InteropServices;

namespace FlavosDiag.Providers.Nativo;

/// <summary>Leituras do Windows relacionadas a acessibilidade e relatório de energia (somente leitura).</summary>
public static class AcessibilidadeWindows
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

    private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    /// <summary>Respeita a configuração de redução de movimento/animações do Windows.</summary>
    public static bool AnimacoesHabilitadas
    {
        get
        {
            var habilitado = true;
            try
            {
                SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref habilitado, 0);
            }
            catch (Exception)
            {
                return true;
            }

            return habilitado;
        }
    }

    /// <summary>Último XML de relatório de bateria gerado pelo Windows (não gera nada novo).</summary>
    public static string? CaminhoRelatorioBateria()
    {
        var raiz = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "PowerShell");
        if (!Directory.Exists(raiz))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(raiz, "battery-report*.xml", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Lê o relatório de bateria em XML (formato documentado pela Microsoft) como pares nome → valor.</summary>
    public static FlavosDiag.Core.Leitura<Dictionary<string, string>> LerRelatorioBateria(string caminho)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Load(caminho);
            var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in doc.Descendants())
            {
                if (el.HasElements || string.IsNullOrWhiteSpace(el.Value))
                {
                    continue;
                }

                valores[el.Name.LocalName] = el.Value.Trim();
            }

            return FlavosDiag.Core.Leitura<Dictionary<string, string>>.Ok(valores);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return FlavosDiag.Core.Leitura<Dictionary<string, string>>.Falha("Arquivo de relatório de bateria ilegível: " + ex.Message);
        }
    }
}
