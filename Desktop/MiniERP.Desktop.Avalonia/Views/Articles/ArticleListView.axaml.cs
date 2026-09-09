using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Desktop.Infrastructure;
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
        AttachedToVisualTree += async (_, _) =>
        {
            await ViewModel.LoadAsync();
            SyncDataGridColumnWidths();
        };
    }

    public Task ReloadAsync() => ViewModel.LoadAsync();

    private void New_Click(object? sender, RoutedEventArgs e)
        => OpenArticleRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedArticle is null)
        {
            await ViewModel.DeleteSelectedAsync();
            return;
        }

        var confirmed = await ConfirmationDialog.ShowAsync(
            this,
            "Delete Article",
            $"Delete article '{ViewModel.SelectedArticle.Name}'? This cannot be undone.");

        if (confirmed)
            await ViewModel.DeleteSelectedAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.LoadAsync();

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string field)
            return;

        ViewModel.SetFilter(field, textBox.Text);
    }

    private void Sort_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string field)
            return;

        ViewModel.SortBy(field);
        UpdateSortIndicators();
    }

    private void UpdateSortIndicators()
    {
        NameSortArrow.Text = SortArrow("Name");
        PriceSortArrow.Text = SortArrow("Price");
        MinimumPriceSortArrow.Text = SortArrow("MinimumPrice");
        DescriptionSortArrow.Text = SortArrow("Description");
        SpecificationSortArrow.Text = SortArrow("Specification");
        DiscountSortArrow.Text = SortArrow("Discount");
        NoteSortArrow.Text = SortArrow("Note");
    }

    private string SortArrow(string field)
    {
        if (!string.Equals(ViewModel.SortField, field, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return ViewModel.SortAscending ? "▲" : "▼";
    }

    private void ArticleTableLayout_SizeChanged(object? sender, SizeChangedEventArgs e)
        => SyncDataGridColumnWidths();

    private void SyncDataGridColumnWidths()
    {
        if (ArticleGrid.Columns.Count != ArticleTableLayout.ColumnDefinitions.Count)
            return;

        for (var i = 0; i < ArticleGrid.Columns.Count; i++)
        {
            var width = ArticleTableLayout.ColumnDefinitions[i].ActualWidth;
            if (width > 0)
                ArticleGrid.Columns[i].Width = new DataGridLength(width);
        }
    }

    private void ArticleGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        const string alternateClass = "alternate";

        if (e.Row.Index % 2 == 1)
        {
            if (!e.Row.Classes.Contains(alternateClass))
                e.Row.Classes.Add(alternateClass);
        }
        else
        {
            e.Row.Classes.Remove(alternateClass);
        }
    }

    private void ArticleGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedArticle is not null)
            OpenArticleRequested?.Invoke(ViewModel.SelectedArticle);
    }
}
