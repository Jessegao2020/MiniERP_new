using Avalonia.Controls;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Contracts;

public sealed class ContractWorkspaceView : UserControl
{
    private readonly ContractListView _listView;
    private ContractEditorView? _editorView;
    private bool _deleted;

    public event Action<Contract, InvoiceType>? CreateInvoiceRequested;
    public event Action<Contract>? CreatePackingListRequested;

    public ContractWorkspaceView()
    {
        _listView = new ContractListView();
        _listView.OpenContractRequested += ShowEditor;
        Content = _listView;
    }

    public Task ReloadListAsync() => _listView.ReloadAsync();
    public void RefreshExchangeRate() => _editorView?.RefreshExchangeRate();

    public void ShowEditor(Contract? contract)
    {
        _editorView?.StopTracking();
        _deleted = false;

        var editor = new ContractEditorView(contract);
        editor.Deleted += (_, _) => _deleted = true;
        editor.RequestClose += Editor_RequestClose;
        editor.CreateInvoiceRequested += (source, type) => CreateInvoiceRequested?.Invoke(source, type);
        editor.CreatePackingListRequested += source => CreatePackingListRequested?.Invoke(source);
        _editorView = editor;
        Content = editor;
    }

    private async void Editor_RequestClose(object? sender, EventArgs e)
    {
        if (sender is not ContractEditorView editor) return;
        editor.RequestClose -= Editor_RequestClose;
        var id = _deleted ? null : editor.ContractId > 0 ? editor.ContractId : null;
        editor.StopTracking();
        _editorView = null;
        Content = _listView;
        await _listView.ReloadAndSelectAsync(id);
    }
}
