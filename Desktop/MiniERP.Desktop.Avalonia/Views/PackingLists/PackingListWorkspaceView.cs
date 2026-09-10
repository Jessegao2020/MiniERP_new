using Avalonia.Controls;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.PackingLists;

public sealed class PackingListWorkspaceView : UserControl
{
    private readonly PackingListListView _listView;
    private PackingListEditorView? _editorView;
    private bool _deleted;

    public PackingListWorkspaceView()
    {
        _listView = new PackingListListView();
        _listView.OpenPackingListRequested += ShowEditor;
        Content = _listView;
    }

    public Task ReloadListAsync() => _listView.ReloadAsync();

    public void ShowEditor(PackingList? packingList)
    {
        _editorView?.StopTracking();
        _deleted = false;

        var editor = new PackingListEditorView(packingList);
        editor.Deleted += (_, _) => _deleted = true;
        editor.RequestClose += Editor_RequestClose;
        _editorView = editor;
        Content = editor;
    }

    private async void Editor_RequestClose(object? sender, EventArgs e)
    {
        if (sender is not PackingListEditorView editor) return;
        editor.RequestClose -= Editor_RequestClose;
        int? id = _deleted ? null : editor.PackingListId > 0 ? editor.PackingListId : null;
        editor.StopTracking();
        _editorView = null;
        Content = _listView;
        await _listView.ReloadAndSelectAsync(id);
    }
}
