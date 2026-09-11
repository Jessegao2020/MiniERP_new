using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private bool _articleEnglishDisplayHooked;
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
            HookArticleEnglishDisplay();
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

        // The text portion of the lookup must use the quotation-facing English name.
        // The ItemTemplate still renders Article.Name, so the popup remains the familiar
        // Chinese/internal product list used by the sales team.
        ArticleAutoComplete.ValueMemberBinding = new Binding(nameof(Article.QuotationName));

        // Do not sacrifice the convenient lookup behavior just because the visible selected
        // value is English. Search both the internal/Chinese name and the English name.
        ArticleAutoComplete.ItemFilter = (search, item) =>
        {
            if (item is not Article article)
                return false;

            if (string.IsNullOrWhiteSpace(search))
                return true;

            return (article.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (article.Name_EN?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
        };
    }

    private void HookArticleEnglishDisplay()
    {
        if (_articleEnglishDisplayHooked)
            return;

        _articleEnglishDisplayHooked = true;
        ArticleAutoComplete.SelectionChanged += ArticleAutoComplete_SelectionChanged;

        if (ArticleAutoComplete.SelectedItem is Article article)
            QueueEnglishArticleName(article);
    }

    private void ArticleAutoComplete_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ArticleAutoComplete.SelectedItem is Article article)
            QueueEnglishArticleName(article);
    }

    private void QueueEnglishArticleName(Article article)
    {
        // ApplySelectedArticleToEditor still fills the editor fields when selection changes.
        // AutoCompleteBox then finishes its own text synchronization asynchronously. Post one
        // final assignment using the SAME value exposed through ValueMemberBinding. Because
        // those values now match, the control no longer clears SelectedItem/SelectedArticle.
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(ArticleAutoComplete.SelectedItem, article))
                return;

            var displayName = article.QuotationName;

            _loadingPositionEditor = true;
            try
            {
                ArticleAutoComplete.Text = displayName;
                ArticleAutoComplete.CaretIndex = displayName.Length;
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
