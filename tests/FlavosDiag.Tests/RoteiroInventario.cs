using FlavosDiag.Core.Diagnostics;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Providers.Abstractions;
using FlavosDiag.Providers.Wmi;
using FlavosDiag.Reports;
using FlavosDiag.Reports.Json;

namespace FlavosDiag.Tests;

/// <summary>
/// Verificação manual/assistida do inventário real: lê o computador desta máquina pelo provider real.
/// Não roda na suíte padrão (é um roteiro manual): execute com
/// <c>dotnet exec FlavosDiag.Tests.dll --roteiro</c>.
/// </summary>
public static class RoteiroInventario
{
    public static async Task<int> ExecutarAsync(string pastaSaida)
    {
        Directory.CreateDirectory(pastaSaida);
        var provider = new InventarioWmiProvider(new WmiFonteAuditada(new WmiFonte()), new AmbienteSistema());
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Console.WriteLine("Coletando inventário real (WMI/CIM + DXGI)...");
        var inventario = await provider.ColetarAsync(cts.Token);
        var problemas = 0;

        void Mostrar(string rotulo, FlavosDiag.Core.Leitura<string> leitura)
        {
            if (leitura.Disponivel)
            {
                Console.WriteLine($"  {rotulo}: {leitura.Valor}");
            }
            else
            {
                Console.WriteLine($"  {rotulo}: AUSENTE ({leitura.Estado}) — {leitura.Causa}");
                problemas++;
            }
        }

        Console.WriteLine();
        Mostrar("Sistema", inventario.Sistema.Nome);
        Mostrar("Build", inventario.Sistema.Build);
        Mostrar("Fabricante da placa", inventario.PlacaMae.Fabricante);
        Mostrar("Modelo da placa", inventario.PlacaMae.Modelo);
        Mostrar("BIOS", inventario.Bios.Versao);
        Mostrar("Processador", inventario.Cpu.Nome);
        Mostrar("Núcleos", inventario.Cpu.NucleosFisicos.Map(v => v.ToString()));
        Mostrar("Threads", inventario.Cpu.ThreadsLogicas.Map(v => v.ToString()));
        Mostrar("Memória total", inventario.Memoria.TotalBytes.Map(v => FlavosDiag.Core.FormatacaoBr.Bytes(v)));

        Console.WriteLine($"  Pentes de memória: {inventario.Memoria.Modulos.Count}");
        foreach (var pente in inventario.Memoria.Modulos)
        {
            Console.WriteLine($"    - {pente.Fabricante.Valor ?? "?"} {pente.CapacidadeBytes.Valor / 1024.0 / 1024 / 1024:0} GiB @ {pente.VelocidadeConfiguradaMhz.Valor} MT/s ({pente.Local.Valor})");
        }

        Console.WriteLine($"  GPU(s): {inventario.Gpus.Count}");
        foreach (var gpu in inventario.Gpus)
        {
            Console.WriteLine($"    - {gpu.Nome.Valor} | VRAM: {FlavosDiag.Core.FormatacaoBr.Bytes(gpu.VramDedicadaBytes.Valor)} | driver {gpu.VersaoDriver.Valor}");
        }

        Console.WriteLine($"  Limitações registradas: {inventario.Limitacoes.Count}");
        foreach (var l in inventario.Limitacoes)
        {
            Console.WriteLine($"    - {l.Item}: {l.Motivo}");
        }

        // Grava um laudo real mínimo para conferir a cadeia de exportação com dados da máquina.
        var categorias = new[]
        {
            new FlavosDiag.Core.Modelos.ResultadoCategoria
            {
                Categoria = FlavosDiag.Core.Vereditos.Regras.CategoriaSistema,
                Veredito = FlavosDiag.Core.Vereditos.Regras.Sistema(inventario).Veredito,
                Resumo = FlavosDiag.Core.Vereditos.Regras.Sistema(inventario).Resumo,
            },
        };

        var diagnostico = new ResultadoDiagnosticoRapido
        {
            IniciadoEm = DateTimeOffset.Now,
            Duracao = TimeSpan.Zero,
            Inventario = inventario,
            Discos = Array.Empty<FlavosDiag.Core.Modelos.DiscoInfo>(),
            Bateria = null,
            UltimaAmostra = null,
            AmostrasSensores = Array.Empty<FlavosDiag.Core.Modelos.AmostraSensores>(),
            Rede = null,
            Etapas = Array.Empty<EtapaDiagnostico>(),
            Categorias = categorias,
            Agregacao = FlavosDiag.Core.Vereditos.VereditoConsolidador.Agregar(categorias),
            Limitacoes = inventario.Limitacoes,
            Cancelado = false,
        };

        var laudo = LaudoBuilder.A_partirDiagnostico(
            diagnostico,
            new FlavosDiag.Core.Modelos.DadosTecnico { Nome = "Roteiro automático", Empresa = "Verificação local" },
            "ROTEIRO-" + DateTime.Now.ToString("yyyyMMdd-HHmm"),
            "0.5.0",
            mascararSeriais: true,
            modoDemonstracao: false,
            checklist: null,
            limiteTemperaturaCelsius: 90);

        var json = Path.Combine(pastaSaida, "laudo-inventario.json");
        var pdf = Path.Combine(pastaSaida, "laudo-inventario.pdf");
        await LaudoJson.GravarAsync(laudo, json);
        new FlavosDiag.Reports.Pdf.GeradorLaudoPdf().GerarArquivo(laudo, pdf);

        Console.WriteLine();
        Console.WriteLine($"JSON: {json} ({new FileInfo(json).Length} bytes)");
        Console.WriteLine($"PDF : {pdf} ({new FileInfo(pdf).Length} bytes)");
        Console.WriteLine($"Veredito geral: {laudo.VereditoGeral.Rotulo()}");
        Console.WriteLine(problemas == 0
            ? "Roteiro: todos os campos essenciais foram lidos."
            : $"Roteiro: {problemas} campo(s) sem leitura (aparecem como Inconclusivo com causa).");
        return 0;
    }

}
