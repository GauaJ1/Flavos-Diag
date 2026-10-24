using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FlavosDiag.App.ViewModels;

namespace FlavosDiag.App.Views;

/// <summary>
/// Item do menu lateral: ícone desenhado (Path próprio) + rótulo.
/// <para>
/// O item é observável porque o destaque (<see cref="Selecionado"/>) muda durante a sessão.
/// A coleção <c>ItensNav</c> é criada uma única vez: recriá-la a cada navegação faria o
/// ItemsControl reconstruir os botões e perder o foco do teclado no item clicado.
/// </para>
/// </summary>
public sealed partial class ItemNav : ObservableObject
{
    public required PaginaBase Pagina { get; init; }

    public required string Rotulo { get; init; }

    public required Geometry GeometriaIcone { get; init; }

    /// <summary>
    /// Comando sem parâmetro, que já captura a página deste item.
    /// <para>
    /// Antes o menu usava <c>RelayCommand&lt;PaginaBase&gt;</c> e o botão do DataTemplate não tinha
    /// <c>CommandParameter</c>: o comando recebia <c>null</c> e nenhum item navegava. Capturar a
    /// página no próprio comando elimina a dependência de o XAML passar o parâmetro.
    /// </para>
    /// </summary>
    public required ICommand Comando { get; init; }

    [ObservableProperty]
    private bool _selecionado;
}
