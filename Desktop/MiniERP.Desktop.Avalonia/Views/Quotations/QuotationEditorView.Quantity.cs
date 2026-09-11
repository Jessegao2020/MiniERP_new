using Avalonia.Controls;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private void PositionQty_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        UpdatePositionAmountPreview();
        UpdatePositionSaveState();
    }
}
