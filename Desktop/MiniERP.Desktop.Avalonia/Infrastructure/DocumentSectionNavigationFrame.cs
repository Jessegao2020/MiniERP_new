using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Gives non-quotation document editors the same thin left-navigation frame as
/// the Quotation editor, without changing their existing document commands.
/// </summary>
internal static class DocumentSectionNavigationFrame
{
    public static void Install(Control view)
    {
        var tabs = view.GetVisualDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(tab => tab.Classes.Contains("document-sections"));

        var presenter = tabs?.GetVisualDescendants()
            .OfType<ItemsPresenter>()
            .FirstOrDefault();

        if (presenter is null || AdornerLayer.GetAdorner(presenter) is not null)
            return;

        var frame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#B5B5B5")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };

        AdornerLayer.SetAdorner(presenter, frame);
    }
}
