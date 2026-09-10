using Avalonia.Controls;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public sealed class QuotationWorkspaceView : UserControl
{
    private readonly QuotationListView _listView;
    private QuotationEditorView? _editorView;
    private bool _deleted;

    public event Action<Quotation, InvoiceType>? CreateInvoiceRequested;
    public event Action<Quotation>? CreatePackingListRequested;
    public event Action<Quotation>? CreateContractRequested;

    public QuotationWorkspaceView()
    {
        _listView = new QuotationListView();
        _listView.OpenQuotationRequested += ShowEditor;
        Content = _listView;
    }

    public Task ReloadListAsync() => _listView.ReloadAsync();
    public void RefreshExchangeRate() => _editorView?.RefreshExchangeRate();

    public void ShowEditor(Quotation? quotation)
    {
        _editorView?.StopTracking();
        _deleted = false;

        var editor = new QuotationEditorView(quotation);
        editor.Deleted += (_, _) => _deleted = true;
        editor.RequestClose += Editor_RequestClose;
        editor.CreateInvoiceRequested += (source, type) => CreateInvoiceRequested?.Invoke(source, type);
        editor.CreatePackingListRequested += source => CreatePackingListRequested?.Invoke(source);
        editor.CreateContractRequested += source => CreateContractRequested?.Invoke(source);
        _editorView = editor;
        Content = editor;
    }

    private async void Editor_RequestClose(object? sender, EventArgs e)
    {
        if (sender is not QuotationEditorView editor) return;
        editor.RequestClose -= Editor_RequestClose;
        var id = _deleted ? null : editor.QuotationId > 0 ? editor.QuotationId : null;
        editor.StopTracking();
        _editorView = null;
        Content = _listView;
        await _listView.ReloadAndSelectAsync(id);
    }
}
