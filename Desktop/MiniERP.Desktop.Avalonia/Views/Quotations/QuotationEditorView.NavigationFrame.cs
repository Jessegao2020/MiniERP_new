using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private void InstallSectionNavigationFrame()
    {
        var sectionTabs = this.GetVisualDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(tab => tab.Classes.Contains("document-sections"));

        if (sectionTabs is null)
            return;

        var stripPresenter = sectionTabs.GetVisualDescendants()
            .OfType<ItemsPresenter>()
            .FirstOrDefault();

        if (stripPresenter is null || AdornerLayer.GetAdorner(stripPresenter) is not null)
            return;

        var frame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#B5B5B5")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };

        AdornerLayer.SetAdorner(stripPresenter, frame);
    }
}
