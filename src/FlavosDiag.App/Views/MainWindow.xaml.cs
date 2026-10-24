using System.Windows;
using FlavosDiag.App.ViewModels;

namespace FlavosDiag.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LimitarAoEspacoDeTrabalho();
        DataContextChanged += (_, _) => Ligar();
    }

    /// <summary>
    /// Reduz a janela inicial ao espaço útil da tela.
    /// <para>
    /// O tamanho pedido no XAML (1240x820) não cabe em telas de 1366x768 — e cabe menos ainda com
    /// escala de 150%, porque a área lógica encolhe na mesma proporção. Sem este limite, a janela
    /// abre maior que a tela e os botões do rodapé (tema e Configurações) ficam fora de alcance.
    /// </para>
    /// <para>
    /// <see cref="SystemParameters.WorkArea"/> já vem em unidades independentes de dispositivo, ou
    /// seja, já considera a escala do Windows. Não é preciso converter DPI aqui.
    /// </para>
    /// </summary>
    private void LimitarAoEspacoDeTrabalho()
    {
        var area = SystemParameters.WorkArea;

        // Área nula acontece em sessão sem superfície gráfica (teste automatizado, serviço).
        var larguraTela = area.Width > 0 ? area.Width : 1240;
        var alturaTela = area.Height > 0 ? area.Height : 820;

        // O mínimo também precisa caber; senão o Windows abre a janela cortada de qualquer forma.
        MinWidth = Math.Min(MinWidth, larguraTela);
        MinHeight = Math.Min(MinHeight, alturaTela);

        Width = Math.Min(Width, larguraTela);
        Height = Math.Min(Height, alturaTela);

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = area.Left + Math.Max(0, (larguraTela - Width) / 2);
        Top = area.Top + Math.Max(0, (alturaTela - Height) / 2);
    }

    private void Ligar()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.SolicitarConfirmacao = Confirmar;
        vm.SolicitarPasta = EscolherPasta;
        vm.SolicitarArquivo = EscolherArquivo;
    }

    /// <summary>Confirmação modal (usada por testes de esforço, reinício e exclusão de dados).</summary>
    public bool Confirmar(string titulo, string texto)
    {
        var resultado = MessageBox.Show(this, texto, titulo, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return resultado == MessageBoxResult.Yes;
    }

    public (bool Ok, string? Pasta) EscolherPasta()
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog { Title = "Escolher pasta" };
        return dialogo.ShowDialog(this) == true ? (true, dialogo.FolderName) : (false, null);
    }

    public (bool Ok, string? Arquivo) EscolherArquivo(string filtro, string titulo)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog { Title = titulo, Filter = filtro };
        return dialogo.ShowDialog(this) == true ? (true, dialogo.FileName) : (false, null);
    }
}
