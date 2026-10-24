using System.Text.Json;
using FlavosDiag.App.Services;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;

namespace FlavosDiag.Tests;

/// <summary>
/// Exportação do diagnóstico de sensores: o arquivo que o suporte recebe quando a temperatura não
/// aparece. Sem ele, a única informação disponível seria "não funcionou".
/// </summary>
/// <remarks>
/// Usa o modo demonstração: nenhuma leitura real de hardware, nenhum driver, nenhuma elevação.
/// </remarks>
public static class DiagnosticoSensoresDumpTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        yield return ("Dump: grava .json e .txt com ambiente, CPU e sensores", GravaJsonETexto);
        yield return ("Dump: sensores sem leitura aparecem com mínimo e máximo", () => Task.Run(SemLeituraTrazMinimoMaximo));
        yield return ("Dump: um sensor sem leitura continua sendo listado, não some", SensorSemLeituraNaoSome);
    }

    private static async Task GravaJsonETexto()
    {
        var pasta = PastaTemporaria();
        try
        {
            var dump = new DiagnosticoSensoresDump(Servicos.Criar(demonstracao: true));
            var (json, texto) = await dump.ExportarAsync(pasta, mascararIdentificadores: true);

            Assert.True(File.Exists(json), "o .json precisa existir");
            Assert.True(File.Exists(texto), "o .txt precisa existir");

            var conteudo = await File.ReadAllTextAsync(json);
            using var doc = JsonDocument.Parse(conteudo);
            var raiz = doc.RootElement;

            // Os quatro blocos que tornam o arquivo útil para diagnóstico remoto.
            Assert.True(raiz.TryGetProperty("ambiente", out var ambiente), "precisa ter o bloco ambiente");
            Assert.True(ambiente.TryGetProperty("processoElevado", out _), "precisa dizer se está elevado");
            Assert.True(ambiente.TryGetProperty("pawnIoTexto", out _), "precisa dizer o estado do PawnIO");

            Assert.True(raiz.TryGetProperty("aplicativo", out var app), "precisa ter o bloco do aplicativo");
            Assert.True(app.TryGetProperty("versaoBiblioteca", out _), "precisa registrar a versão da biblioteca");

            Assert.True(raiz.TryGetProperty("cpu", out _), "precisa ter o bloco da CPU");
            Assert.True(raiz.TryGetProperty("sensores", out _), "precisa listar os sensores");
            Assert.True(raiz.TryGetProperty("sensoresSemLeitura", out _), "precisa listar os sensores sem leitura");
            Assert.True(raiz.TryGetProperty("causa", out _), "precisa registrar a causa");

            var legivel = await File.ReadAllTextAsync(texto);
            Assert.True(legivel.Contains("DIAGNÓSTICO DE SENSORES"), "o texto precisa ter o título");
            Assert.True(legivel.Contains("AMBIENTE"), "o texto precisa ter a seção de ambiente");
            Assert.True(legivel.Contains("CAUSA"), "o texto precisa ter a seção de causa");
        }
        finally
        {
            Limpar(pasta);
        }
    }

    private static void SemLeituraTrazMinimoMaximo()
    {
        {
            // A combinação que prova uma sentinela: valor 0 com mínimo 0 e máximo 0.
            var amostra = new AmostraSensores
            {
                Quando = DateTimeOffset.Now,
                Valores = Array.Empty<ValorSensor>(),
                SemLeitura = new[]
                {
                    new SensorSemLeitura
                    {
                        Id = "cpu/0/temperature/0",
                        Categoria = CategoriaSensor.Temperatura,
                        TipoHardware = TipoHardware.Cpu,
                        NomeHardware = "CPU",
                        NomeSensor = "Core (Tctl/Tdie)",
                        Unidade = "°C",
                        ValorBruto = 0,
                        Minimo = 0,
                        Maximo = 0,
                        Motivo = TemperaturaFisica.MotivoSemLeitura(0, 0, 0),
                    },
                },
            };

            var texto = FlavosDiag.App.Services.DiagnosticoSensoresDump.MontarTexto(Descrever(amostra));
            Assert.True(texto.Contains("Core (Tctl/Tdie)"), "o sensor sem leitura precisa ser nomeado");
            Assert.True(texto.Contains("mín:"), "o texto precisa mostrar o mínimo");
            Assert.True(texto.Contains("máx:"), "o texto precisa mostrar o máximo");
            Assert.True(texto.Contains("sentinela"), "o motivo precisa explicar que 0 é sentinela");
        }
    }

    private static async Task SensorSemLeituraNaoSome()
    {
        var pasta = PastaTemporaria();
        try
        {
            var dump = new DiagnosticoSensoresDump(Servicos.Criar(demonstracao: true));
            var (json, _) = await dump.ExportarAsync(pasta, mascararIdentificadores: true);
            var conteudo = await File.ReadAllTextAsync(json);

            using var doc = JsonDocument.Parse(conteudo);
            var raiz = doc.RootElement;

            // Todo sensor listado precisa de tipo, nome e valor (mesmo nulo), para o suporte poder
            // distinguir "não existe sensor" de "existe sensor que não respondeu".
            foreach (var lista in new[] { "sensores", "sensoresSemLeitura" })
            {
                foreach (var sensor in raiz.GetProperty(lista).EnumerateArray())
                {
                    Assert.True(sensor.TryGetProperty("tipoHardware", out _), lista + ": precisa ter o tipo do hardware");
                    Assert.True(sensor.TryGetProperty("id", out _), lista + ": precisa ter o identificador");
                    Assert.True(sensor.TryGetProperty("qualidade", out _), lista + ": precisa ter a qualidade");
                }
            }
        }
        finally
        {
            Limpar(pasta);
        }
    }

    /// <summary>Monta um <see cref="DumpSensores"/> a partir de uma amostra, sem tocar em hardware.</summary>
    private static DumpSensores Descrever(AmostraSensores amostra) => new()
    {
        GeradoEm = DateTimeOffset.Now,
        Aplicativo = new InfoAplicativo
        {
            Versao = "teste",
            Biblioteca = "teste",
            VersaoBiblioteca = "teste",
            ModoDemonstracao = true,
        },
        Ambiente = new InfoAmbiente
        {
            Windows = "teste",
            Eh64Bits = true,
            ProcessoElevado = false,
            PawnIoInstalado = false,
            PawnIoTexto = "não instalado",
        },
        Causa = new InfoCausa
        {
            Classificacao = nameof(CausaSensor.DriverAusente),
            Texto = DiagnosticoSensores.TextoDeCausa(
                DiagnosticoSensores.Avaliar(false, false, 1, 0)),
        },
        Cpu = new InfoCpuDump
        {
            Nome = "CPU de teste",
            SensoresTermicosReconhecidos = 1,
            SensoresTermicosComLeitura = 0,
        },
        Sensores = Array.Empty<SensorDump>(),
        SensoresSemLeitura = amostra.SemLeitura.Select(s => new SensorDump
        {
            Id = s.Id,
            TipoHardware = s.TipoHardware.ToString(),
            NomeHardware = s.NomeHardware,
            NomeSensor = s.NomeSensor,
            Categoria = s.Categoria.ToString(),
            Valor = s.ValorBruto,
            Minimo = s.Minimo,
            Maximo = s.Maximo,
            Unidade = s.Unidade,
            Qualidade = nameof(QualidadeSensor.SemLeitura),
            Motivo = s.Motivo,
        }).ToList(),
    };

    private static string PastaTemporaria()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "flavos-dump-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    private static void Limpar(string pasta)
    {
        try
        {
            if (Directory.Exists(pasta) && pasta.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(pasta, recursive: true);
            }
        }
        catch (IOException)
        {
            // Pasta temporária que não pôde ser removida não invalida o teste.
        }
    }
}
