using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniERP.Desktop.ViewModels.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private bool _articleLookupConfigured;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // These tweaks depend on Fluent control templates having created their visual parts.
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

        // Keep the popup ItemTemplate in Chinese/internal naming, but let the text portion
        // of AutoCompleteBox use the quotation-facing English name. This is native
        // AutoCompleteBox behavior and, unlike manually rewriting Text after selection,
        // does not break SelectedItem/SelectedArticle.
        ArticleAutoComplete.ValueMemberBinding = new Binding(nameof(Article.QuotationName));
        ArticleAutoComplete.ItemFilter = (search, item) =>
        {
            if (item is not Article article)
                return false;

            if (string.IsNullOrWhiteSpace(search))
                return true;

            return (article.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (article.Name_EN?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
        };

        // The original handler writes Article.Name (the internal/Chinese name) into the
        // AutoCompleteBox. With an English ValueMemberBinding that text no longer matches
        // the selected item, so AutoCompleteBox clears its selection: Save stays disabled
        // and the old SelectionChanged workaround can recurse during Discard. Replace that
        // handler with an English-aware version instead of mutating Text after selection.
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChangedEnglishArticle;

        // LoadPositionEditor deliberately suppresses PropertyChanged while it fills an
        // existing row, so normalize the visible article name after its double-click handler.
        PositionsGrid.DoubleTapped += PositionsGrid_DoubleTappedEnglishArticle;
    }

    private void ViewModel_PropertyChangedEnglishArticle(object? sender, PropertyChangedEventArgs e)
    {
        if (_loadingPositionEditor || e.PropertyName != nameof(QuotationEditorViewModel.SelectedArticle))
            return;

        if (ViewModel.SelectedArticle is not null)
            ApplySelectedArticleToEditorEnglish(ViewModel.SelectedArticle);
        else
            UpdatePositionSaveState();
    }

    private void ApplySelectedArticleToEditorEnglish(Article article)
    {
        _loadingPositionEditor = true;
        try
        {
            ArticleAutoComplete.Text = article.QuotationName;
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

    private void PositionsGrid_DoubleTappedEnglishArticle(object? sender, TappedEventArgs e)
    {
        if (_editingPosition is null || ViewModel.SelectedArticle is not Article article)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_editingPosition is null || !ReferenceEquals(ViewModel.SelectedArticle, article))
                return;

            _loadingPositionEditor = true;
            try
            {
                ArticleAutoComplete.Text = article.QuotationName;
                ArticleAutoComplete.CaretIndex = article.QuotationName.Length;
            }
            finally
            {
                _loadingPositionEditor = false;
            }

            UpdatePositionSaveState();
        });
    }

    private void PositionQty_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        UpdatePositionAmountPreview();
        UpdatePositionSaveState();
    }
}
