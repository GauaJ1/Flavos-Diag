using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Gpu;
using FlavosDiag.Providers.Sensores;
using FlavosDiag.Reports;

namespace FlavosDiag.Tests;

/// <summary>
/// Roteiro manual da Fase 4: executa a prova de GPU em hardware real por poucos segundos e imprime
/// o que foi medido. Existe para gerar evidência verificável; não faz parte da suíte automática.
/// </summary>
public static class RoteiroFase4
{
    public static async Task ExecutarAsync(string pastaSaida)
    {
        Directory.CreateDirectory(pastaSaida);
        var linhas = new List<string>();

        void Escrever(string texto = "")
        {
            linhas.Add(texto);
            Console.WriteLine(texto);
        }

        Escrever("=== GPU (Direct3D 11, carga curta de verificação) ===");

        var provider = new GpuD3D11Provider(new SensoresHardwareFactory());
        Escrever("provider: " + provider.Nome);

        var resultado = await provider.TestarAsync(
            new ConfiguracaoEstresse
            {
                Perfil = PerfilCarga.Leve,
                DuracaoMaxima = TimeSpan.FromSeconds(5),
                LimiteTemperaturaGpuCelsius = 85,
            },
            deveParar: () => false,
            progresso: null,
            cancelamento: CancellationToken.None);

        Escrever("adaptador: " + resultado.Adaptador);

        if (resultado.Estresse is null)
        {
            Escrever("SEM TESTE: a prova de GPU não pôde ser executada neste equipamento.");
            Escrever("(o resultado fica Inconclusivo no laudo, com a causa, e nunca vira OK)");
        }
        else
        {
            var e = resultado.Estresse;
            Escrever($"duração real: {e.DuracaoReal.TotalSeconds:0.0} s de {e.DuracaoSolicitada.TotalSeconds:0} s");
            Escrever("frames executados: " + e.CiclosTotais);
            Escrever("motivo da parada: " + e.MotivoParada + " — " + e.MotivoParadaTexto);
            Escrever("divergências de pixel: " + e.ErrosDeCalculo);
            Escrever("temperatura máxima: " + Texto(e.TemperaturaMaximaCelsius));
            Escrever("temperatura média: " + Texto(e.TemperaturaMediaCelsius));
            Escrever("limite configurado: " + e.LimiteTemperaturaCelsius + " °C");
            if (e.AvisoSemSensor is not null)
            {
                Escrever("AVISO: " + e.AvisoSemSensor);
            }
        }

        if (resultado.Benchmark is { } b)
        {
            Escrever();
            Escrever("=== pontuação ===");
            Escrever($"pontos: {b.Pontuacao}");
            Escrever($"fps médio: {b.FpsMedia}");
            Escrever("amostras: " + string.Join(", ", b.Amostras));
            Escrever("aviso: " + b.TextoAviso);
        }

        Escrever();
        Escrever("=== shader usado ===");
        Escrever("carga: compute shader próprio, compilado em tempo de execução (cs_5_0)");
        Escrever("detecção de artefato: hash FNV-1a de 64 bits sobre 256x256 pixels RGBA float");

        var caminho = Path.Combine(pastaSaida, "roteiro-fase4.txt");
        File.WriteAllText(caminho, string.Join(Environment.NewLine, linhas));
        Console.WriteLine();
        Console.WriteLine("Roteiro salvo em " + caminho);
    }

    private static string Texto(Core.Leitura<double> leitura) =>
        leitura.Disponivel ? $"{leitura.Valor:0.0} °C" : "não disponível (" + leitura.Causa + ")";
}
