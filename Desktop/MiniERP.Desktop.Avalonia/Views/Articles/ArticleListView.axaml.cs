using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Desktop.ViewModels.Articles;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Articles;

public partial class ArticleListView : UserControl
{
    private ArticleListViewModel ViewModel => (ArticleListViewModel)DataContext!;

    public event Action<Article?>? OpenArticleRequested;

    public ArticleListView()
    {
        InitializeComponent();
        DataContext = new ArticleListViewModel();
        AttachedToVisualTree += async (_, _) => await ViewModel.LoadAsync();
    }

    public Task ReloadAsync() => ViewModel.LoadAsync();

    private void New_Click(object? sender, RoutedEventArgs e)
        => OpenArticleRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.DeleteSelectedAsync();

    private async void Search_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.LoadAsync();

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SearchText = string.Empty;
        await ViewModel.LoadAsync();
    }

    private void ArticleGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedArticle is not null)
            OpenArticleRequested?.Invoke(ViewModel.SelectedArticle);
    }
}
