using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;

namespace FlavosDiag.Tests;

/// <summary>
/// Runner mínimo de testes usado quando o `dotnet test` não consegue falar com o host de testes
/// (Process.WaitForExit/EnableRaisingEvents pode ser negado no ambiente de execução).
/// Descobre os métodos [Fact]/[Theory] deste assembly, executa e imprime um relatório no formato TRX.
/// Uso: dotnet exec FlavosDiag.Tests.dll --runner --saida resultados.trx
/// </summary>
public static class RunnerStandalone
{
    public static int Main(string[] args)
    {
        if (args.Contains("--roteiro-fase2"))
        {
            var destinoFase2 = args.SkipWhile(a => a != "--pasta").Skip(1).FirstOrDefault()
                               ?? Path.Combine(Path.GetTempPath(), "flavos-roteiro");
            return RoteiroFase2.ExecutarAsync(destinoFase2).GetAwaiter().GetResult();
        }

        if (args.Contains("--roteiro-fase4"))
        {
            var destinoFase4 = args.SkipWhile(a => a != "--pasta").Skip(1).FirstOrDefault()
                               ?? Path.Combine(Path.GetTempPath(), "flavos-roteiro");
            RoteiroFase4.ExecutarAsync(destinoFase4).GetAwaiter().GetResult();
            return 0;
        }

        if (args.Contains("--roteiro-inventario"))
        {
            var destino = args.SkipWhile(a => a != "--pasta").Skip(1).FirstOrDefault()
                          ?? Path.Combine(Path.GetTempPath(), "flavos-roteiro");
            return RoteiroInventario.ExecutarAsync(destino).GetAwaiter().GetResult();
        }

        var saida = args.SkipWhile(a => a != "--saida").Skip(1).FirstOrDefault()
                    ?? Path.Combine(AppContext.BaseDirectory, "resultados.trx");

        var assembly = Assembly.GetExecutingAssembly();
        AssemblyLoadContext.Default.Resolving += (ctx, nome) =>
        {
            var caminho = Path.Combine(AppContext.BaseDirectory, nome.Name + ".dll");
            return File.Exists(caminho) ? ctx.LoadFromAssemblyPath(caminho) : null;
        };

        var total = 0;
        var falhas = 0;
        var ignorados = 0;
        var resultados = new List<(string Nome, string Classe, string Resultado, string? Mensagem, double Segundos)>();
        var relogio = System.Diagnostics.Stopwatch.StartNew();

        // Tabelas de casos: tipos com static IEnumerable<(string, Func<Task>)> Casos().
        foreach (var tipo in assembly.GetTypes().Where(t => t.IsClass && t.IsAbstract && t.IsSealed))
        {
            var fabrica = tipo.GetMethod("Casos", BindingFlags.Public | BindingFlags.Static);
            if (fabrica is null || !fabricaLanca()) { continue; }
            foreach (var item in (IEnumerable)fabrica.Invoke(null, null)!)
            {
                if (item is null) { continue; }
                var campos = item.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                var nome = (string)(campos.FirstOrDefault(f => f.Name == "Item1")?.GetValue(item) ?? tipo.Name);
                var corpo = (Func<Task>)(campos.FirstOrDefault(f => f.Name == "Item2")?.GetValue(item) ?? throw new InvalidOperationException("caso sem corpo"));
                total++;
                var relogioCaso = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    if (!corpo().Wait(TimeSpan.FromMinutes(10)))
                    {
                        throw new TimeoutException("teste excedeu 10 minutos");
                    }

                    relogioCaso.Stop();
                    resultados.Add((nome, tipo.Name, "Passed", null, relogioCaso.Elapsed.TotalSeconds));
                    Console.WriteLine($"  ok   {nome}");
                }
                catch (Exception ex)
                {
                    relogioCaso.Stop();
                    var real = ex is AggregateException { InnerException: { } interno } ? interno : ex;
                    falhas++;
                    resultados.Add((nome, tipo.Name, "Failed", real.ToString(), relogioCaso.Elapsed.TotalSeconds));
                    Console.WriteLine($"  FAIL {nome}");
                    Console.WriteLine("       " + real.Message.Replace("\n", "\n       "));
                }
            }

            continue;

            bool fabricaLanca() => fabrica.GetParameters().Length == 0
                                   && fabrica.ReturnType != typeof(object)
                                   && typeof(IEnumerable).IsAssignableFrom(fabrica.ReturnType);
        }

