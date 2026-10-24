using System.Text;

namespace FlavosDiag.Tests;

/// <summary>
/// Teste da documentação de decisões. A tabela de <c>docs/DECISOES.md</c> é a fonte que o projeto usa para
/// justificar comportamento; se ela quebrar (coluna faltando, número repetido, status apontando para decisão
/// que não existe), o documento deixa de ser confiável sem que ninguém perceba nenhum erro no build.
/// </summary>
/// <remarks>
/// Os casos são síncronos e embrulhados em <see cref="Task.Run(Action)"/> de propósito: um <c>async</c> sem
/// <c>await</c> renderia CS1998, que vira erro no build em Release com <c>-warnaserror</c>.
/// </remarks>
public static class DocumentacaoTests
{
    private const string ArquivoDecisoes = "docs/DECISOES.md";

    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        yield return ("Docs: todas as linhas da tabela de decisões têm o mesmo número de colunas", () => Task.Run(ColunasDaTabelaIguais));
        yield return ("Docs: nenhum número de decisão repetido", () => Task.Run(NenhumNumeroRepetido));
        yield return ("Docs: nenhuma célula de decisão termina em interrogação", () => Task.Run(NenhumaDecisaoTerminaComInterrogacao));
        yield return ("Docs: todo status aponta para uma decisão que existe", () => Task.Run(StatusApontamParaDecisaoExistente));
        yield return ("Docs: todo status é Vigente, Substituída por Dxx ou Revista por Dxx", () => Task.Run(StatusComValorPermitido));
        yield return ("Docs: toda decisão marcada como substítuida ou revista existe como alvo", () => Task.Run(MarcadasTemDestino));
        yield return ("Docs: toda entrada detalhada alcançável pela tabela ou pelo índice", () => Task.Run(EntradasDetalhadasAlcancaveis));
        yield return ("Docs: nenhuma linha de decisão é separador ou cabeçalho solto", () => Task.Run(SemLinhasEstruturaisNaTabela));
    }

    // ------------------------------------------------------------------ casos

    private static void ColunasDaTabelaIguais()
    {
        var linhas = LinhasDaTabela();
        Assert.True(linhas.Count > 0, "a tabela de decisões de " + ArquivoDecisoes + " não foi encontrada");

        var esperado = linhas[0].Length;
        Assert.Equal(5, esperado);

        for (var i = 1; i < linhas.Count; i++)
        {
            var celulas = linhas[i];
            Assert.True(
                celulas.Length == esperado,
                $"linha {celulas[0]} tem {celulas.Length} colunas; a tabela tem {esperado} (esperado: # | Decisão | Motivo | Impacto | Status)");
        }
    }

    private static void NenhumNumeroRepetido()
    {
        var vistos = new Dictionary<int, string>();
        foreach (var celulas in LinhasDaTabela())
        {
            var numero = Numero(celulas[0]);
            Assert.True(numero > 0, "linha sem número de decisão legível: " + celulas[0]);
            Assert.False(
                vistos.TryGetValue(numero, out var anterior),
                $"decisão D{numero} aparece duas vezes na tabela (\"{anterior}\" e \"{celulas[1]}\")");
            vistos[numero] = celulas[1];
        }
    }

    private static void NenhumaDecisaoTerminaComInterrogacao()
    {
        foreach (var celulas in LinhasDaTabela())
        {
            var decisao = celulas[1];
            Assert.False(
                decisao.TrimEnd().EndsWith('?'),
                $"D{Numero(celulas[0])} termina em interrogação: uma decisão não pode deixar pergunta aberta (\"{decisao}\")");

            // Pergunta no meio do texto: só conta interrogação seguida de fim, espaço ou aspas.
            // "int?" e "string?" são tipos anuláveis do C#, não perguntas, e aparecem naturalmente
            // em decisões sobre código.
            var perguntaNoMeio = System.Text.RegularExpressions.Regex.IsMatch(decisao, @"\?(?:\s|""|$|,)")
                                 && !System.Text.RegularExpressions.Regex.IsMatch(decisao, @"\b(int|string|double|long|bool|float|decimal)\?");
            Assert.False(
                perguntaNoMeio,
                $"D{Numero(celulas[0])} tem pergunta no meio do texto: reformule como decisão (\"{decisao}\")");
        }
    }

    private static void StatusApontamParaDecisaoExistente()
    {
        // Se a tabela estiver malformada, a mensagem do teste de colunas é a que explica o problema; aqui
        // só se devolve cedo para não estourar índice fora da faixa com um erro que não ajuda ninguém.
        if (LinhasDaTabela().Any(celulas => celulas.Length < 5))
        {
            return;
        }

        var existentes = NumerosDaTabela();
        foreach (var celulas in LinhasDaTabela())
        {
            var status = celulas[4].Trim();
            var alvo = AlvoDoStatus(status);
            if (alvo == 0)
            {
                continue;
            }

            Assert.True(
                existentes.Contains(alvo),
                $"D{Numero(celulas[0])} tem status \"{status}\", mas D{alvo} não existe na tabela");
            Assert.NotEqual(Numero(celulas[0]), alvo);
        }

        // Toda entrada detalhada também tem status na tabela; se alguma sumir de lá, o índice continua válido,
        // mas o status deixa de ser verificável.
        foreach (var numero in NumerosDasEntradasDetalhadas())
        {
            Assert.True(
                existentes.Contains(numero),
                $"a entrada detalhada D{numero} não aparece na tabela, então o status dela não é verificável");
        }
    }

    private static void StatusComValorPermitido()
    {
        if (LinhasDaTabela().Any(celulas => celulas.Length < 5))
        {
            return;
        }

        foreach (var celulas in LinhasDaTabela())
        {
            var status = celulas[4].Trim();
            var valido = status == "Vigente"
                         || AlvoDoStatus(status) > 0;
            Assert.True(
                valido,
                $"D{Numero(celulas[0])} tem status \"{status}\"; os únicos valores aceitos são " +
                "\"Vigente\", \"Substituída por Dxx\" e \"Revista por Dxx\"");

            if (AlvoDoStatus(status) == 0)
            {
                continue;
            }

            Assert.True(
                status.StartsWith("Substituída por D", StringComparison.Ordinal)
                || status.StartsWith("Revista por D", StringComparison.Ordinal),
                $"D{Numero(celulas[0])} tem status \"{status}\" sem o prefixo \"Substituída por \" ou \"Revista por \"");
        }
    }

    private static void MarcadasTemDestino()
    {
        // As marcações exigidas pela T5 existem e apontam para D72, D78 e D79. Testar explicitamente impede que
        // alguém "limpe" a tabela removendo o status e o teste de colunas continuar verde.
        var marcadas = new Dictionary<int, string>();
        foreach (var celulas in LinhasDaTabela())
        {
            marcadas[Numero(celulas[0])] = celulas[4].Trim();
        }

        Assert.Equal("Substituída por D72", valor(marcadas, 17));
        Assert.Equal("Substituída por D72", valor(marcadas, 65));
        Assert.Equal("Substituída por D79", valor(marcadas, 56));
        Assert.Equal("Substituída por D79", valor(marcadas, 58));
        Assert.Equal("Revista por D78", valor(marcadas, 68));
        Assert.Equal("Vigente", valor(marcadas, 72));
        Assert.Equal("Vigente", valor(marcadas, 78));
        Assert.Equal("Vigente", valor(marcadas, 79));
    }

    private static string valor(Dictionary<int, string> mapa, int numero)
    {
        Assert.True(mapa.ContainsKey(numero), $"D{numero} não existe na tabela de decisões");
        return mapa[numero];
    }

    private static void EntradasDetalhadasAlcancaveis()
    {
        var detalhadas = NumerosDasEntradasDetalhadas();
        Assert.True(detalhadas.Count > 0, "nenhuma entrada detalhada (## Dxx —) foi encontrada em " + ArquivoDecisoes);

        var texto = LerArquivo();

        // 1. Toda entrada detalhada aparece na tabela, que é o índice que todo mundo lê.
        var naTabela = NumerosDaTabela();
        foreach (var numero in detalhadas)
        {
            Assert.True(naTabela.Contains(numero), $"D{numero} tem seção detalhada mas não aparece na tabela de decisões");
        }

        // 2. Todo link do índice no topo leva a uma seção que existe, e cobre todas as entradas detalhadas.
        var comLink = NumerosDoIndice();
        foreach (var numero in detalhadas)
        {
            Assert.True(comLink.Contains(numero), $"D{numero} não está no índice de decisões detalhadas (topo do arquivo)");
        }

        foreach (var numero in comLink)
        {
            Assert.True(detalhadas.Contains(numero), $"o índice aponta para D{numero}, mas essa seção detalhada não existe");
        }

        // 3. Nenhum link do arquivo aponta para âncora inexistente (o Markdown não valida isso sozinho).
        foreach (var ancora in AncorasCitadas())
        {
            Assert.True(
                AncorasExistentes(texto).Contains(ancora),
                $"link quebrado em {ArquivoDecisoes}: #{ancora} não corresponde a nenhum título");
        }
    }

    private static void SemLinhasEstruturaisNaTabela()
    {
        foreach (var celulas in LinhasDaTabela())
        {
            Assert.True(Numero(celulas[0]) > 0, "a linha " + string.Join(" | ", celulas) + " não começa com D<nº>");
            Assert.False(celulas[1].Trim().Length == 0, $"D{Numero(celulas[0])} está sem texto de decisão");
            Assert.False(celulas[2].Trim().Length == 0, $"D{Numero(celulas[0])} está sem motivo");
            Assert.False(celulas[3].Trim().Length == 0, $"D{Numero(celulas[0])} está sem impacto");
        }
    }

    // ------------------------------------------------------------------ leitura do documento

    /// <summary>
    /// Sobe de <see cref="AppContext.BaseDirectory"/> até achar a raiz do repositório (a pasta que contém
    /// <c>docs/DECISOES.md</c>). Não usa caminho fixo, para o teste valer em qualquer máquina e em qualquer
    /// configuração de saída.
    /// </summary>
    private static string RaizRepositorio()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null)
        {
            if (File.Exists(Path.Combine(pasta.FullName, "docs", "DECISOES.md")))
            {
                return pasta.FullName;
            }

            pasta = pasta.Parent;
        }

        throw new FileNotFoundException(
            "docs/DECISOES.md não encontrado a partir de " + AppContext.BaseDirectory +
            ": o teste precisa rodar dentro do repositório do Flavos Diag");
    }

    private static string LerArquivo() =>
        File.ReadAllText(Path.Combine(RaizRepositorio(), ArquivoDecisoes), Encoding.UTF8);

    private static List<string[]> LinhasDaTabela()
    {
        // Só a seção "Tabela de decisões" conta: o índice e a legenda de status no topo também são
        // tabelas, com outras colunas, e não devem entrar nas verificações da tabela principal.
        var linhas = new List<string[]>();
        var dentroDaTabela = false;
        foreach (var linha in QuebrarLinhas(LerArquivo()))
        {
            var limpa = linha.Trim();

            if (limpa.StartsWith("## ", StringComparison.Ordinal))
            {
                dentroDaTabela = limpa.StartsWith("## Tabela de decisões", StringComparison.Ordinal);
                continue;
            }

            if (!dentroDaTabela || !limpa.StartsWith('|'))
            {
                continue;
            }

            var celulas = Celulas(limpa);
            if (EhSeparador(celulas) || !celulas[0].StartsWith("D", StringComparison.Ordinal))
            {
                continue;
            }

            linhas.Add(celulas);
        }

        return linhas;
    }

    /// <summary>Linha de separação do Markdown (<c>|---|---|</c>), que não é conteúdo.</summary>
    private static bool EhSeparador(string[] celulas) =>
        celulas.Length > 0 && celulas.All(c => c.Length > 0 && c.All(h => h == '-' || h == ':'));

    private static string[] QuebrarLinhas(string texto) => texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static string[] Celulas(string linha)
    {
        var limpa = linha.Trim();
        if (limpa.EndsWith('|'))
        {
            limpa = limpa[..^1];
        }

        if (limpa.StartsWith('|'))
        {
            limpa = limpa[1..];
        }

        return limpa.Split('|').Select(c => c.Trim()).ToArray();
    }

    /// <summary>Número de uma célula iniciada por <c>D<nº></c>; 0 se não houver número.</summary>
    private static int Numero(string celula)
    {
        var texto = celula.Trim().Trim('*', '`', ' ').TrimStart('D');
        var fim = 0;
        while (fim < texto.Length && char.IsAsciiDigit(texto[fim]))
        {
            fim++;
        }

        return fim == 0 ? 0 : int.Parse(texto[..fim]);
    }

    /// <summary>Número citado em "Substituída por Dxx" / "Revista por Dxx"; 0 quando o status é "Vigente".</summary>
    private static int AlvoDoStatus(string status)
    {
        foreach (var prefixo in new[] { "Substituída por ", "Revista por " })
        {
            if (status.StartsWith(prefixo, StringComparison.Ordinal))
            {
                return Numero(status[prefixo.Length..]);
            }
        }

        return 0;
    }

    private static HashSet<int> NumerosDaTabela() =>
        LinhasDaTabela().Select(celulas => Numero(celulas[0])).Where(n => n > 0).ToHashSet();

    /// <summary>Seções <c>## Dxx — …</c> da parte de entradas detalhadas.</summary>
    private static HashSet<int> NumerosDasEntradasDetalhadas() =>
        QuebrarLinhas(LerArquivo())
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("## D", StringComparison.Ordinal))
            .Select(l => Numero(l[3..]))
            .Where(n => n > 0)
            .ToHashSet();

    /// <summary>Decisões citadas em links do índice (linhas <c>| Dxx | … | [Dxx](#…)</c>).</summary>
    private static HashSet<int> NumerosDoIndice()
    {
        var numeros = new HashSet<int>();
        foreach (var linha in QuebrarLinhas(LerArquivo()))
        {
            var limpa = linha.Trim();
            if (!limpa.StartsWith("| D", StringComparison.Ordinal) || !limpa.Contains("](#", StringComparison.Ordinal))
            {
                continue;
            }

            numeros.Add(Numero(Celulas(limpa)[0]));
        }

        return numeros;
    }

    private static IEnumerable<string> AncorasCitadas() =>
        System.Text.RegularExpressions.Regex
            .Matches(LerArquivo(), @"\]\(#([^)]+)\)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);

    /// <summary>Âncoras geradas pelos títulos, no formato que o GitHub usa: minúsculas, sem pontuação, hífens.</summary>
    private static HashSet<string> AncorasExistentes(string texto)
    {
        var ancoras = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linha in QuebrarLinhas(texto))
        {
            var limpa = linha.Trim();
            if (!limpa.StartsWith('#'))
            {
                continue;
            }

            var titulo = limpa.TrimStart('#').Trim();
            ancoras.Add(Ancora(titulo));

            // Variante com os hífens repetidos colapsados: o título "D80 — Os limites…" gera
            // "d80--os-limites" na regra do GitHub, mas há renderizador que descarta o travessão e
            // deixa "d80-os-limites". As duas formas são aceitas para o link válido não reprovar.
            ancoras.Add(System.Text.RegularExpressions.Regex.Replace(Ancora(titulo), "-{2,}", "-"));
        }

        return ancoras;
    }

    private static string Ancora(string titulo)
    {
        // Regra do GitHub: minúsculas, remove pontuação (mantém letras, dígitos, hífen, "_" e espaço)
        // e só então troca cada espaço por hífen. Fazer a troca de espaço antes de remover a
        // pontuação produz âncoras com hífen a mais e reprova links que funcionam.
        var semPontuacao = new StringBuilder();
        foreach (var caractere in titulo.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(caractere) || caractere is '-' or '_' or ' ')
            {
                semPontuacao.Append(caractere);
            }
        }

        return semPontuacao.ToString().Replace(' ', '-');
    }
}
