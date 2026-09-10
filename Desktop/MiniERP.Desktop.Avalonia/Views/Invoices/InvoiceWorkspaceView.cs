using Avalonia.Controls;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Invoices;

public sealed class InvoiceWorkspaceView : UserControl
{
    private readonly InvoiceListView _listView;
    private readonly InvoiceType _type;
    private InvoiceEditorView? _editorView;
    private bool _deleted;

    public event Action<Invoice>? CreateCommercialRequested;
    public event Action<Invoice>? CreatePackingListRequested;
    public event Action<Invoice>? CreateContractRequested;

    public InvoiceWorkspaceView(InvoiceType type)
    {
        _type = type;
        _listView = new InvoiceListView(type);
        _listView.OpenInvoiceRequested += ShowEditor;
        Content = _listView;
    }

    public Task ReloadListAsync() => _listView.ReloadAsync();

    public void ShowEditor(Invoice? invoice)
    {
        _editorView?.StopTracking();
        _deleted = false;

        var editor = new InvoiceEditorView(invoice, _type);
        editor.Deleted += (_, _) => _deleted = true;
        editor.RequestClose += Editor_RequestClose;
        editor.CreateCommercialRequested += source => CreateCommercialRequested?.Invoke(source);
        editor.CreatePackingListRequested += source => CreatePackingListRequested?.Invoke(source);
        editor.CreateContractRequested += source => CreateContractRequested?.Invoke(source);
        _editorView = editor;
        Content = editor;
    }

    private async void Editor_RequestClose(object? sender, EventArgs e)
    {
        if (sender is not InvoiceEditorView editor) return;
        editor.RequestClose -= Editor_RequestClose;
        var id = _deleted ? null : editor.InvoiceId > 0 ? editor.InvoiceId : null;
        editor.StopTracking();
        _editorView = null;
        Content = _listView;
        await _listView.ReloadAndSelectAsync(id);
    }
}
