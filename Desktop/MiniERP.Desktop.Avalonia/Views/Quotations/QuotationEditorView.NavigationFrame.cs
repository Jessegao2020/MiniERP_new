using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The Fluent TabControl template leaves the left tab strip visually open.
        // SelectLine frames that navigation area as a separate pane which runs from
        // the same top edge to the same bottom edge as the document work area.
        // Install the frame after the TabControl template has created its ItemsPresenter.
        Dispatcher.UIThread.Post(InstallSectionNavigationFrame);
    }

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
