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

        // --dump-sensores: grava o diagnóstico e encerra, sem abrir a janela. Serve para o dono
        // capturar a situação antes e depois de instalar o driver, e para suporte por arquivo.
        if (e.Args.Any(a => a.Equals("--dump-sensores", StringComparison.OrdinalIgnoreCase)))
        {
            // Sem janela, o modo padrão (OnLastWindowClose) encerraria o aplicativo assim que este
            // método retornasse, matando a coleta antes de gravar o arquivo. O encerramento passa a
            // ser explícito, feito por ExecutarDumpAsync quando terminar.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = ExecutarDumpAsync(configuracao);
            return;
        }

        AplicarTema(configuracao.Tema != "Claro");

        var vm = new MainViewModel(Servicos, configuracao, demonstracao);
        var janela = new MainWindow { DataContext = vm };
        MainWindow = janela;
        vm.AoIniciar(janela);
        janela.Show();
    }

    private static async Task ExecutarDumpAsync(Configuracao configuracao)
    {
        var pasta = string.IsNullOrWhiteSpace(configuracao.PastaExportacao)
            ? ConfiguracaoStore.PastaDados
            : configuracao.PastaExportacao;

        try
        {
            var dump = new DiagnosticoSensoresDump(Servicos);
            var (json, texto) = await dump.ExportarAsync(pasta, configuracao.MascararSeriais);

            // Argumento de linha de comando: os caminhos vão para a saída padrão, sem caixa de
            // diálogo. Um modal exigiria alguém clicando, o que impede uso em script e em teste
            // automatizado — e este comando existe justamente para ser repetível.
            Console.Out.WriteLine("Diagnóstico de sensores gravado:");
            Console.Out.WriteLine("  " + json);
            Console.Out.WriteLine("  " + texto);
            Console.Out.Flush();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine("Não foi possível gravar o diagnóstico de sensores: " + ex.Message);
            Console.Error.Flush();
        }
        finally
        {
            Current.Shutdown();
        }
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
