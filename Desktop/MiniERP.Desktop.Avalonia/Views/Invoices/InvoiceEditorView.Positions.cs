using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Invoices;
using MiniERP.Desktop.Views.Quotations;
using MiniERP.Domain;
using MiniERP.ApplicationLayer.Services;

namespace MiniERP.Desktop.Views.Invoices;

public partial class InvoiceEditorView
{
    private InvoiceItemRowViewModel? _editingPosition;
    private Article? _positionSourceArticle;
    private Article? _pendingExplicitArticle;
    private Article? _highlightedArticleSuggestion;
    private Control? _articleSuggestionPointerRoot;
    private bool _loadingPositionEditor;
    private bool _positionLookupConfigured;
    private bool _blankNormalizationQueued;

    private void InitializePositionEditor()
    {
        PositionsGrid.HeadersVisibility = DataGridHeadersVisibility.All;
        PositionsGrid.RowHeaderWidth = 42;
        ClearPositionEditor();
        NormalizePickerButtons();
    }

    private void NormalizePickerButtons()
    {
        // A picker belongs inside the shared input border, not as a separate button.
        foreach (var button in this.GetLogicalDescendants()
                     .OfType<Button>()
                     .Where(button => button.Classes.Contains("lookup-button")))
        {
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Stretch;
            button.CornerRadius = new CornerRadius(0);
            button.MinHeight = 0;
        }
    }

