using System.Windows;
using System.Windows.Media;
using FlavosDiag.App.Services;
using FlavosDiag.App.ViewModels;
using FlavosDiag.App.Views;

namespace FlavosDiag.App;

public partial class App : Application
{
    public static Servicos Servicos { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var demonstracao = e.Args.Any(a => a.Equals("--modo-demo", StringComparison.OrdinalIgnoreCase));
        RegistrarFonte();

        var configuracao = ConfiguracaoStore.Carregar();
        Servicos = Servicos.Criar(demonstracao);

        AplicarTema(configuracao.Tema != "Claro");

        var vm = new MainViewModel(Servicos, configuracao, demonstracao);
        var janela = new MainWindow { DataContext = vm };
        MainWindow = janela;
        vm.AoIniciar(janela);
        janela.Show();
    }

    /// <summary>Alterna claro/escuro. No escuro, aplica a sobreposição de cores do tema Flavos.</summary>
    public static void AplicarTema(bool escuro)
    {
        var dicio = Current.Resources.MergedDictionaries;
        const string UriEscuro = "Theme/TemaEscuro.xaml";
        for (var i = dicio.Count - 1; i >= 0; i--)
        {
            var origem = dicio[i].Source?.OriginalString ?? string.Empty;
            if (origem.Contains(UriEscuro, StringComparison.OrdinalIgnoreCase))
            {
                dicio.RemoveAt(i);
            }
        }

        try
        {
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                escuro ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light,
                updateAccent: false);
        }
        catch (Exception)
        {
            // Biblioteca de tema havia falhado: a interface segue com os estilos próprios.
        }

        if (escuro)
        {
            // URI com o nome do assembly: a forma relativa ("Theme/TemaEscuro.xaml") só resolve
            // quando quem carrega é o próprio assembly do aplicativo. Com o nome explícito, a troca
            // de tema funciona também a partir de código de teste.
            dicio.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/FlavosDiag.App;component/Theme/TemaEscuro.xaml", UriKind.Absolute),
            });
        }
    }

    private static void RegistrarFonte()
    {
        try
        {
            // Registra a família de fontes empacotada (OFL 1.1) a partir do próprio assembly.
            var familia = new FontFamily(new Uri("pack://application:,,,/FlavosDiag.App;component/Assets/Fonts/"), "./#Geist");
            _ = familia.GetTypefaces().ToList();
        }
        catch (Exception)
        {
            // Sem a fonte empacotada, o WPF usa a fonte do sistema; a interface continua funcional.
        }
    }
}
