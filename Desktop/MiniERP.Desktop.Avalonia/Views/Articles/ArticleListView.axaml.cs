using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Articles;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Articles;

public partial class ArticleListView : UserControl
{
    private bool _initialLoadCompleted;
    private bool _hasSavedScrollOffset;
    private Vector _savedScrollOffset;
    private int? _articleToReveal;

    private ArticleListViewModel ViewModel => (ArticleListViewModel)DataContext!;

    public event Action<Article?>? OpenArticleRequested;
    public event Action<Article>? DuplicateArticleRequested;

    public ArticleListView()
    {
        InitializeComponent();
        DataContext = new ArticleListViewModel();

        // The XAML originally carried a placeholder IsEnabled=False on Duplicate.
        // Promote it to the real SelectLine-style duplicate command.
        if (Content is Grid root && root.Children.OfType<StackPanel>().FirstOrDefault() is { } toolbar)
        {
            var buttons = toolbar.Children.OfType<Button>().ToList();
            if (buttons.Count > 1)
            {
                buttons[1].IsEnabled = true;
                buttons[1].Click += Duplicate_Click;
            }
        }

        AttachedToVisualTree += async (_, _) =>
        {
            // Switching from list to detail detaches this view. Reloading it on
            // every reattachment used to clear all rows and reset the scrollbar.
            if (!_initialLoadCompleted)
            {
                await ViewModel.LoadAsync();
                _initialLoadCompleted = true;
            }

            ScheduleRestoreListViewport();
        };
    }

    public async Task ReloadAsync()
    {
        RememberListViewport();
        await ViewModel.LoadAsync();
        _initialLoadCompleted = true;
        ScheduleRestoreListViewport();
    }

    public async Task ReloadAndSelectAsync(int? articleId)
    {
        // When returning from a detail editor this list is detached, so keep
        // the viewport captured before opening the editor.
        RememberListViewport();
        await ViewModel.LoadAsync();
        _initialLoadCompleted = true;

        _articleToReveal = articleId is > 0 ? articleId : null;
        if (_articleToReveal is int id)
            ViewModel.SelectedArticle = ViewModel.Articles.FirstOrDefault(article => article.Id == id);

        ScheduleRestoreListViewport();
    }

    private ScrollViewer? FindArticleScrollViewer()
        => ArticleGrid.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .OrderByDescending(viewer => viewer.Extent.Height - viewer.Viewport.Height)
            .FirstOrDefault();

    private void RememberListViewport()
    {
        // An editor replaces this view in the visual tree. Do not overwrite a
        // remembered position with an unattached DataGrid's reset offset.
        if (TopLevel.GetTopLevel(this) is null)
            return;

        var viewer = FindArticleScrollViewer();
        if (viewer is null)
            return;

        _savedScrollOffset = viewer.Offset;
        _hasSavedScrollOffset = true;
    }

    private void ScheduleRestoreListViewport()
    {
        Dispatcher.UIThread.Post(() =>
        {

            if (_articleToReveal is int id)
            {
                var row = ViewModel.Articles.FirstOrDefault(article => article.Id == id);
                if (row is not null && ArticleGrid.Columns.Count > 0)
                {
                    ArticleGrid.ScrollIntoView(row, ArticleGrid.Columns[0]);
                    _articleToReveal = null;
                    return;
                }
                _articleToReveal = null;
            }

            if (!_hasSavedScrollOffset)
                return;

            var viewer = FindArticleScrollViewer();
            if (viewer is not null)
                viewer.Offset = _savedScrollOffset;
        }, DispatcherPriority.Loaded);
    }

    private void New_Click(object? sender, RoutedEventArgs e)
    {
        RememberListViewport();
        OpenArticleRequested?.Invoke(null);
    }

    private void Duplicate_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedArticle is not null)
        {
            RememberListViewport();
            DuplicateArticleRequested?.Invoke(ViewModel.SelectedArticle);
        }
    }

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
        {
            RememberListViewport();
            await ViewModel.DeleteSelectedAsync();
            ScheduleRestoreListViewport();
        }
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
        => await ReloadAsync();

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string field)
            return;

        ViewModel.SetFilter(field, textBox.Text);
    }

    private void Grid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        if (SelectLineGridSupport.GetSortField(e.Column) is not string field)
            return;

        ViewModel.SortBy(field);
        UpdateSortIndicators();
    }

    private void UpdateSortIndicators()
        => SelectLineGridSupport.UpdateSortIndicators(ArticleGrid, SortArrow);

    private string SortArrow(string field)
    {
        if (!string.Equals(ViewModel.SortField, field, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return ViewModel.SortAscending ? "▲" : "▼";
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
        if (!SelectLineGridSupport.IsDataRowEvent(e)) return;
        if (ViewModel.SelectedArticle is not null)
        {
            RememberListViewport();
            OpenArticleRequested?.Invoke(ViewModel.SelectedArticle);
        }
    }
}
