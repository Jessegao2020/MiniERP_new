using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private bool _articleLookupConfigured;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // These tweaks depend on Fluent control templates having created their visual parts.
        // Keep a single OnAttachedToVisualTree override for this partial class and initialize
        // the compact quantity spinner, SelectLine-like navigation frame, and article lookup
        // behavior together.
        Dispatcher.UIThread.Post(() =>
        {
            ApplyCompactQuantitySpinner();
            InstallSectionNavigationFrame();
            ConfigureArticleLookup();
        });
    }

    private void ApplyCompactQuantitySpinner()
    {
        var spinnerPanel = PositionQtyTextBox
            .GetVisualDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Name == "PART_SpinnerPanel");

        if (spinnerPanel is not null)
        {
            spinnerPanel.Orientation = Orientation.Vertical;
            spinnerPanel.Width = 18;
        }

        foreach (var button in PositionQtyTextBox.GetVisualDescendants().OfType<RepeatButton>())
        {
            button.Width = 18;
            button.MinWidth = 18;
            button.Height = 15;
            button.MinHeight = 0;
            button.Padding = new Thickness(0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;

            foreach (var icon in button.GetVisualDescendants().OfType<PathIcon>())
            {
                icon.Width = 8;
                icon.Height = 4;
            }
        }
    }

    private void ConfigureArticleLookup()
    {
        if (_articleLookupConfigured)
            return;

        _articleLookupConfigured = true;

        // The popup deliberately keeps Article.Name through its ItemTemplate so the sales
        // team can identify products by the familiar Chinese/internal name. The selected
        // text, however, is the quotation-facing English name.
        ArticleAutoComplete.ValueMemberBinding = new Binding(nameof(Article.QuotationName));

        // Search both internal/Chinese and English names.
        ArticleAutoComplete.ItemFilter = (search, item) =>
        {
            if (item is not Article article)
                return false;

            if (string.IsNullOrWhiteSpace(search))
                return true;

            return (article.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (article.Name_EN?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
        };

        // SelectedItem is the authoritative state for the Article lookup. The previous
        // implementation tried to rewrite AutoCompleteBox.Text asynchronously after a
        // selection. That allowed Text, SelectedItem and ViewModel.SelectedArticle to drift
        // apart: Save could remain disabled, and a visually empty editor could still look
        // dirty to HasUnappliedPositionChanges(). Keep the three states synchronized from
        // one event instead.
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ArticleAutoComplete.SelectionChanged += ArticleAutoComplete_SelectionChangedStable;

        // LoadPositionEditor in the original editor still assigns the internal Name to Text.
        // Normalize it after the normal double-click handler has loaded the row so an existing
        // position also keeps a valid SelectedItem and displays the English quotation name.
        PositionsGrid.DoubleTapped += PositionsGrid_DoubleTappedNormalizeArticle;

        if (ArticleAutoComplete.SelectedItem is Article selected)
            ViewModel.SelectedArticle = selected;
    }

    private void ArticleAutoComplete_SelectionChangedStable(object? sender, SelectionChangedEventArgs e)
    {
        var article = ArticleAutoComplete.SelectedItem as Article;
        ViewModel.SelectedArticle = article;

        if (_loadingPositionEditor)
        {
            // ClearPositionEditor runs with _loadingPositionEditor=true. NumericUpDown keeps
            // Value separately from its visible Text, so reset the internal value here while
            // the editor is being cleared. This prevents a blank editor from later reporting
            // unsaved changes and also prevents Amount from reappearing as 0.00.
            if (article is null && _editingPosition is null)
            {
                PositionQtyTextBox.Value = null;
                PositionQtyTextBox.Text = string.Empty;
                PositionAmountText.Text = string.Empty;
            }
            return;
        }

        if (article is null)
        {
            UpdatePositionSaveState();
            return;
        }

        ApplySelectedArticleToEditorStable(article);
    }

    private void ApplySelectedArticleToEditorStable(Article article)
    {
        _loadingPositionEditor = true;
        try
        {
            // This value matches ValueMemberBinding, so assigning it does not invalidate the
            // current AutoCompleteBox selection.
            ArticleAutoComplete.Text = article.QuotationName;
            PositionQtyTextBox.Value = 1m;
            PositionQtyTextBox.Text = "1";
            PositionUnitTextBox.Text = "PCS";
            PositionDiscountTextBox.Text = "0";
            PositionDescriptionTextBox.Text = article.Description_EN ?? string.Empty;

            if (!TryGetArticleUnitPrice(article, out var unitPrice))
            {
                PositionUnitPriceTextBox.Text = string.Empty;
                PositionAmountText.Text = string.Empty;
                PositionSaveButton.IsEnabled = false;
                return;
            }

            PositionUnitPriceTextBox.Text = unitPrice.ToString("0.####", CultureInfo.InvariantCulture);
            UpdatePositionAmountPreview();
        }
        finally
        {
            _loadingPositionEditor = false;
        }

        UpdatePositionSaveState();
        ViewModel.SetStatusMessage($"Article selected: {article.QuotationName}. Adjust the position details and click Save.");
    }

    private void PositionsGrid_DoubleTappedNormalizeArticle(object? sender, TappedEventArgs e)
    {
        if (_editingPosition?.SourceArticleId is not int articleId)
            return;

        var article = ViewModel.Articles.FirstOrDefault(candidate => candidate.Id == articleId);
        if (article is null)
            return;

        _loadingPositionEditor = true;
        try
        {
            ViewModel.SelectedArticle = article;
            ArticleAutoComplete.SelectedItem = article;
            ArticleAutoComplete.Text = article.QuotationName;
            ArticleAutoComplete.CaretIndex = article.QuotationName.Length;
        }
        finally
        {
            _loadingPositionEditor = false;
        }

        PositionSaveButton.IsEnabled = false;
    }

    private void PositionQty_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        UpdatePositionAmountPreview();
        UpdatePositionSaveState();
    }
}
