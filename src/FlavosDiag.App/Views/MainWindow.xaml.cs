using System.Windows;
using FlavosDiag.App.ViewModels;

namespace FlavosDiag.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Ligar();
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
