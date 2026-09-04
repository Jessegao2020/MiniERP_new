using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniERP.Desktop.ViewModels.Articles;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Articles;

public partial class ArticleEditorView : UserControl
{
    private ArticleEditorViewModel ViewModel => (ArticleEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;

    public ArticleEditorView(Article? article)
    {
        InitializeComponent();
        DataContext = new ArticleEditorViewModel(article);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.SaveAsync()) return;

        Saved?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void Discard_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.DeleteAsync()) return;

        Deleted?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
