using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private bool _articleLookupConfigured;
    private bool _blankPositionNormalizationScheduled;
    private Article? _positionSourceArticle;

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

        // SelectedItem is only used to seed the editor from the Article database. Once the
        // fields are populated, the visible editor values are authoritative and may be
        // changed freely before Position Save.
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ArticleAutoComplete.SelectionChanged += ArticleAutoComplete_SelectionChangedStable;

        // Article text editing intentionally breaks AutoCompleteBox.SelectedItem. Do not use
        // the old TextChanged handler for this field because it treated SelectedArticle as a
        // prerequisite for Save. The editor itself is now the source of truth.
        ArticleAutoComplete.TextChanged -= PositionEditor_TextChanged;
        ArticleAutoComplete.TextChanged += ArticleAutoComplete_TextChangedStable;

        // The other fields still use their existing calculation handler. Add a second handler
        // afterwards so the final Save state is based on the visible editor values rather than
        // AutoCompleteBox selection state.
        PositionUnitTextBox.TextChanged += PositionEditor_VisibleFieldChangedStable;
        PositionUnitPriceTextBox.TextChanged += PositionEditor_VisibleFieldChangedStable;
        PositionDiscountTextBox.TextChanged += PositionEditor_VisibleFieldChangedStable;
        PositionDescriptionTextBox.TextChanged += PositionEditor_VisibleFieldChangedStable;

        // Replace the original Position Save handler for the same reason: saving must persist
        // exactly what is currently in the editor, including a manually adjusted Article name.
        PositionSaveButton.Click -= SavePositionEdit_Click;
        PositionSaveButton.Click += SavePositionEditStable_Click;

        // Replace the original row double-tap handler. NumericUpDown and AutoCompleteBox can
        // retain transient internal values after the editor is visually cleared. A row switch
        // warning should be based on meaningful, visible user edits instead of those internal
        // control values.
        PositionsGrid.DoubleTapped -= PositionsGrid_DoubleTapped;
        PositionsGrid.DoubleTapped += PositionsGrid_DoubleTappedStable;

        if (ArticleAutoComplete.SelectedItem is Article selected)
        {
            _positionSourceArticle = selected;
            ViewModel.SelectedArticle = selected;
        }
    }

    private void ArticleAutoComplete_SelectionChangedStable(object? sender, SelectionChangedEventArgs e)
    {
        var article = ArticleAutoComplete.SelectedItem as Article;
        ViewModel.SelectedArticle = article;

        if (article is not null)
            _positionSourceArticle = article;

        if (_loadingPositionEditor)
        {
            if (article is null && _editingPosition is null)
            {
                _positionSourceArticle = null;
                PositionQtyTextBox.Value = null;
                PositionQtyTextBox.Text = string.Empty;
                PositionAmountText.Text = string.Empty;
                ScheduleBlankPositionNormalization();
            }
            return;
        }

        if (article is null)
        {
            // When the user manually edits the selected Article name AutoCompleteBox clears
            // SelectedItem. Keep _positionSourceArticle so the line remains linked to the
            // originally chosen database Article, but let the visible text be saved verbatim.
            if (IsVisuallyBlankNewPositionEditor())
            {
                _positionSourceArticle = null;
                ScheduleBlankPositionNormalization();
            }

            UpdateStablePositionSaveState();
            return;
        }

        ApplySelectedArticleToEditorStable(article);
    }

    private void ArticleAutoComplete_TextChangedStable(object? sender, TextChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        if (_editingPosition is null && IsVisuallyBlankNewPositionEditor())
        {
            _positionSourceArticle = null;
            PositionAmountText.Text = string.Empty;
            PositionSaveButton.IsEnabled = false;
            ScheduleBlankPositionNormalization();
            return;
        }

        UpdateStablePositionSaveState();
    }

    private void PositionEditor_VisibleFieldChangedStable(object? sender, TextChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        UpdateStablePositionSaveState();
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
            return !PositionEditorMatchesCurrentItemStable();

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

    private void NormalizeLoadedArticleDisplay(QuotationItemRowViewModel item)
    {
        _positionSourceArticle = null;

        if (item.SourceArticleId is not int articleId)
        {
            UpdateStablePositionSaveState();
            return;
        }

        var article = ViewModel.Articles.FirstOrDefault(candidate => candidate.Id == articleId);
        if (article is null)
        {
            UpdateStablePositionSaveState();
            return;
        }

        _positionSourceArticle = article;
        _loadingPositionEditor = true;
        try
        {
            ViewModel.SelectedArticle = article;
            ArticleAutoComplete.SelectedItem = article;
            // Existing quotation items may deliberately contain a customized Article name.
            // Keep the stored line name instead of forcing the database Name_EN back over it.
            ArticleAutoComplete.Text = item.ArticleName;
            ArticleAutoComplete.CaretIndex = item.ArticleName.Length;
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
                _positionSourceArticle = null;
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
        _positionSourceArticle = article;
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

        UpdateStablePositionSaveState();
        ViewModel.SetStatusMessage($"Article selected: {article.QuotationName}. Adjust the position details and click Save.");
    }

    private void PositionQty_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        if (PositionQtyTextBox.Value is null || string.IsNullOrWhiteSpace(PositionQtyTextBox.Text))
        {
            PositionAmountText.Text = string.Empty;
            UpdateStablePositionSaveState();
            return;
        }

        UpdatePositionAmountPreview();
        UpdateStablePositionSaveState();
    }

    private void UpdateStablePositionSaveState()
    {
        if (_loadingPositionEditor)
            return;

        var articleName = (ArticleAutoComplete.Text ?? string.Empty).Trim();
        var quantityText = NormalizeDecimalInput(PositionQtyTextBox.Text);
        var unit = (PositionUnitTextBox.Text ?? string.Empty).Trim();
        var unitPriceText = NormalizeDecimalInput(PositionUnitPriceTextBox.Text);
        var discountText = NormalizeDecimalInput(PositionDiscountTextBox.Text);

        var isValid = !string.IsNullOrWhiteSpace(articleName)
            && TryParseDecimal(quantityText, out var quantity) && quantity > 0
            && !string.IsNullOrWhiteSpace(unit)
            && TryParseDecimal(unitPriceText, out var unitPrice) && unitPrice >= 0
            && TryParseDecimal(discountText, out var discount) && discount >= 0 && discount <= 100;

        PositionSaveButton.IsEnabled = isValid
            && (_editingPosition is null || !PositionEditorMatchesCurrentItemStable());
    }

    private bool PositionEditorMatchesCurrentItemStable()
    {
        if (_editingPosition is null)
            return false;

        var sourceArticleId = _positionSourceArticle?.Id ?? _editingPosition.SourceArticleId;

        return sourceArticleId == _editingPosition.SourceArticleId
            && string.Equals((ArticleAutoComplete.Text ?? string.Empty).Trim(), _editingPosition.ArticleName, StringComparison.Ordinal)
            && string.Equals(NormalizeDecimalInput(PositionQtyTextBox.Text), _editingPosition.QuantityText, StringComparison.Ordinal)
            && string.Equals(PositionUnitTextBox.Text ?? string.Empty, _editingPosition.Unit, StringComparison.Ordinal)
            && string.Equals(NormalizeDecimalInput(PositionUnitPriceTextBox.Text), _editingPosition.UnitPriceText, StringComparison.Ordinal)
            && string.Equals(NormalizeDecimalInput(PositionDiscountTextBox.Text), _editingPosition.DiscountText, StringComparison.Ordinal)
            && string.Equals(PositionDescriptionTextBox.Text ?? string.Empty, _editingPosition.Description ?? string.Empty, StringComparison.Ordinal);
    }

    private void SavePositionEditStable_Click(object? sender, RoutedEventArgs e)
    {
        var articleName = (ArticleAutoComplete.Text ?? string.Empty).Trim();
        var quantityText = NormalizeDecimalInput(PositionQtyTextBox.Text);
        var unit = (PositionUnitTextBox.Text ?? string.Empty).Trim();
        var unitPriceText = NormalizeDecimalInput(PositionUnitPriceTextBox.Text);
        var discountText = NormalizeDecimalInput(PositionDiscountTextBox.Text);
        var description = PositionDescriptionTextBox.Text;

        if (string.IsNullOrWhiteSpace(articleName))
        {
            ViewModel.SetStatusMessage("The position article name is required.");
            return;
        }

        if (!TryParseDecimal(quantityText, out var quantity) || quantity <= 0)
        {
            ViewModel.SetStatusMessage($"Quantity for '{articleName}' must be greater than zero.");
            return;
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            ViewModel.SetStatusMessage($"Unit for '{articleName}' is required.");
            return;
        }

        if (!TryParseDecimal(unitPriceText, out var unitPrice) || unitPrice < 0)
        {
            ViewModel.SetStatusMessage($"Unit price for '{articleName}' cannot be negative.");
            return;
        }

        if (!TryParseDecimal(discountText, out var discount) || discount < 0 || discount > 100)
        {
            ViewModel.SetStatusMessage($"Discount for '{articleName}' must be between 0 and 100%.");
            return;
        }

        var isNewPosition = _editingPosition is null;
        QuotationItemRowViewModel target;

        if (isNewPosition)
        {
            // Reuse the ViewModel's row-creation path so totals/property subscriptions stay
            // intact. If this is a completely manual line with no database Article selected,
            // use a temporary Article only to create the row and immediately clear its source id.
            var sourceArticle = _positionSourceArticle;
            Article articleForCreation;

            if (sourceArticle is not null)
            {
                articleForCreation = sourceArticle;
            }
            else
            {
                var cnyPrice = ViewModel.Currency == "USD"
                    ? unitPrice * ViewModel.ExchangeRateSnapshot
                    : unitPrice;

                articleForCreation = new Article
                {
                    Name = articleName,
                    Name_EN = articleName,
                    Description_EN = description,
                    Price = cnyPrice
                };
            }

            ViewModel.SelectedArticle = articleForCreation;
            var countBefore = ViewModel.Items.Count;
            ViewModel.AddSelectedArticle();
            if (ViewModel.Items.Count == countBefore)
            {
                UpdateStablePositionSaveState();
                return;
            }

            target = ViewModel.Items[^1];
            target.SetSourceArticle(sourceArticle?.Id);
        }
        else
        {
            target = _editingPosition!;
            target.SetSourceArticle(_positionSourceArticle?.Id ?? target.SourceArticleId);
        }

        // The editor is authoritative. Never replace a manually adjusted line name with the
        // selected Article's database name during Save.
        target.ArticleName = articleName;
        target.QuantityText = quantityText;
        target.Unit = unit;
        target.UnitPriceText = unitPriceText;
        target.DiscountText = discountText;
        target.Description = description;

        RenumberPositions();
        ViewModel.SelectedItem = target;
        ClearPositionEditor();
        ViewModel.SetStatusMessage(isNewPosition
            ? $"Position '{target.ArticleName}' added to the overview. Save the document to persist it."
            : $"Position '{target.ArticleName}' updated in the overview. Save the document to persist it.");
    }
}
