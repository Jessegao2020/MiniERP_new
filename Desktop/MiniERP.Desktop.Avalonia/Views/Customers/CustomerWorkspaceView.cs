using Avalonia.Controls;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Customers;

public sealed class CustomerWorkspaceView : UserControl
{
    private readonly CustomerListView _listView;
    private CustomerEditorView? _editorView;
    private bool _deleted;

    public CustomerWorkspaceView()
    {
        _listView = new CustomerListView();
        _listView.OpenCustomerRequested += ShowEditor;
        Content = _listView;
    }

    public Task ReloadListAsync() => _listView.ReloadAsync();

    public void ShowEditor(Customer? customer)
    {
        _editorView?.StopTracking();
        _deleted = false;

        var editor = new CustomerEditorView(customer);
        editor.Deleted += (_, _) => _deleted = true;
        editor.RequestClose += Editor_RequestClose;
        _editorView = editor;
        Content = editor;
    }

    private async void Editor_RequestClose(object? sender, EventArgs e)
    {
        if (sender is not CustomerEditorView editor) return;
        editor.RequestClose -= Editor_RequestClose;
        int? id = _deleted ? null : editor.CustomerId > 0 ? editor.CustomerId : null;
        editor.StopTracking();
        _editorView = null;
        Content = _listView;
        await _listView.ReloadAndSelectAsync(id);
    }
}
