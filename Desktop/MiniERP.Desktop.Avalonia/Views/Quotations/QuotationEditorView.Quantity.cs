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
using MiniERP.Desktop.ViewModels.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private bool _articleLookupConfigured;
    private bool _blankPositionNormalizationScheduled;
    private Control? _articleSuggestionPointerRoot;
    private Article? _pendingExplicitArticle;
    private Article? _positionSourceArticle;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

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

        // Typing is only search/edit input. Merely making the text equal to an Article name
        // must never import that Article into the position editor.
        ArticleAutoComplete.IsTextCompletionEnabled = false;
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

        // Stop the original SelectedArticle-driven workflow. Article data is imported only by
        // CommitExplicitArticleSelection, which is called from a real suggestion click, Enter
        // on an open suggestion list, or the explicit "..." picker.
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ArticleAutoComplete.SelectionChanged += ArticleAutoComplete_SelectionChangedStable;
        ArticleAutoComplete.DropDownOpened += ArticleAutoComplete_DropDownOpened;
        ArticleAutoComplete.KeyDown += ArticleAutoComplete_KeyDown;

        ArticleAutoComplete.TextChanged -= PositionEditor_TextChanged;
        ArticleAutoComplete.TextChanged += ArticleAutoComplete_TextChangedStable;

        PositionUnitTextBox.TextChanged += PositionEditor_VisibleFieldChangedStable;
        PositionUnitPriceTextBox.TextChanged += PositionEditor_VisibleFieldChangedStable;
        PositionDiscountTextBox.TextChanged += PositionEditor_VisibleFieldChangedStable;
        PositionDescriptionTextBox.TextChanged += PositionEditor_VisibleFieldChangedStable;

        PositionSaveButton.Click -= SavePositionEdit_Click;
        PositionSaveButton.Click += SavePositionEditStable_Click;

        PositionsGrid.DoubleTapped -= PositionsGrid_DoubleTapped;
        PositionsGrid.DoubleTapped += PositionsGrid_DoubleTappedStable;

        // Replace the Article picker button as well, otherwise setting SelectedArticle from
        // the old picker path would no longer have an explicit commit point.
        if (ArticleAutoComplete.GetLogicalParent() is Grid articleLookupGrid)
        {
            var pickerButton = articleLookupGrid.Children
                .OfType<Button>()
                .FirstOrDefault(button => !ReferenceEquals(button, ArticleAutoComplete));
            if (pickerButton is not null)
            {
                pickerButton.Click -= PickArticle_Click;
                pickerButton.Click += PickArticleStable_Click;
            }
        }

        // The document Save command used the legacy hidden-control dirty check. Replace only
        // that toolbar handler so a genuinely blank Position editor cannot block quotation Save.
        var documentSaveButton = this.GetLogicalDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => !ReferenceEquals(button, PositionSaveButton)
                && button.GetLogicalDescendants()
                    .OfType<TextBlock>()
                    .Any(text => string.Equals(text.Text, "Save", StringComparison.Ordinal)));
        if (documentSaveButton is not null)
        {
            documentSaveButton.Click -= Save_Click;
            documentSaveButton.Click += SaveDocumentStable_Click;
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
                    InputElement.PointerPressedEvent,
                    ArticleSuggestion_PointerPressed);
            }

            _articleSuggestionPointerRoot = pointerRoot;
            _articleSuggestionPointerRoot.AddHandler(
                InputElement.PointerPressedEvent,
                ArticleSuggestion_PointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
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

        // Let AutoCompleteBox finish its own click/selection bookkeeping first. We then seed
        // the editor exactly once from the Article the user actually clicked.
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_pendingExplicitArticle, article))
                return;

            _pendingExplicitArticle = null;
            CommitExplicitArticleSelection(article);
        });
    }

    private void ArticleAutoComplete_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !ArticleAutoComplete.IsDropDownOpen)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (ArticleAutoComplete.SelectedItem is Article article)
                CommitExplicitArticleSelection(article);
        });
    }

    private void ArticleAutoComplete_SelectionChangedStable(object? sender, SelectionChangedEventArgs e)
    {
        var article = ArticleAutoComplete.SelectedItem as Article;

        if (_loadingPositionEditor)
        {
            ViewModel.SelectedArticle = article;
            return;
        }

        if (article is null)
        {
            ViewModel.SelectedArticle = null;

            if (_editingPosition is null && IsVisuallyBlankNewPositionEditor())
            {
                _positionSourceArticle = null;
                ScheduleBlankPositionNormalization();
            }

            UpdateStablePositionSaveState();
            return;
        }

        // IMPORTANT: SelectionChanged alone is never permission to overwrite the editor.
        // Avalonia can select an exact text match while the user is merely typing. Explicit
        // mouse/keyboard/picker paths call CommitExplicitArticleSelection separately.
        if (!ReferenceEquals(_pendingExplicitArticle, article))
        {
            var textToPreserve = ArticleAutoComplete.Text ?? string.Empty;
            var sourceToPreserve = _positionSourceArticle;

            _loadingPositionEditor = true;
            try
            {
                ArticleAutoComplete.SelectedItem = null;
                ViewModel.SelectedArticle = null;
                _positionSourceArticle = sourceToPreserve;
                ArticleAutoComplete.Text = textToPreserve;
                ArticleAutoComplete.CaretIndex = textToPreserve.Length;
            }
            finally
            {
                _loadingPositionEditor = false;
            }

            UpdateStablePositionSaveState();
        }
    }

    private async void PickArticleStable_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var picker = new ArticlePickerWindow(ViewModel.Articles, _positionSourceArticle?.Id);
        var selected = await picker.ShowDialog<Article?>(owner);
        if (selected is not null)
            CommitExplicitArticleSelection(selected);
    }

    private void CommitExplicitArticleSelection(Article article)
    {
        _pendingExplicitArticle = null;
        _positionSourceArticle = article;

        _loadingPositionEditor = true;
        try
        {
            ViewModel.SelectedArticle = article;
            ArticleAutoComplete.SelectedItem = article;
        }
        finally
        {
            _loadingPositionEditor = false;
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

        // Qty and Amount are excluded for a new blank editor: Qty is a NumericUpDown with a
        // separate internal Value and Amount is derived. Neither should create phantom edits.
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
            // Preserve a customized line name. Loading an existing position must never force
            // the current database Name_EN back over the stored quotation line.
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
            if (_editingPosition is not null || !IsVisuallyBlankNewPositionEditor())
            {
                _blankPositionNormalizationScheduled = false;
                return;
            }

            ForceBlankPositionEditorState();
            _blankPositionNormalizationScheduled = false;
        });
    }

    private void ForceBlankPositionEditorState()
    {
        _loadingPositionEditor = true;
        try
        {
            _pendingExplicitArticle = null;
            _positionSourceArticle = null;
            ViewModel.SelectedArticle = null;
            ArticleAutoComplete.IsDropDownOpen = false;
            ArticleAutoComplete.SelectedItem = null;
            ArticleAutoComplete.Text = string.Empty;
            ArticleAutoComplete.CaretIndex = 0;

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
            // intact. A completely manual line uses a temporary Article only to create the row.
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

        // The visible editor is authoritative. Never replace a manually adjusted line name
        // with the database Article name during Position Save.
        target.ArticleName = articleName;
        target.QuantityText = quantityText;
        target.Unit = unit;
        target.UnitPriceText = unitPriceText;
        target.DiscountText = discountText;
        target.Description = description;

        RenumberPositions();
        ViewModel.SelectedItem = target;
        ClearPositionEditor();
        ForceBlankPositionEditorState();
        ScheduleBlankPositionNormalization();

        ViewModel.SetStatusMessage(isNewPosition
            ? $"Position '{target.ArticleName}' added to the overview. Save the document to persist it."
            : $"Position '{target.ArticleName}' updated in the overview. Save the document to persist it.");
    }

    private async void SaveDocumentStable_Click(object? sender, RoutedEventArgs e)
    {
        if (HasMeaningfulPositionEditorChanges())
        {
            ViewModel.SetStatusMessage("The position editor has unapplied changes. Click Position Save or Discard first.");
            return;
        }

        // Normalize any stale AutoCompleteBox/NumericUpDown state before saving the document.
        if (_editingPosition is null && IsVisuallyBlankNewPositionEditor())
            ForceBlankPositionEditorState();

        if (!await ViewModel.SaveAsync())
            return;

        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
