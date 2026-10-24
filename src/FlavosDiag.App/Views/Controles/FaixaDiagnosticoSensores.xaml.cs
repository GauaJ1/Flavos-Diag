using System.Windows;
using System.Windows.Controls;

namespace FlavosDiag.App.Views.Controles;

/// <summary>
/// Faixa de aviso quando a temperatura não tem leitura: mostra a causa, o comando sugerido (com
/// botão Copiar) e o botão "Verificar novamente".
/// </summary>
/// <remarks>
/// O comando é apenas copiado — o aplicativo nunca instala driver. Copiar em vez de executar é
/// deliberado: quem instala um driver de kernel decide isso por si, conferindo o site oficial, e
/// não por um clique numa tela de diagnóstico.
/// </remarks>
public partial class FaixaDiagnosticoSensores : UserControl
{
    public static readonly DependencyProperty TextoProperty = DependencyProperty.Register(
        nameof(Texto),
        typeof(string),
        typeof(FaixaDiagnosticoSensores),
        new PropertyMetadata(null, AoMudarTexto));

    public static readonly DependencyProperty ComandoProperty = DependencyProperty.Register(
        nameof(Comando),
        typeof(string),
        typeof(FaixaDiagnosticoSensores),
        new PropertyMetadata(null, AoMudarComando));

    public static readonly DependencyProperty ReverificarCommandProperty = DependencyProperty.Register(
        nameof(ReverificarCommand),
        typeof(System.Windows.Input.ICommand),
        typeof(FaixaDiagnosticoSensores),
        new PropertyMetadata(null));

    public static readonly DependencyProperty MostrarComandoProperty = DependencyProperty.Register(
        nameof(MostrarComando),
        typeof(bool),
        typeof(FaixaDiagnosticoSensores),
        new PropertyMetadata(false, AoMudarComando));

    public FaixaDiagnosticoSensores()
    {
        InitializeComponent();
        Atualizar();
    }

    /// <summary>Texto da causa, já pronto em pt-BR.</summary>
    public string? Texto
    {
        get => (string?)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    /// <summary>Comando sugerido de instalação do driver de terceiros.</summary>
    public string? Comando
    {
        get => (string?)GetValue(ComandoProperty);
        set => SetValue(ComandoProperty, value);
    }

    /// <summary>Comando que fecha e reabre a sessão de sensores.</summary>
    public System.Windows.Input.ICommand? ReverificarCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(ReverificarCommandProperty);
        set => SetValue(ReverificarCommandProperty, value);
    }

    /// <summary>Mostrar o bloco do comando (só quando instalar o driver é uma causa possível).</summary>
    public bool MostrarComando
    {
        get => (bool)GetValue(MostrarComandoProperty);
        set => SetValue(MostrarComandoProperty, value);
    }

    private static void AoMudarTexto(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((FaixaDiagnosticoSensores)d).Atualizar();

    private static void AoMudarComando(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((FaixaDiagnosticoSensores)d).Atualizar();

    private void Atualizar()
    {
        var temTexto = !string.IsNullOrWhiteSpace(Texto);
        Root.Visibility = temTexto ? Visibility.Visible : Visibility.Collapsed;
        BlocoComando.Visibility = MostrarComando && !string.IsNullOrWhiteSpace(Comando)
            ? Visibility.Visible
            : Visibility.Collapsed;
        CampoComando.Text = Comando ?? string.Empty;
    }

    private void CopiarComando(object sender, RoutedEventArgs e)
    {
        var comando = Comando;
        if (string.IsNullOrWhiteSpace(comando))
        {
            return;
        }

        try
        {
            Clipboard.SetText(comando);
            StatusCopia.Text = "Comando copiado. Confira o site oficial antes de instalar.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A área de transferência pode estar bloqueada por outro processo; o texto continua na
            // tela e pode ser selecionado à mão.
            StatusCopia.Text = "Não foi possível copiar automaticamente. Selecione o texto acima.";
        }
    }
}
