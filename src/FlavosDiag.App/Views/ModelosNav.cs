using System.Windows.Input;
using System.Windows.Media;
using FlavosDiag.App.ViewModels;

namespace FlavosDiag.App.Views;

/// <summary>Item do menu lateral: ícone desenhado (Path próprio) + rótulo.</summary>
public sealed class ItemNav
{
    public required PaginaBase Pagina { get; init; }

    public required string Rotulo { get; init; }

    public required Geometry GeometriaIcone { get; init; }

    public required ICommand Comando { get; init; }

    public bool Selecionado { get; init; }
}
