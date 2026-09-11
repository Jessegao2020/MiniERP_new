using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private bool _articleEnglishDisplayHooked;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // These tweaks depend on Fluent control templates having created their visual parts.
        // Keep a single OnAttachedToVisualTree override for this partial class and initialize
        // the compact quantity spinner, SelectLine-like navigation frame, and article lookup
        // display behavior together.
        Dispatcher.UIThread.Post(() =>
        {
            ApplyCompactQuantitySpinner();
            InstallSectionNavigationFrame();
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
        // The dropdown deliberately keeps Article.Name (the Chinese/internal name) so the
        // sales team can identify products quickly. Once an item is chosen, replace only
        // the editor's visible text with the quotation-facing English name. Posting this
        // until after the selection event lets AutoCompleteBox finish its own text sync first.
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(ArticleAutoComplete.SelectedItem, article))
                return;

            var displayName = PreferredArticleName(article);

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
