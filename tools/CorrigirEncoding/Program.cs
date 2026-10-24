using System.Text;

// Ferramenta de manutencao (nao faz parte do produto): conserta arquivos de texto com dupla
// codificacao UTF-8 (o texto foi gravado uma vez em UTF-8 e lido de novo como Latin-1).
// Uso: dotnet run --project tools/CorrigirEncoding -- <pasta> [--aplicar]
var pasta = args.Length > 0 ? args[0] : ".";
var aplicar = args.Contains("--aplicar");
var utf8 = new UTF8Encoding(false);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var latin1 = Encoding.GetEncoding(28591);
var extensoes = new[] { ".cs", ".xaml", ".md", ".ps1", ".csproj", ".props", ".json", ".sln", ".txt" };
var assinatura = new[] { "\u00C3\u00A1", "\u00C3\u00A7", "\u00C3\u00A3", "\u00C3\u00A9", "\u00C3\u00B3", "\u00C2\u00A0", "\u00E2\u20AC\u201D" };

var corrigidos = 0;
foreach (var arquivo in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories))
{
    if (!extensoes.Contains(Path.GetExtension(arquivo), StringComparer.OrdinalIgnoreCase))
    {
        continue;
    }

    if (arquivo.Contains("\\obj\\") || arquivo.Contains("\\bin\\") || arquivo.Contains("\\artifacts\\") || arquivo.Contains("\\dist\\") || arquivo.Contains("\\tools\\"))
    {
        continue;
    }

    var bytes = File.ReadAllBytes(arquivo);
    if (Array.IndexOf(bytes, (byte)0) >= 0)
    {
        continue;
    }

    var texto = utf8.GetString(bytes);
    if (!assinatura.Any(texto.Contains))
    {
        continue;
    }

    var tentativa = utf8.GetString(latin1.GetBytes(texto));
    if (assinatura.Any(tentativa.Contains) || tentativa.Contains('\uFFFD'))
    {
        Console.WriteLine("NAO RESOLVIDO: " + arquivo);
        continue;
    }

    corrigidos++;
    Console.WriteLine("CORRIGIDO: " + arquivo);
    if (aplicar)
    {
        File.WriteAllText(arquivo, tentativa, utf8);
    }
}

Console.WriteLine($"Arquivos com dupla codificacao: {corrigidos}" + (aplicar ? " (corrigidos)" : " (use --aplicar)"));
return 0;
