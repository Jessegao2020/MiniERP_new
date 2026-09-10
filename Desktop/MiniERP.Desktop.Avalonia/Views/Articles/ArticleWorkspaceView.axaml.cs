using Avalonia.Controls;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Articles;

public partial class ArticleWorkspaceView : UserControl
{
    private readonly ArticleListView _listView;
    private ArticleEditorView? _editorView;
    private int? _returnArticleId;
    private bool _deleted;

    public ArticleWorkspaceView()
    {
        InitializeComponent();

        _listView = new ArticleListView();
        _listView.OpenArticleRequested += article => ShowEditor(article, forceNew: false);
        _listView.DuplicateArticleRequested += article => ShowEditor(article, forceNew: true);
        ViewHost.Content = _listView;
    }

    public Task ReloadListAsync() => _listView.ReloadAsync();

    public void RefreshExchangeRate()
        => _editorView?.RefreshExchangeRate();

    private void ShowEditor(Article? article, bool forceNew)
    {
        var editor = new ArticleEditorView(article, forceNew);
        editor.Saved += Editor_Saved;
        editor.Deleted += Editor_Deleted;
        editor.RequestClose += Editor_RequestClose;

        _returnArticleId = forceNew ? null : article?.Id;
        _deleted = false;
        _editorView = editor;
        ViewHost.Content = editor;
    }

    private void Editor_Saved(object? sender, EventArgs e)
    {
        if (sender is ArticleEditorView editor && editor.ArticleId > 0)
            _returnArticleId = editor.ArticleId;
    }

    private void Editor_Deleted(object? sender, EventArgs e)
    {
        _deleted = true;
        _returnArticleId = null;
    }

    private async void Editor_RequestClose(object? sender, EventArgs e)
    {
        if (sender is ArticleEditorView editor)
        {
            editor.Saved -= Editor_Saved;
            editor.Deleted -= Editor_Deleted;
            editor.RequestClose -= Editor_RequestClose;

            if (!_deleted && editor.ArticleId > 0)
                _returnArticleId = editor.ArticleId;
        }

        _editorView = null;
        await _listView.ReloadAndSelectAsync(_deleted ? null : _returnArticleId);
        ViewHost.Content = _listView;
    }
}
