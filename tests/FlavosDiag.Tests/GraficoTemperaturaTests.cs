using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlavosDiag.App.Views.Controles;
using FlavosDiag.Core.Monitor;

namespace FlavosDiag.Tests;

/// <summary>
/// Renderização do gráfico de temperatura. O teste desenha o controle num bitmap e conta pixels:
/// é a única forma de provar que a curva realmente aparece. Um gráfico que não desenha nada
/// passaria por qualquer teste que só conferisse propriedades.
/// </summary>
public static class GraficoTemperaturaTests
{
    private const int Largura = 600;

    private const int Altura = 260;

    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        yield return ("Gráfico: série de temperatura desenha a curva da CPU", DesenhaSerieCpu);
        yield return ("Gráfico: sem leitura não desenha curva nenhuma", SemLeituraNaoDesenha);
        yield return ("Gráfico: buraco no meio da série interrompe a linha", BuracoQuebraLinha);
        yield return ("Gráfico: limiar dentro da escala pinta a faixa de alerta", FaixaDeAlerta);
    }

    private static Task DesenhaSerieCpu()
    {
        var pixels = Renderizar(new[]
        {
            Amostra(0, 40),
            Amostra(10, 55),
            Amostra(20, 70),
            Amostra(30, 62),
        }, limiar: 95);

        var tinta = ContarCor(pixels, Colors.DeepSkyBlue);
        Assert.True(tinta > 50, $"a linha da CPU deveria pintar pixels na própria cor; pintou {tinta}");
        return Task.CompletedTask;
    }

    private static Task SemLeituraNaoDesenha()
    {
        var pixels = Renderizar(new[] { Amostra(0, null), Amostra(10, null) }, limiar: 95);

        var tinta = ContarCor(pixels, Colors.DeepSkyBlue) + ContarCor(pixels, Colors.MediumPurple);
        Assert.True(tinta == 0, $"sem leitura não pode haver curva; pintou {tinta} pixels");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Duas amostras válidas separadas por um vazio não podem virar uma reta atravessando o buraco.
    /// Comparar com uma linha contínua no mesmo intervalo torna a asserção objetiva.
    /// </summary>
    private static Task BuracoQuebraLinha()
    {
        var comBuraco = ContarCor(
            Renderizar(new[] { Amostra(0, 40), Amostra(10, null), Amostra(20, 40) }, limiar: 95),
            Colors.DeepSkyBlue);

        var continuo = ContarCor(
            Renderizar(new[] { Amostra(0, 40), Amostra(10, 40), Amostra(20, 40) }, limiar: 95),
            Colors.DeepSkyBlue);

        Assert.True(
            comBuraco < continuo,
            $"a linha interrompida precisa ter menos tinta que a contínua ({comBuraco} vs {continuo})");
        return Task.CompletedTask;
    }

    private static Task FaixaDeAlerta()
    {
        var amostras = new[] { Amostra(0, 50) };

        // A faixa é translúcida (14% da cor de alerta sobre o fundo). Contar a cor pura não a
        // encontraria; o que se mede aqui é o quanto a região acima do limiar ficou avermelhada.
        var comFaixa = ContarVermelhoVisivel(Renderizar(amostras, limiar: 80));

        // Limiar fora da escala visível não pode pintar faixa nenhuma.
        var semFaixa = ContarVermelhoVisivel(Renderizar(amostras, limiar: 0));

        Assert.True(
            comFaixa > semFaixa + 100,
            $"limiar na escala precisa tingir a área acima dele ({comFaixa} vs {semFaixa})");
        return Task.CompletedTask;
    }

    /// <summary>Pixels com dominância de vermelho — a marca da faixa de alerta translúcida.</summary>
    private static int ContarVermelhoVisivel(byte[] pixels)
    {
        var total = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            if (r > g + 8 && r > b + 8)
            {
                total++;
            }
        }

        return total;
    }

    private static AmostraTemporal Amostra(double segundos, double? cpu) => new()
    {
        Quando = DateTimeOffset.Now,
        Segundos = segundos,
        TemperaturaCpuCelsius = cpu,
    };

    /// <summary>
    /// Desenha o gráfico numa thread STA e devolve os pixels RGBA. WPF exige STA para renderizar.
    /// </summary>
    private static byte[] Renderizar(IReadOnlyList<AmostraTemporal> amostras, double limiar)
    {
        byte[]? pixels = null;
        Exception? falha = null;

        var thread = new Thread(() =>
        {
            try
            {
                var grafico = new GraficoTemperatura
                {
                    Width = Largura,
                    Height = Altura,
                    Amostras = amostras,
                    LimiarAlerta = limiar,
                    CorCpu = Brushes.DeepSkyBlue,
                    CorGpu = Brushes.MediumPurple,
                    CorGrade = Brushes.DimGray,
                    CorTexto = Brushes.Gainsboro,
                    CorAlerta = Brushes.IndianRed,
                    SegundosVisiveis = 60,
                };

                grafico.Measure(new Size(Largura, Altura));
                grafico.Arrange(new Rect(0, 0, Largura, Altura));
                grafico.UpdateLayout();

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, Largura, Altura));
                    dc.DrawRectangle(new VisualBrush(grafico), null, new Rect(0, 0, Largura, Altura));
                }

                var bitmap = new RenderTargetBitmap(Largura, Altura, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);

                var buffer = new byte[Largura * Altura * 4];
                bitmap.CopyPixels(buffer, Largura * 4, 0);
                pixels = buffer;
            }
            catch (Exception ex)
            {
                falha = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "a renderização do gráfico demorou demais");
        Assert.True(falha is null, "a renderização do gráfico falhou: " + falha);
        Assert.True(pixels is not null, "não foi possível capturar os pixels do gráfico");
        return pixels!;
    }

    /// <summary>Conta pixels próximos da cor pedida, com tolerância para suavização e transparência.</summary>
    private static int ContarCor(byte[] pixels, Color cor)
    {
        var total = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            if (Math.Abs(r - cor.R) <= 40 && Math.Abs(g - cor.G) <= 40 && Math.Abs(b - cor.B) <= 40)
            {
                total++;
            }
        }

        return total;
    }
}
