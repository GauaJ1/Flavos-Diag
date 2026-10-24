using CommunityToolkit.Mvvm.ComponentModel;

namespace FlavosDiag.App.ViewModels;

/// <summary>Base de todas as páginas: título, ícone e carregamento inicial.</summary>
public abstract partial class PaginaBase : ObservableObject
{
    protected PaginaBase(MainViewModel principal, string titulo, string icone)
    {
        Principal = principal;
        Titulo = titulo;
        Icone = icone;
    }

    public MainViewModel Principal { get; }

    public string Titulo { get; }

    /// <summary>Chave do Geometry do ícone (Converters/Icone.xaml).</summary>
    public string Icone { get; }

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private string? _erro;

    [ObservableProperty]
    private string? _mensagemVazia;

    [ObservableProperty]
    private bool _carregado;

    /// <summary>Chamado ao abrir a página (uma vez, a menos que <paramref name="forcar"/>).</summary>
    public void AoAtivar(bool forcar = false)
    {
        if (Carregado && !forcar)
        {
            return;
        }

        Carregado = true;
        _ = CarregarAsync();
    }

    protected abstract Task CarregarAsync();

    /// <summary>Executa uma operação com estado de carregamento e tratamento de erro visível.</summary>
    protected async Task ExecutarAsync(Func<Task> acao, string mensagemErro = "Não foi possível concluir a operação.")
    {
        Carregando = true;
        Erro = null;
        try
        {
            await acao();
        }
        catch (OperationCanceledException)
        {
            Erro = "Operação cancelada.";
        }
        catch (Exception ex)
        {
            Erro = mensagemErro + " Detalhe: " + ex.Message;
        }
        finally
        {
            Carregando = false;
        }
    }
}

/// <summary>Linha rótulo/valor com estado (usada nas telas de inventário).</summary>
public sealed record LinhaCampo(string Rotulo, string Valor, string? Causa = null)
{
    public bool Ausente => !string.IsNullOrEmpty(Causa);
}

public static class LinhaFactory
{
    public static LinhaCampo De<T>(string rotulo, FlavosDiag.Core.Leitura<T> leitura, Func<T, string>? formatador = null)
    {
        if (leitura.Disponivel && leitura.Valor is not null)
        {
            return new LinhaCampo(rotulo, formatador is null ? leitura.Valor.ToString()! : formatador(leitura.Valor));
        }

        return new LinhaCampo(rotulo, FlavosDiag.Core.FormatacaoBr.NaoDisponivel(leitura.Causa), leitura.Causa ?? "Não disponível");
    }
}
