using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FlavosDiag.Core.Monitor;

namespace FlavosDiag.App.Views.Controles;

/// <summary>
/// Gráfico de temperatura ao longo do tempo, desenhado à mão (sem biblioteca de gráficos: uma
/// dependência a mais num app que precisa rodar offline não se paga por uma linha).
/// </summary>
/// <remarks>
/// <para>
/// Escolhas que fazem diferença na leitura:
/// </para>
/// <list type="bullet">
/// <item>O eixo Y começa em <see cref="TemperaturaMinima"/> e vai até um teto que cresce conforme os
/// dados — nunca em 0, que achataria a curva e esconderia a variação.</item>
/// <item>Uma amostra sem leitura <b>quebra a linha</b> em vez de ser desenhada em 0: ligar os pontos
/// por cima de um vácuo inventaria uma temperatura que ninguém mediu.</item>
/// <item>A faixa de alerta é desenhada atrás dos dados, para o técnico ver a curva se aproximando do
/// limite antes de o limite ser atingido.</item>
/// </list>
/// </remarks>
public sealed class GraficoTemperatura : FrameworkElement
{
    public static readonly DependencyProperty AmostrasProperty = DependencyProperty.Register(
        nameof(Amostras),
        typeof(IReadOnlyList<AmostraTemporal>),
        typeof(GraficoTemperatura),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LimiarAlertaProperty = DependencyProperty.Register(
        nameof(LimiarAlerta),
        typeof(double),
        typeof(GraficoTemperatura),
        new FrameworkPropertyMetadata(95.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CorCpuProperty = DependencyProperty.Register(
        nameof(CorCpu),
        typeof(Brush),
        typeof(GraficoTemperatura),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CorGpuProperty = DependencyProperty.Register(
        nameof(CorGpu),
        typeof(Brush),
        typeof(GraficoTemperatura),
        new FrameworkPropertyMetadata(Brushes.MediumPurple, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CorGradeProperty = DependencyProperty.Register(
        nameof(CorGrade),
        typeof(Brush),
        typeof(GraficoTemperatura),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CorTextoProperty = DependencyProperty.Register(
        nameof(CorTexto),
        typeof(Brush),
        typeof(GraficoTemperatura),
        new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CorAlertaProperty = DependencyProperty.Register(
        nameof(CorAlerta),
        typeof(Brush),
        typeof(GraficoTemperatura),
        new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Janela de tempo mostrada, em segundos.</summary>
    public static readonly DependencyProperty SegundosVisiveisProperty = DependencyProperty.Register(
        nameof(SegundosVisiveis),
        typeof(double),
        typeof(GraficoTemperatura),
        new FrameworkPropertyMetadata(60.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public const double TemperaturaMinima = 20;

    public const double TemperaturaMaxima = 110;

    public IReadOnlyList<AmostraTemporal>? Amostras
    {
        get => (IReadOnlyList<AmostraTemporal>?)GetValue(AmostrasProperty);
        set => SetValue(AmostrasProperty, value);
    }

    public double LimiarAlerta
    {
        get => (double)GetValue(LimiarAlertaProperty);
        set => SetValue(LimiarAlertaProperty, value);
    }

    public Brush CorCpu
    {
        get => (Brush)GetValue(CorCpuProperty);
        set => SetValue(CorCpuProperty, value);
    }

    public Brush CorGpu
    {
        get => (Brush)GetValue(CorGpuProperty);
        set => SetValue(CorGpuProperty, value);
    }

    public Brush CorGrade
    {
        get => (Brush)GetValue(CorGradeProperty);
        set => SetValue(CorGradeProperty, value);
    }

    public Brush CorTexto
    {
        get => (Brush)GetValue(CorTextoProperty);
        set => SetValue(CorTextoProperty, value);
    }

    public Brush CorAlerta
    {
        get => (Brush)GetValue(CorAlertaProperty);
        set => SetValue(CorAlertaProperty, value);
    }

    public double SegundosVisiveis
    {
        get => (double)GetValue(SegundosVisiveisProperty);
        set => SetValue(SegundosVisiveisProperty, value);
    }

    /// <summary>Margem interna para os rótulos do eixo.</summary>
    private const double MargemEsquerda = 42;

    private const double MargemInferior = 18;

    private const double MargemSuperior = 8;

    private const double MargemDireita = 10;

    protected override void OnRender(DrawingContext dc)
    {
        var larg = ActualWidth;
        var alt = ActualHeight;
        if (larg <= MargemEsquerda + MargemDireita || alt <= MargemSuperior + MargemInferior)
        {
            return;
        }

        var area = new Rect(
            MargemEsquerda,
            MargemSuperior,
            larg - MargemEsquerda - MargemDireita,
            alt - MargemSuperior - MargemInferior);

        var canetaGrade = new Pen(CorGrade, 1) { Brush = CorGrade };
        canetaGrade.Freeze();

        DesenharFaixaDeAlerta(dc, area);
        DesenharGrade(dc, area, canetaGrade);

        var amostras = Amostras;
        if (amostras is null || amostras.Count == 0)
        {
            DesenharTexto(dc, "Aguardando leitura…", new Point(area.X + 10, area.Y + 10), 12);
            return;
        }

        var janela = Math.Max(1, SegundosVisiveis);
        var fim = amostras[^1].Segundos;
        var inicio = Math.Max(0, fim - janela);
        var span = Math.Max(0.001, fim - inicio);

        // Se ainda não há leitura válida nenhuma, dizer isso é mais honesto do que uma linha reta.
        if (!amostras.Any(a => a.TemAlgumaTemperatura))
        {
            var causa = amostras[^1].CausaCpu ?? amostras[^1].CausaGpu ?? "sensores de temperatura sem leitura";
            DesenharTexto(dc, "Sem leitura de temperatura", new Point(area.X + 10, area.Y + 10), 12);
            DesenharTexto(dc, causa, new Point(area.X + 10, area.Y + 28), 11);
            return;
        }

        DesenharSerie(dc, area, amostras, inicio, span, a => a.TemperaturaCpuCelsius, CorCpu);
        DesenharSerie(dc, area, amostras, inicio, span, a => a.TemperaturaGpuCelsius, CorGpu);
    }

    private void DesenharFaixaDeAlerta(DrawingContext dc, Rect area)
    {
        if (LimiarAlerta <= TemperaturaMinima || LimiarAlerta >= TemperaturaMaxima)
        {
            return;
        }

        var y = YDe(LimiarAlerta, area);
        var faixa = new Rect(area.X, area.Y, area.Width, Math.Max(0, y - area.Y));
        var fundo = CorAlerta.Clone();
        fundo.Opacity = 0.14;
        fundo.Freeze();
        dc.DrawRectangle(fundo, null, faixa);

        var caneta = new Pen(CorAlerta, 1) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        caneta.Freeze();
        dc.DrawLine(caneta, new Point(area.X, y), new Point(area.Right, y));
    }

    private void DesenharGrade(DrawingContext dc, Rect area, Pen caneta)
    {
        // Uma linha a cada 20 °C é o suficiente para ler o valor sem poluir.
        for (var t = TemperaturaMinima; t <= TemperaturaMaxima; t += 20)
        {
            var y = YDe(t, area);
            dc.DrawLine(caneta, new Point(area.X, y), new Point(area.Right, y));
            DesenharTexto(dc, t.ToString("0", CultureInfo.GetCultureInfo("pt-BR")) + "°", new Point(6, y - 7), 10);
        }

        dc.DrawLine(caneta, new Point(area.X, area.Bottom), new Point(area.Right, area.Bottom));
    }

    private void DesenharSerie(
        DrawingContext dc,
        Rect area,
        IReadOnlyList<AmostraTemporal> amostras,
        double inicio,
        double span,
        Func<AmostraTemporal, double?> seletor,
        Brush cor)
    {
        var caneta = new Pen(cor, 2) { LineJoin = PenLineJoin.Round };
        caneta.Freeze();

        var geometria = new StreamGeometry();
        using (var ctx = geometria.Open())
        {
            var desenhando = false;
            foreach (var a in amostras)
            {
                var valor = seletor(a);
                if (valor is null || !ClassificadorTemperatura.Valida(valor))
                {
                    // Sem leitura: interrompe a linha em vez de ligar os pontos por cima do vácuo.
                    desenhando = false;
                    continue;
                }

                var x = area.X + (a.Segundos - inicio) / span * area.Width;
                var y = YDe(valor.Value, area);
                var ponto = new Point(Math.Clamp(x, area.X, area.Right), Math.Clamp(y, area.Y, area.Bottom));

                if (!desenhando)
                {
                    ctx.BeginFigure(ponto, isFilled: false, isClosed: false);
                    desenhando = true;
                }
                else
                {
                    ctx.LineTo(ponto, isStroked: true, isSmoothJoin: true);
                }
            }
        }

        geometria.Freeze();
        dc.DrawGeometry(null, caneta, geometria);
    }

    private void DesenharTexto(DrawingContext dc, string texto, Point posicao, double tamanho)
    {
        var ft = new FormattedText(
            texto,
            CultureInfo.GetCultureInfo("pt-BR"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            tamanho,
            CorTexto,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, posicao);
    }

    private static double YDe(double temperatura, Rect area)
    {
        var fracao = (temperatura - TemperaturaMinima) / (TemperaturaMaxima - TemperaturaMinima);
        fracao = Math.Clamp(fracao, 0, 1);
        return area.Bottom - fracao * area.Height;
    }
}
