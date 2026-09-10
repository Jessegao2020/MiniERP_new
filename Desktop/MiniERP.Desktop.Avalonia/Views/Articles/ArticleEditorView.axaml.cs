using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.Desktop.Controls;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Articles;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Articles;

public partial class ArticleEditorView : UserControl
{
    private readonly DecimalTextBoxFilter _priceFilter;
    private bool _trackDirtyChanges;
    private ArticleEditorViewModel ViewModel => (ArticleEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;

    public int ArticleId => ViewModel.Article.Id;

    public ArticleEditorView(Article? article, bool forceNew = false)
    {
        InitializeComponent();

        var settings = App.Services.GetRequiredService<AppSettingsService>();
        DataContext = new ArticleEditorViewModel(article, settings, forceNew);
        _priceFilter = new DecimalTextBoxFilter(PriceTextBox);

        AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.MarkClean();
                _trackDirtyChanges = true;
            }, DispatcherPriority.Loaded);
    }

    public void RefreshExchangeRate()
        => ViewModel.RefreshExchangeRate();

    private void EditorField_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_trackDirtyChanges)
            ViewModel.MarkDirty();
    }

    private void GridView_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.SaveAsync()) return;
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void Discard_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsNew)
        {
            var confirmed = await ConfirmationDialog.ShowAsync(
                this,
                "Delete Article",
                $"Delete article '{ViewModel.Article.Name}'? This cannot be undone.");

            if (!confirmed)
                return;
        }

        if (!await ViewModel.DeleteAsync()) return;

        Deleted?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