        foreach (var tipo in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition))
        {
            var metodos = tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute"))
                .OrderBy(m => m.Name)
                .ToList();
            if (metodos.Count == 0)
            {
                continue;
            }

            foreach (var metodo in metodos)
            {
                var casos = Casos(metodo);
                var instancia = metodo.IsStatic ? null : Activator.CreateInstance(tipo);
                foreach (var caso in casos)
                {
                    total++;
                    var nome = metodo.Name;
                    if (caso is not null)
                    {
                        nome += "(" + string.Join(", ", caso.Select(Descrever)) + ")";
                    }

                    var cronometro = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var execucao = metodo.Invoke(instancia, caso);
                        if (execucao is Task tarefa)
                        {
                            if (!tarefa.Wait(TimeSpan.FromMinutes(10)))
                            {
                                throw new TimeoutException("teste excedeu 10 minutos");
                            }
                        }

                        cronometro.Stop();
                        resultados.Add((nome, tipo.Name, "Passed", null, cronometro.Elapsed.TotalSeconds));
                        Console.WriteLine($"  ok   {tipo.Name}.{nome}");
                    }
                    catch (Exception ex)
                    {
                        cronometro.Stop();
                        var real = ex is TargetInvocationException { InnerException: { } interno } ? interno : ex;
                        if (real is SkipExceptionVariavel)
                        {
                            ignorados++;
                            resultados.Add((nome, tipo.Name, "NotExecuted", real.Message, cronometro.Elapsed.TotalSeconds));
                            Console.WriteLine($"  SKIP {tipo.Name}.{nome} — {real.Message}");
                            continue;
                        }

                        falhas++;
                        resultados.Add((nome, tipo.Name, "Failed", real.ToString(), cronometro.Elapsed.TotalSeconds));
                        Console.WriteLine($"  FAIL {tipo.Name}.{nome}");
                        Console.WriteLine("       " + real.Message.Replace("\n", "\n       "));
                    }
                }

                if (instancia is IDisposable descartavel)
                {
                    descartavel.Dispose();
                }
            }
        }

        relogio.Stop();
        GravarTrx(saida, resultados, total, falhas, ignorados, relogio.Elapsed);
        Console.WriteLine();
        Console.WriteLine($"Total: {total} | Aprovados: {total - falhas - ignorados} | Falhas: {falhas} | Ignorados: {ignorados}");
        Console.WriteLine($"Relatório: {saida}");
        return falhas == 0 ? 0 : 1;
    }

    /// <summary>Exceção usada por testes que precisam ser pulados neste ambiente.</summary>
    public sealed class SkipExceptionVariavel : Exception
    {
        public SkipExceptionVariavel(string motivo) : base(motivo)
        {
        }
    }

    private static IEnumerable<object?[]?> Casos(MethodInfo metodo)
    {
        var inline = metodo.GetCustomAttributes().Where(a => a.GetType().Name == "InlineDataAttribute").ToList();
        if (inline.Count == 0)
        {
            yield return metodo.GetParameters().Length == 0 ? null : throw new InvalidOperationException("Teste sem dados: " + metodo.Name);
            yield break;
        }

        foreach (var atributo in inline)
        {
            var dados = atributo.GetType().GetProperty("Data")?.GetValue(atributo)
                        ?? atributo.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                            .FirstOrDefault(f => f.FieldType.IsAssignableTo(typeof(System.Collections.IEnumerable)))?.GetValue(atributo);
            foreach (var item in (System.Collections.IEnumerable)dados!)
            {
                // Cada caso pode vir como vetor de argumentos (int[]) ou lista de vetores (List<object[]>).
                object?[] vetor = item switch
                {
                    object?[] v => v,
                    Array a => a.Cast<object?>().ToArray(),
                    _ => new[] { item },
                };
                yield return Ajustar(vetor, metodo);
            }
        }
    }

    /// <summary>Converte os valores de InlineData para os tipos dos parâmetros (enums chegam como int).</summary>
    private static object?[] Ajustar(object?[] valores, MethodInfo metodo)
    {
        var parametros = metodo.GetParameters();
        var ajustado = new object?[parametros.Length];
        for (var i = 0; i < parametros.Length; i++)
        {
            var valor = i < valores.Length ? valores[i] : null;
            var alvo = Nullable.GetUnderlyingType(parametros[i].ParameterType) ?? parametros[i].ParameterType;
            if (valor is not null && !alvo.IsInstanceOfType(valor) && alvo.IsEnum)
            {
                valor = valor is string s ? Enum.Parse(alvo, s) : Enum.ToObject(alvo, valor);
            }
            else if (valor is not null && !alvo.IsInstanceOfType(valor) && valor is IConvertible)
            {
                valor = Convert.ChangeType(valor, alvo);
            }

            ajustado[i] = valor;
        }

        return ajustado;
    }

    private static string Descrever(object? valor) => valor switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        _ => valor.ToString() ?? string.Empty,
    };

    private static void GravarTrx(
        string caminho,
        List<(string Nome, string Classe, string Resultado, string? Mensagem, double Segundos)> resultados,
        int total,
        int falhas,
        int ignorados,
        TimeSpan duracao)
    {
        var agora = DateTimeOffset.Now;
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var resultadosXml = new XElement(ns + "Results");
        var definicoesXml = new XElement(ns + "TestDefinitions");
        foreach (var r in resultados)
        {
            var id = Guid.NewGuid();
            resultadosXml.Add(new XElement(
                ns + "UnitTestResult",
                new XAttribute("testId", id),
                new XAttribute("testName", r.Nome),
                new XAttribute("duration", TimeSpan.FromSeconds(r.Segundos).ToString()),
                new XAttribute("outcome", r.Resultado),
                new XElement(
                    ns + "Output",
                    r.Mensagem is null
                        ? new XElement(ns + "StdOut", "ok")
                        : new XElement(ns + "ErrorInfo", new XElement(ns + "Message", r.Mensagem)))));
            definicoesXml.Add(new XElement(
                ns + "UnitTest",
                new XAttribute("name", r.Nome),
                new XAttribute("id", id),
                new XElement(ns + "TestMethod", new XAttribute("className", r.Classe), new XAttribute("name", r.Nome))));
        }

        var doc = new XDocument(
            new XElement(
                ns + "TestRun",
                new XAttribute("id", Guid.NewGuid()),
                new XAttribute("name", "FlavosDiag"),
                new XElement(
                    ns + "Times",
                    new XAttribute("start", agora.ToString("O")),
                    new XAttribute("finish", agora.Add(duracao).ToString("O")),
                    new XAttribute("creation", agora.ToString("O"))),
                new XElement(
                    ns + "ResultSummary",
                    new XAttribute("outcome", falhas == 0 ? "Completed" : "Failed"),
                    new XElement(
                        ns + "Counters",
                        new XAttribute("total", total),
                        new XAttribute("executed", total - ignorados),
                        new XAttribute("passed", total - falhas - ignorados),
                        new XAttribute("failed", falhas),
                        new XAttribute("notExecuted", ignorados),
                        new XAttribute("error", 0),
                        new XAttribute("timeout", 0),
                        new XAttribute("aborted", 0),
                        new XAttribute("inconclusive", 0),
                        new XAttribute("passedButRunAborted", 0),
                        new XAttribute("disconnected", 0),
                        new XAttribute("warning", 0))),
                resultadosXml,
                definicoesXml));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho))!);
        doc.Save(caminho);
    }


}