    private void ConfigurePositionEditor()
    {
        if (_positionLookupConfigured)
            return;

        _positionLookupConfigured = true;
        ArticleAutoComplete.IsTextCompletionEnabled = false;
        ArticleAutoComplete.ValueMemberBinding = new Binding(nameof(Article.QuotationName));
        ArticleAutoComplete.ItemFilter = (search, item) =>
        {
            if (item is not Article article)
                return false;
            return string.IsNullOrWhiteSpace(search)
                || (article.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (article.Name_EN?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
        };

        ArticleAutoComplete.SelectionChanged += ArticleAutoComplete_SelectionChanged;
        ArticleAutoComplete.DropDownOpened += ArticleAutoComplete_DropDownOpened;
        ArticleAutoComplete.KeyDown += ArticleAutoComplete_KeyDown;

        // A TabControl does not create the inactive tab's template until it is shown.
        // Re-apply the compact arrows whenever the Qty control enters the visual tree.
        PositionQtyTextBox.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(ApplyCompactQuantitySpinner, DispatcherPriority.Loaded);
        ApplyCompactQuantitySpinner();
        Dispatcher.UIThread.Post(ApplyCompactQuantitySpinner, DispatcherPriority.Render);
        UpdatePositionSaveState();
    }

    private void ApplyCompactQuantitySpinner()
    {
        var spinner = PositionQtyTextBox.GetVisualDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Name == "PART_SpinnerPanel");
        if (spinner is not null)
        {
            spinner.Orientation = Orientation.Vertical;
            spinner.Width = 18;
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

    private void ArticleAutoComplete_DropDownOpened(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var popup = ArticleAutoComplete.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
            var pointerRoot = popup?.Child;
            if (pointerRoot is null || ReferenceEquals(pointerRoot, _articleSuggestionPointerRoot))
                return;

            if (_articleSuggestionPointerRoot is not null)
            {
                _articleSuggestionPointerRoot.RemoveHandler(
                    InputElement.PointerPressedEvent, ArticleSuggestion_PointerPressed);
            }

            _articleSuggestionPointerRoot = pointerRoot;
            pointerRoot.AddHandler(
                InputElement.PointerPressedEvent, ArticleSuggestion_PointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        });
    }

    private void ArticleSuggestion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ArticleAutoComplete.IsDropDownOpen || e.Source is not Visual source)
            return;

        var article = (source as StyledElement)?.DataContext as Article
            ?? source.GetVisualAncestors()
                .OfType<StyledElement>()
                .Select(element => element.DataContext)
                .OfType<Article>()
                .FirstOrDefault();

        if (article is null)
            return;

        _pendingExplicitArticle = article;
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_pendingExplicitArticle, article))
                return;
            _pendingExplicitArticle = null;
            ApplyExplicitArticle(article);
        });
    }

    private void ArticleAutoComplete_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !ArticleAutoComplete.IsDropDownOpen)
            return;

        // Avalonia may clear its temporary SelectedItem before the posted
        // action runs; keep the highlighted suggestion for explicit Enter.
        var selected = ArticleAutoComplete.SelectedItem as Article
            ?? _highlightedArticleSuggestion;
        if (selected is not null)
            Dispatcher.UIThread.Post(() => ApplyExplicitArticle(selected));
    }

    private void ArticleAutoComplete_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        // AutoCompleteBox also changes SelectedItem when typed text becomes an
        // exact match. That is NOT permission to replace a customized position.
        if (ArticleAutoComplete.SelectedItem is Article article
            && !ReferenceEquals(_pendingExplicitArticle, article))
        {
            if (ArticleAutoComplete.IsDropDownOpen)
                _highlightedArticleSuggestion = article;
            var name = ArticleAutoComplete.Text ?? string.Empty;
            _loadingPositionEditor = true;
            try
            {
                ArticleAutoComplete.SelectedItem = null;
                ViewModel.SelectedArticle = null;
                ArticleAutoComplete.Text = name;
                ArticleAutoComplete.CaretIndex = name.Length;
            }
            finally
            {
                _loadingPositionEditor = false;
            }
        }

        UpdatePositionSaveState();
    }

    private async void PickPositionArticle_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;
        var picker = new ArticlePickerWindow(ViewModel.Articles, _positionSourceArticle?.Id);
        var selected = await picker.ShowDialog<Article?>(owner);
        if (selected is not null)
            ApplyExplicitArticle(selected);
    }

    private void ApplyExplicitArticle(Article article)
    {
        _pendingExplicitArticle = null;
        _positionSourceArticle = article;
        _loadingPositionEditor = true;
        try
        {
            ViewModel.SelectedArticle = article;
            ArticleAutoComplete.SelectedItem = article;
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
            }
            else
            {
                PositionUnitPriceTextBox.Text = unitPrice.ToString("0.####", CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _loadingPositionEditor = false;
        }
        UpdatePositionPreview();
        UpdatePositionSaveState();
    }

    private bool TryGetArticleUnitPrice(Article article, out decimal unitPrice)
    {
        if (InvoiceRules.TryGetArticleUnitPrice(article, ViewModel.Currency, ViewModel.ExchangeRateSnapshot, out unitPrice, out var error))
            return true;
        ViewModel.SetStatusMessage(error);
        return false;
    }

    private void PositionEditor_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;

        if (ReferenceEquals(sender, ArticleAutoComplete))
            _highlightedArticleSuggestion = null;

        UpdatePositionPreview();
        UpdatePositionSaveState();

        if (_editingPosition is null && IsBlankNewPositionEditor())
            ScheduleBlankPositionNormalization();
    }

    private void PositionQty_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_loadingPositionEditor)
            return;
        UpdatePositionPreview();
        UpdatePositionSaveState();
    }

    private static bool TryParsePositionDecimal(string? input, out decimal value)
    {
        var normalized = (input ?? string.Empty).Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);
    }

    private static string DecimalText(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    private void UpdatePositionPreview()
    {
        if (TryParsePositionDecimal(PositionQtyTextBox.Text, out var qty)
            && TryParsePositionDecimal(PositionUnitPriceTextBox.Text, out var price)
            && TryParsePositionDecimal(PositionDiscountTextBox.Text, out var discount))
        {
            var amount = SalesLineAmounts.Calculate(qty, price, discount);
            PositionAmountText.Text = amount.ToString("N2", CultureInfo.InvariantCulture);
        }
        else
        {
            PositionAmountText.Text = string.Empty;
        }
    }

    private bool IsBlankNewPositionEditor()
        => _editingPosition is null
            && string.IsNullOrWhiteSpace(ArticleAutoComplete.Text)
            && string.IsNullOrWhiteSpace(PositionUnitTextBox.Text)
            && string.IsNullOrWhiteSpace(PositionUnitPriceTextBox.Text)
            && string.IsNullOrWhiteSpace(PositionDiscountTextBox.Text)
            && string.IsNullOrWhiteSpace(PositionDescriptionTextBox.Text);

    private bool PositionMatchesCurrent()
    {
        if (_editingPosition is null)
            return false;

        var sourceId = _positionSourceArticle?.Id ?? _editingPosition.SourceArticleId;
        return sourceId == _editingPosition.SourceArticleId
            && string.Equals((ArticleAutoComplete.Text ?? string.Empty).Trim(),
                _editingPosition.ArticleName, StringComparison.Ordinal)
            && TryParsePositionDecimal(PositionQtyTextBox.Text, out var qty)
            && qty == _editingPosition.Quantity
            && string.Equals(PositionUnitTextBox.Text ?? string.Empty, _editingPosition.Unit,
                StringComparison.Ordinal)
            && TryParsePositionDecimal(PositionUnitPriceTextBox.Text, out var price)
            && price == _editingPosition.UnitPrice
            && TryParsePositionDecimal(PositionDiscountTextBox.Text, out var discount)
            && discount == _editingPosition.DiscountPercent
            && string.Equals(PositionDescriptionTextBox.Text ?? string.Empty,
                _editingPosition.Description ?? string.Empty, StringComparison.Ordinal);
    }

    private bool HasUnappliedPositionChanges()
    {
        if (_editingPosition is not null)
            return !PositionMatchesCurrent();

        // NumericUpDown may retain a hidden Value after clearing, so the
        // editor's visible fields (not control internals) determine dirtiness.
        return !IsBlankNewPositionEditor();
    }

    private bool EnsurePositionEditorCommitted()
    {
        if (!HasUnappliedPositionChanges())
            return true;

        ViewModel.SetStatusMessage(
            "The position editor has unapplied changes. Click Position Save or Discard first.");
        return false;
    }

    private void UpdatePositionSaveState()
    {
        if (_loadingPositionEditor)
            return;

        var quantityValid = TryParsePositionDecimal(PositionQtyTextBox.Text, out var qty);
        var priceValid = TryParsePositionDecimal(PositionUnitPriceTextBox.Text, out var price);
        var discountValid = TryParsePositionDecimal(PositionDiscountTextBox.Text, out var discount);
        var isValid = InvoiceRules.TryValidatePosition(ArticleAutoComplete.Text ?? string.Empty,
            quantityValid ? qty : null, PositionUnitTextBox.Text ?? string.Empty,
            priceValid ? price : null, discountValid ? discount : null, out _);

        PositionSaveButton.IsEnabled = isValid
            && (_editingPosition is null || !PositionMatchesCurrent());
    }

    private void ClearPositionEditor()
    {
        _editingPosition = null;
        _loadingPositionEditor = true;
        try
        {
            _positionSourceArticle = null;
            _pendingExplicitArticle = null;
            _highlightedArticleSuggestion = null;
            ArticleAutoComplete.IsDropDownOpen = false;
            ArticleAutoComplete.SelectedItem = null;
            ViewModel.SelectedArticle = null;
            ArticleAutoComplete.Text = string.Empty;
            PositionQtyTextBox.Value = null;
            PositionQtyTextBox.Text = string.Empty;
            PositionUnitTextBox.Text = string.Empty;
            PositionUnitPriceTextBox.Text = string.Empty;
            PositionDiscountTextBox.Text = string.Empty;
            PositionDescriptionTextBox.Text = string.Empty;
            PositionAmountText.Text = string.Empty;
            PositionSaveButton.IsEnabled = false;
        }
        finally
        {
            _loadingPositionEditor = false;
        }
        ScheduleBlankPositionNormalization();
    }

    private void ScheduleBlankPositionNormalization()
    {
        if (_blankNormalizationQueued)
            return;
        _blankNormalizationQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _blankNormalizationQueued = false;
            if (!IsBlankNewPositionEditor())
                return;

            _loadingPositionEditor = true;
            try
            {
                ArticleAutoComplete.SelectedItem = null;
                ArticleAutoComplete.IsDropDownOpen = false;
                ViewModel.SelectedArticle = null;
                ArticleAutoComplete.Text = string.Empty;
                PositionQtyTextBox.Value = null;
                PositionQtyTextBox.Text = string.Empty;
                PositionAmountText.Text = string.Empty;
                PositionSaveButton.IsEnabled = false;
                _positionSourceArticle = null;
            }
            finally
            {
                _loadingPositionEditor = false;
            }
        }, DispatcherPriority.Background);
    }

    private void LoadPositionEditor(InvoiceItemRowViewModel item)
    {
        _loadingPositionEditor = true;
        _editingPosition = item;
        try
        {
            var article = item.SourceArticleId is int id
                ? ViewModel.Articles.FirstOrDefault(candidate => candidate.Id == id)
                : null;

            _positionSourceArticle = article;
            ViewModel.SelectedArticle = article;
            ArticleAutoComplete.SelectedItem = article;
            // The document snapshot takes precedence over current master data.
            ArticleAutoComplete.Text = item.ArticleName;
            PositionQtyTextBox.Value = item.Quantity;
            PositionQtyTextBox.Text = item.QuantityText;
            PositionUnitTextBox.Text = item.Unit;
            PositionUnitPriceTextBox.Text = item.UnitPriceText;
            PositionDiscountTextBox.Text = item.DiscountText;
            PositionDescriptionTextBox.Text = item.Description ?? string.Empty;
            PositionAmountText.Text = item.LineTotal.ToString("N2", CultureInfo.InvariantCulture);
        }
        finally
        {
            _loadingPositionEditor = false;
        }
        UpdatePositionSaveState();
    }

    private async void NewPosition_Click(object? sender, RoutedEventArgs e)
    {
        if (HasUnappliedPositionChanges())
        {
            var discard = await ConfirmationDialog.ShowAsync(
                this, "Discard Position Changes",
                "The current position has unapplied changes. Start a new position and discard them?",
                "Yes", "No");
            if (!discard)
                return;
        }

        ViewModel.SelectedItem = null;
        ClearPositionEditor();
    }

    private void DiscardPosition_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectedItem = null;
        ClearPositionEditor();
    }

    private async void PositionsGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var selected = ViewModel.SelectedItem;
        if (selected is null || ReferenceEquals(_editingPosition, selected))
            return;

        if (HasUnappliedPositionChanges())
        {
            var discard = await ConfirmationDialog.ShowAsync(
                this, "Discard Position Changes",
                "The current position has unapplied changes. Discard them and edit the selected row?",
                "Yes", "No");
            if (!discard)
                return;
        }

        LoadPositionEditor(selected);
    }

    private void SavePositionEdit_Click(object? sender, RoutedEventArgs e)
    {
        var name = (ArticleAutoComplete.Text ?? string.Empty).Trim();
        var unit = (PositionUnitTextBox.Text ?? string.Empty).Trim();
        var description = PositionDescriptionTextBox.Text;

        var quantityValid = TryParsePositionDecimal(PositionQtyTextBox.Text, out var qty);
        var priceValid = TryParsePositionDecimal(PositionUnitPriceTextBox.Text, out var price);
        var discountValid = TryParsePositionDecimal(PositionDiscountTextBox.Text, out var discount);
        if (!InvoiceRules.TryValidatePosition(name, quantityValid ? qty : null, unit,
            priceValid ? price : null, discountValid ? discount : null, out var error))
        {
            ViewModel.SetStatusMessage(error);
            return;
        }

        if (_editingPosition is null)
        {
            var source = _positionSourceArticle;
            var item = new InvoiceItemRowViewModel(
                name, description,
                source is null ? null : source.Specs_EN ?? source.Specification,
                source?.Id, ViewModel.Currency, ViewModel.ExchangeRateSnapshot,
                qty, unit, price, discount);
            ViewModel.AddPosition(item);
        }
        else
        {
            var target = _editingPosition;
            if (_positionSourceArticle is Article selectedMaster
                && target.SourceArticleId != selectedMaster.Id)
            {
                // An explicit product switch replaces the product specification,
                // not merely the label and price. Simple renaming does neither.
                target.Specification = selectedMaster.Specs_EN ?? selectedMaster.Specification;
            }
            target.SetSourceArticle(_positionSourceArticle?.Id ?? target.SourceArticleId);
            target.ArticleName = name;
            target.QuantityText = DecimalText(qty);
            target.Unit = unit;
            target.UnitPriceText = DecimalText(price);
            target.DiscountText = DecimalText(discount);
            target.Description = description;
            ViewModel.SelectedItem = target;
            ViewModel.SetStatusMessage($"Position '{name}' updated. Save the document to persist it.");
        }

        ClearPositionEditor();
    }

    private async void DeletePosition_Click(object? sender, RoutedEventArgs e)
    {
        if (HasUnappliedPositionChanges())
        {
            var discard = await ConfirmationDialog.ShowAsync(
                this, "Discard Position Changes",
                "Discard the pending position changes and delete the selected row?",
                "Yes", "No");
            if (!discard)
                return;
        }

        var item = ViewModel.SelectedItem ?? _editingPosition;
        if (item is null)
        {
            ViewModel.SetStatusMessage("Select an invoice item first.");
            return;
        }
        ViewModel.RemovePosition(item);
        ClearPositionEditor();
    }

    private void PositionsGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        SelectLineGridSupport.ApplyAlternateRow(e);
        e.Row.Header = (e.Row.Index + 1).ToString(CultureInfo.InvariantCulture);
    }
}
