using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private bool _articleLookupConfigured;
    private bool _blankPositionNormalizationScheduled;

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

        // Replace the original row double-tap handler. NumericUpDown and AutoCompleteBox can
        // retain transient internal values after the editor is visually cleared. A row switch
        // warning should be based on meaningful, visible user edits instead of those internal
        // control values.
        PositionsGrid.DoubleTapped -= PositionsGrid_DoubleTapped;
        PositionsGrid.DoubleTapped += PositionsGrid_DoubleTappedStable;

        // AutoCompleteBox can finish synchronizing its text one dispatcher turn after a
        // selection is cleared. Run a final visual normalization after that text event too,
        // so the derived Amount cannot reappear as 0.00 in an otherwise blank editor.
        ArticleAutoComplete.TextChanged += ArticleAutoComplete_TextChangedNormalizeBlank;

        if (ArticleAutoComplete.SelectedItem is Article selected)
            ViewModel.SelectedArticle = selected;
    }

    private void ArticleAutoComplete_SelectionChangedStable(object? sender, SelectionChangedEventArgs e)
    {
        var article = ArticleAutoComplete.SelectedItem as Article;
        ViewModel.SelectedArticle = article;

        if (_loadingPositionEditor)
        {
            if (article is null && _editingPosition is null)
            {
                PositionQtyTextBox.Value = null;
                PositionQtyTextBox.Text = string.Empty;
                PositionAmountText.Text = string.Empty;
                ScheduleBlankPositionNormalization();
            }
            return;
        }

        if (article is null)
        {
            ScheduleBlankPositionNormalization();
            UpdatePositionSaveState();
            return;
        }

        ApplySelectedArticleToEditorStable(article);
    }

    private void ArticleAutoComplete_TextChangedNormalizeBlank(object? sender, TextChangedEventArgs e)
    {
        if (_editingPosition is not null)
            return;

        if (IsVisuallyBlankNewPositionEditor())
        {
            PositionAmountText.Text = string.Empty;
            PositionSaveButton.IsEnabled = false;
            ScheduleBlankPositionNormalization();
        }
    }

    private bool IsVisuallyBlankNewPositionEditor()
        => _editingPosition is null
           && string.IsNullOrWhiteSpace(ArticleAutoComplete.Text)
           && string.IsNullOrWhiteSpace(PositionUnitTextBox.Text)
           && string.IsNullOrWhiteSpace(PositionUnitPriceTextBox.Text)
           && string.IsNullOrWhiteSpace(PositionDiscountTextBox.Text)
           && string.IsNullOrWhiteSpace(PositionDescriptionTextBox.Text);

    private bool HasMeaningfulPositionEditorChanges()
    {
        if (_editingPosition is not null)
            return !PositionEditorMatchesCurrentItem();

        // Qty and Amount are intentionally excluded for a new blank editor. NumericUpDown can
        // keep a hidden Value even when its text box is visually empty, and Amount is derived.
        // With no article/name or other editable position data there is nothing meaningful to
        // lose, so switching rows must not show a discard warning.
        return !string.IsNullOrWhiteSpace(ArticleAutoComplete.Text)
            || !string.IsNullOrWhiteSpace(PositionUnitTextBox.Text)
            || !string.IsNullOrWhiteSpace(PositionUnitPriceTextBox.Text)
            || !string.IsNullOrWhiteSpace(PositionDiscountTextBox.Text)
            || !string.IsNullOrWhiteSpace(PositionDescriptionTextBox.Text);
    }

    private async void PositionsGrid_DoubleTappedStable(object? sender, TappedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        if (item is null)
            return;

        if (HasMeaningfulPositionEditorChanges())
        {
            if (ReferenceEquals(_editingPosition, item))
                return;

            var previousSelection = _editingPosition;
            var discard = await ConfirmationDialog.ShowAsync(
                this,
                "Discard Position Changes",
                "The current position has unsaved changes. Ignore them and edit the selected position?",
                "Yes",
                "No");

            if (!discard)
            {
                ViewModel.SelectedItem = previousSelection;
                return;
            }

            ClearPositionEditor();
            ViewModel.SelectedItem = item;
        }

        LoadPositionEditor(item);
        NormalizeLoadedArticleDisplay(item);
        ViewModel.SetStatusMessage($"Editing position: {item.ArticleName}");
    }

    private void NormalizeLoadedArticleDisplay(MiniERP.Desktop.ViewModels.Quotations.QuotationItemRowViewModel item)
    {
        if (item.SourceArticleId is not int articleId)
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

    private void ScheduleBlankPositionNormalization()
    {
        if (_blankPositionNormalizationScheduled)
            return;

        _blankPositionNormalizationScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            // A visually blank editor wins over stale internal control state. In particular,
            // AutoCompleteBox can temporarily restore its old SelectedItem after Text has
            // already been cleared. Do not let that stale selection abort the cleanup.
            if (_editingPosition is not null || !IsVisuallyBlankNewPositionEditor())
            {
                _blankPositionNormalizationScheduled = false;
                return;
            }

            _loadingPositionEditor = true;
            try
            {
                ViewModel.SelectedArticle = null;
                ArticleAutoComplete.IsDropDownOpen = false;
                ArticleAutoComplete.SelectedItem = null;
                ArticleAutoComplete.Text = string.Empty;
                ArticleAutoComplete.CaretIndex = 0;

                PositionQtyTextBox.Value = null;
                PositionQtyTextBox.Text = string.Empty;
                PositionAmountText.Text = string.Empty;
                PositionSaveButton.IsEnabled = false;
            }
            finally
            {
                _loadingPositionEditor = false;
                _blankPositionNormalizationScheduled = false;
            }
        });
    }

    private void ApplySelectedArticleToEditorStable(Article article)
    {
        _loadingPositionEditor = true;
        try
        {
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

    private void PositionQty_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        if (PositionQtyTextBox.Value is null || string.IsNullOrWhiteSpace(PositionQtyTextBox.Text))
        {
            PositionAmountText.Text = string.Empty;
            UpdatePositionSaveState();
            return;
        }

        UpdatePositionAmountPreview();
        UpdatePositionSaveState();
    }
}
