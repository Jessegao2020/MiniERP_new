using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Both tweaks depend on Fluent control templates having created their visual parts.
        // Keep a single OnAttachedToVisualTree override for this partial class and initialize
        // the compact quantity spinner and the SelectLine-like navigation frame together.
        Dispatcher.UIThread.Post(() =>
        {
            ApplyCompactQuantitySpinner();
            InstallSectionNavigationFrame();
        });
    }

    private void ApplyCompactQuantitySpinner()
    {
        var spinnerPanel = PositionQtyTextBox
            .GetVisualDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Name == "PART_SpinnerPanel");

        if (spinnerPanel is not null)
        {
            spinnerPanel.Orientation = Orientation.Vertical;
            spinnerPanel.Width = 18;
        }

        foreach (var button in PositionQtyTextBox.GetVisualDescendants().OfType<RepeatButton>())
        {
            button.Width = 18;
            button.MinWidth = 18;
            button.Height = 15;
            button.MinHeight = 0;
            button.Padding = new Thickness(0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;

            foreach (var icon in button.GetVisualDescendants().OfType<PathIcon>())
            {
                icon.Width = 8;
                icon.Height = 4;
            }
        }
    }

    private void PositionQty_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        UpdatePositionAmountPreview();
        UpdatePositionSaveState();
    }
}
