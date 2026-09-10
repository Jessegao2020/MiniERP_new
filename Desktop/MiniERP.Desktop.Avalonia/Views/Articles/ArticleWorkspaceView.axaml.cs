using Avalonia.Controls;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Articles;

public partial class ArticleWorkspaceView : UserControl
{
    private readonly ArticleListView _listView;
    private ArticleEditorView? _editorView;

    public ArticleWorkspaceView()
    {
        InitializeComponent();

        _listView = new ArticleListView();
        _listView.OpenArticleRequested += ShowEditor;
        ViewHost.Content = _listView;
    }

    public Task ReloadListAsync() => _listView.ReloadAsync();

    public void RefreshExchangeRate()
        => _editorView?.RefreshExchangeRate();

    private void ShowEditor(Article? article)
    {
        var editor = new ArticleEditorView(article);
        editor.RequestClose += Editor_RequestClose;

        _editorView = editor;
        ViewHost.Content = editor;
    }

    private async void Editor_RequestClose(object? sender, EventArgs e)
    {
        if (sender is ArticleEditorView editor)
            editor.RequestClose -= Editor_RequestClose;

        _editorView = null;
        await _listView.ReloadAsync();
        ViewHost.Content = _listView;
    }
}
