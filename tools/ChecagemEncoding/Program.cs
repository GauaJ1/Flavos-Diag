using System.Text;

// Ferramenta de manutenção (não faz parte do produto): verifica e corrige caracteres corrompidos.
// Uso: dotnet run --project tools/ChecagemEncoding -- <pasta> [--corrigir]
var pasta = args.Length > 0 ? args[0] : ".";
var corrigir = args.Contains("--corrigir");
var utf8 = new UTF8Encoding(false);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var latin1 = Encoding.GetEncoding(28591);
var substituicao = (char)0xFFFD;
var extensoes = new[] { ".cs", ".xaml", ".md", ".ps1", ".csproj", ".props", ".json", ".sln" };

var problemas = 0;
foreach (var arquivo in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories))
{
    if (!extensoes.Contains(Path.GetExtension(arquivo), StringComparer.OrdinalIgnoreCase))
    {
        continue;
    }

    if (arquivo.Contains("\\obj\\") || arquivo.Contains("\\bin\\") || arquivo.Contains("\\artifacts\\") || arquivo.Contains("\\dist\\") || arquivo.Contains("\\ChecagemEncoding\\"))
    {
        continue;
    }

    var bytes = File.ReadAllBytes(arquivo);
    if (Array.IndexOf(bytes, (byte)0) >= 0)
    {
        continue;
    }

    var texto = utf8.GetString(bytes);
    var dupla = texto.Contains('Ã') || texto.Contains('Â');

    if (dupla)
    {
        var tentativa = utf8.GetString(latin1.GetBytes(texto));
        if (!tentativa.Contains('Ã') && !tentativa.Contains('Â') && !tentativa.Contains(substituicao))
        {
            problemas++;
            Console.WriteLine($"DUPLA CODIFICACAO: {arquivo}");
            if (corrigir)
            {
                File.WriteAllText(arquivo, tentativa, utf8);
            }

            continue;
        }
    }

    if (texto.Contains(substituicao))
    {
        var linhas = File.ReadAllLines(arquivo).Select((conteudo, indice) => (conteudo, indice))
            .Where(x => x.conteudo.Contains(substituicao)).ToList();
        if (corrigir)
        {
            // Recuperação impossível sem intervenção manual: apenas reporta.
            Console.WriteLine($"PERDA IRRECUPERAVEL: {arquivo} ({linhas.Count} linha(s))");
            foreach (var (conteudo, indice) in linhas)
            {
                Console.WriteLine($"  linha {indice + 1}: {conteudo.Trim()}");
            }

            problemas++;
            continue;
        }

        problemas++;
        Console.WriteLine($"CARACTERE INVALIDO: {arquivo} ({linhas.Count} linha(s))");
        foreach (var (conteudo, indice) in linhas)
        {
            Console.WriteLine($"  linha {indice + 1}: {conteudo.Trim()}");
        }
    }
}

Console.WriteLine($"Arquivos com problema: {problemas}");
return problemas == 0 ? 0 : 1;
