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
using MiniERP.Desktop.ViewModels.PackingLists;
using MiniERP.Desktop.Views.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.PackingLists;

public partial class PackingListEditorView
{
    private PackingListItemRowViewModel? _editingGoods;
    private PackingPackageRowViewModel? _editingPackage;
    private Article? _goodsSourceArticle;
    private Article? _pendingGoodsSuggestion;
    private Article? _highlightedGoodsSuggestion;
    private Control? _goodsSuggestionPointerRoot;
    private bool _loadingGoods;
    private bool _loadingPackage;
    private bool _goodsLookupReady;

    private void InitializePackingEditors()
    {
        GoodsGrid.RowHeaderWidth = 42;
        PackGrid.RowHeaderWidth = 42;
        ClearGoodsEditor();
        ClearPackageEditor();
        NormalizePickerButtons();
    }

    private void NormalizePickerButtons()
    {
        foreach (var button in this.GetLogicalDescendants().OfType<Button>()
                     .Where(button => button.Classes.Contains("lookup-button")))
        {
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Stretch;
            button.CornerRadius = new CornerRadius(0);
            button.MinHeight = 0;
        }
    }

    private void ConfigurePackingEditors()
    {
        if (_goodsLookupReady)
            return;
        _goodsLookupReady = true;
        GoodsArticleAutoComplete.IsTextCompletionEnabled = false;
        GoodsArticleAutoComplete.ValueMemberBinding = new Binding(nameof(Article.QuotationName));
        GoodsArticleAutoComplete.ItemFilter = (search, item) =>
            item is Article article && (string.IsNullOrWhiteSpace(search)
                || (article.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (article.Name_EN?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));

        GoodsArticleAutoComplete.SelectionChanged += GoodsArticle_SelectionChanged;
        GoodsArticleAutoComplete.DropDownOpened += GoodsArticle_DropDownOpened;
        GoodsArticleAutoComplete.KeyDown += GoodsArticle_KeyDown;
        // The Packages tab starts inactive, so its spinner template is built
        // later. Apply the same compact arrows when either editor is displayed.
        GoodsQtyTextBox.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(ApplyPackingSpinners, DispatcherPriority.Loaded);
        PackCountTextBox.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(ApplyPackingSpinners, DispatcherPriority.Loaded);
        ApplyPackingSpinners();
        Dispatcher.UIThread.Post(ApplyPackingSpinners, DispatcherPriority.Render);
        UpdateGoodsSaveState();
        UpdatePackSaveState();
    }

    private void ApplyPackingSpinners()
    {
        foreach (var control in new[] { GoodsQtyTextBox, PackCountTextBox })
        {
            var spinner = control.GetVisualDescendants().OfType<StackPanel>()
                .FirstOrDefault(panel => panel.Name == "PART_SpinnerPanel");
            if (spinner is not null)
            {
                spinner.Orientation = Orientation.Vertical;
                spinner.Width = 18;
            }
            foreach (var button in control.GetVisualDescendants().OfType<RepeatButton>())
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
    }

    private void GoodsArticle_DropDownOpened(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var root = GoodsArticleAutoComplete.GetVisualDescendants()
                .OfType<Popup>().FirstOrDefault()?.Child;
            if (root is null || ReferenceEquals(root, _goodsSuggestionPointerRoot))
                return;

            if (_goodsSuggestionPointerRoot is not null)
                _goodsSuggestionPointerRoot.RemoveHandler(
                    InputElement.PointerPressedEvent, GoodsSuggestion_PointerPressed);

            _goodsSuggestionPointerRoot = root;
            root.AddHandler(InputElement.PointerPressedEvent, GoodsSuggestion_PointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        });
    }

    private void GoodsSuggestion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!GoodsArticleAutoComplete.IsDropDownOpen || e.Source is not Visual source)
            return;
        var article = (source as StyledElement)?.DataContext as Article
            ?? source.GetVisualAncestors().OfType<StyledElement>()
                .Select(x => x.DataContext).OfType<Article>().FirstOrDefault();
        if (article is null)
            return;

        _pendingGoodsSuggestion = article;
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_pendingGoodsSuggestion, article))
                return;
            ApplyGoodsArticle(article);
        });
    }

    private void GoodsArticle_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !GoodsArticleAutoComplete.IsDropDownOpen)
            return;
        var article = GoodsArticleAutoComplete.SelectedItem as Article ?? _highlightedGoodsSuggestion;
        if (article is not null)
            Dispatcher.UIThread.Post(() => ApplyGoodsArticle(article));
    }

    private void GoodsArticle_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingGoods || DataContext is not PackingListEditorViewModel)
            return;
        if (GoodsArticleAutoComplete.SelectedItem is Article article
            && !ReferenceEquals(article, _pendingGoodsSuggestion))
        {
            if (GoodsArticleAutoComplete.IsDropDownOpen)
                _highlightedGoodsSuggestion = article;

            var text = GoodsArticleAutoComplete.Text ?? string.Empty;
            _loadingGoods = true;
            try
            {
                GoodsArticleAutoComplete.SelectedItem = null;
                ViewModel.SelectedArticle = null;
                GoodsArticleAutoComplete.Text = text;
            }
            finally { _loadingGoods = false; }
        }
        UpdateGoodsSaveState();
    }

    private async void PickGoodsArticle_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;
        var picker = new ArticlePickerWindow(ViewModel.Articles, _goodsSourceArticle?.Id);
        var article = await picker.ShowDialog<Article?>(owner);
        if (article is not null)
            ApplyGoodsArticle(article);
    }

    private void ApplyGoodsArticle(Article article)
    {
        _pendingGoodsSuggestion = null;
        _goodsSourceArticle = article;
        _loadingGoods = true;
        try
        {
            ViewModel.SelectedArticle = article;
            GoodsArticleAutoComplete.SelectedItem = article;
            GoodsArticleAutoComplete.Text = article.QuotationName;
            GoodsQtyTextBox.Value = 1m;
            GoodsQtyTextBox.Text = "1";
            GoodsUnitTextBox.Text = "PCS";
            GoodsDescriptionTextBox.Text = article.Description_EN ?? string.Empty;
        }
        finally { _loadingGoods = false; }
        UpdateGoodsSaveState();
    }

    private static bool ParseNumber(string? text, out decimal number)
        => decimal.TryParse((text ?? string.Empty).Trim().Replace(',', '.'),
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out number);

    private static string DecimalText(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    private void GoodsField_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loadingGoods || DataContext is not PackingListEditorViewModel)
            return;
        if (ReferenceEquals(sender, GoodsArticleAutoComplete))
            _highlightedGoodsSuggestion = null;
        UpdateGoodsSaveState();
    }

    private void GoodsQty_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!_loadingGoods && DataContext is PackingListEditorViewModel)
            UpdateGoodsSaveState();
    }

    private bool GoodsEditorIsBlank()
        => _editingGoods is null && string.IsNullOrWhiteSpace(GoodsArticleAutoComplete.Text)
            && string.IsNullOrWhiteSpace(GoodsUnitTextBox.Text)
            && string.IsNullOrWhiteSpace(GoodsDescriptionTextBox.Text);

    private bool GoodsEditorMatches()
        => _editingGoods is not null
            && (_goodsSourceArticle?.Id ?? _editingGoods.SourceArticleId) == _editingGoods.SourceArticleId
            && string.Equals((GoodsArticleAutoComplete.Text ?? "").Trim(), _editingGoods.ArticleName, StringComparison.Ordinal)
            && ParseNumber(GoodsQtyTextBox.Text, out var qty) && qty == _editingGoods.Quantity
            && string.Equals(GoodsUnitTextBox.Text ?? "", _editingGoods.Unit, StringComparison.Ordinal)
            && string.Equals(GoodsDescriptionTextBox.Text ?? "", _editingGoods.Description ?? "", StringComparison.Ordinal);

    private bool GoodsEditorDirty() => _editingGoods is not null ? !GoodsEditorMatches() : !GoodsEditorIsBlank();

    private void UpdateGoodsSaveState()
    {
        if (_loadingGoods)
            return;
        GoodsSaveButton.IsEnabled =
            !string.IsNullOrWhiteSpace(GoodsArticleAutoComplete.Text)
            && !string.IsNullOrWhiteSpace(GoodsUnitTextBox.Text)
            && ParseNumber(GoodsQtyTextBox.Text, out var qty) && qty > 0
            && (_editingGoods is null || !GoodsEditorMatches());
    }

    private void ClearGoodsEditor()
    {
        _editingGoods = null;
        _loadingGoods = true;
        try
        {
            _goodsSourceArticle = null;
            _pendingGoodsSuggestion = null;
            _highlightedGoodsSuggestion = null;
            GoodsArticleAutoComplete.IsDropDownOpen = false;
            GoodsArticleAutoComplete.SelectedItem = null;
            if (DataContext is PackingListEditorViewModel vm)
                vm.SelectedArticle = null;
            GoodsArticleAutoComplete.Text = "";
            GoodsQtyTextBox.Value = null;
            GoodsQtyTextBox.Text = "";
            GoodsUnitTextBox.Text = "";
            GoodsDescriptionTextBox.Text = "";
            GoodsSaveButton.IsEnabled = false;
        }
        finally { _loadingGoods = false; }
        Dispatcher.UIThread.Post(() =>
        {
            if (GoodsEditorIsBlank())
            {
                _loadingGoods = true;
                try
                {
                    GoodsArticleAutoComplete.SelectedItem = null;
                    GoodsArticleAutoComplete.Text = "";
                    GoodsQtyTextBox.Value = null;
                    GoodsQtyTextBox.Text = "";
                }
                finally { _loadingGoods = false; }
            }
        }, DispatcherPriority.Background);
    }

    private void LoadGoodsEditor(PackingListItemRowViewModel row)
    {
        _editingGoods = row;
        _loadingGoods = true;
        try
        {
            _goodsSourceArticle = row.SourceArticleId is int id
                ? ViewModel.Articles.FirstOrDefault(article => article.Id == id)
                : null;
            ViewModel.SelectedArticle = _goodsSourceArticle;
            GoodsArticleAutoComplete.SelectedItem = _goodsSourceArticle;
            GoodsArticleAutoComplete.Text = row.ArticleName;
            GoodsQtyTextBox.Value = row.Quantity;
            GoodsQtyTextBox.Text = row.QuantityText;
            GoodsUnitTextBox.Text = row.Unit;
            GoodsDescriptionTextBox.Text = row.Description ?? "";
        }
        finally { _loadingGoods = false; }
        UpdateGoodsSaveState();
    }

    private async void NewGoods_Click(object? sender, RoutedEventArgs e)
    {
        if (GoodsEditorDirty() && !await ConfirmationDialog.ShowAsync(this,
                "Discard Goods Changes", "Discard the current goods edit and start a new one?", "Yes", "No"))
            return;
        ViewModel.SelectedItem = null;
        ClearGoodsEditor();
    }

    private void DiscardGoods_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectedItem = null;
        ClearGoodsEditor();
    }

    private async void GoodsGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var row = ViewModel.SelectedItem;
        if (row is null || ReferenceEquals(row, _editingGoods))
            return;
        if (GoodsEditorDirty() && !await ConfirmationDialog.ShowAsync(this,
                "Discard Goods Changes", "Discard the current unsaved goods edit?", "Yes", "No"))
            return;
        LoadGoodsEditor(row);
    }

    private void SaveGoods_Click(object? sender, RoutedEventArgs e)
    {
        var name = (GoodsArticleAutoComplete.Text ?? "").Trim();
        var unit = (GoodsUnitTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit)
            || !ParseNumber(GoodsQtyTextBox.Text, out var qty) || qty <= 0)
        {
            ViewModel.SetStatusMessage("Goods article, positive quantity and unit are required.");
            return;
        }

        if (_editingGoods is null)
        {
            var src = _goodsSourceArticle;
            ViewModel.AddGoodsPosition(new PackingListItemRowViewModel(name,
                GoodsDescriptionTextBox.Text,
                src?.Specs_EN ?? src?.Specification,
                src?.Id, qty, unit));
        }
        else
        {
            var row = _editingGoods;
            if (_goodsSourceArticle is Article master && row.SourceArticleId != master.Id)
                row.Specification = master.Specs_EN ?? master.Specification;
            row.SetSourceArticle(_goodsSourceArticle?.Id ?? row.SourceArticleId);
            row.ArticleName = name;
            row.Description = GoodsDescriptionTextBox.Text;
            row.QuantityText = DecimalText(qty);
            row.Unit = unit;
            ViewModel.SelectedItem = row;
        }
        ClearGoodsEditor();
    }

    private async void DeleteGoods_Click(object? sender, RoutedEventArgs e)
    {
        if (GoodsEditorDirty() && !await ConfirmationDialog.ShowAsync(this,
                "Discard Goods Changes", "Discard the pending goods edit and delete the selected row?", "Yes", "No"))
            return;
        var row = ViewModel.SelectedItem ?? _editingGoods;
        if (row is null) { ViewModel.SetStatusMessage("Select a packing-list item first."); return; }
        ViewModel.RemoveGoodsPosition(row);
        ClearGoodsEditor();
    }

    private void GoodsGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        SelectLineGridSupport.ApplyAlternateRow(e);
        e.Row.Header = (e.Row.Index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private void MoveGoodsUp_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePackingEditorsCommitted()) return;
        MoveItemUp_Click(sender, e);
    }

    private void MoveGoodsDown_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePackingEditorsCommitted()) return;
        MoveItemDown_Click(sender, e);
    }

    private void PackField_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loadingPackage || DataContext is not PackingListEditorViewModel)
            return;
        UpdatePackageCbm();
        UpdatePackSaveState();
    }

    private void PackCount_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!_loadingPackage && DataContext is PackingListEditorViewModel)
            UpdatePackSaveState();
    }

    private bool PackEditorIsBlank()
        => _editingPackage is null
            && new[] { PackCartonTextBox.Text, PackContentsTextBox.Text, PackLengthTextBox.Text,
                PackWidthTextBox.Text, PackHeightTextBox.Text, PackNetTextBox.Text, PackGrossTextBox.Text }
                .All(string.IsNullOrWhiteSpace);

    private bool PackEditorMatches()
        => _editingPackage is not null
            && string.Equals((PackCartonTextBox.Text ?? "").Trim(), _editingPackage.CartonNumber, StringComparison.Ordinal)
            && int.TryParse(PackCountTextBox.Text, out var count) && count == _editingPackage.PackageCount
            && string.Equals(PackContentsTextBox.Text ?? "", _editingPackage.Contents ?? "", StringComparison.Ordinal)
            && EquivalentNumber(PackLengthTextBox.Text, _editingPackage.LengthCm)
            && EquivalentNumber(PackWidthTextBox.Text, _editingPackage.WidthCm)
            && EquivalentNumber(PackHeightTextBox.Text, _editingPackage.HeightCm)
            && EquivalentNumber(PackNetTextBox.Text, _editingPackage.NetWeightKg)
            && EquivalentNumber(PackGrossTextBox.Text, _editingPackage.GrossWeightKg);

    private static bool EquivalentNumber(string? text, decimal value)
        => ParseNumber(text, out var n) && n == value;

    private bool PackEditorDirty() => _editingPackage is not null ? !PackEditorMatches() : !PackEditorIsBlank();

    private void UpdatePackSaveState()
    {
        if (_loadingPackage)
            return;
        PackSaveButton.IsEnabled = !string.IsNullOrWhiteSpace(PackCartonTextBox.Text)
            && int.TryParse(PackCountTextBox.Text, out var count) && count > 0
            && (_editingPackage is null || !PackEditorMatches());
    }

    private void UpdatePackageCbm()
    {
        if (ParseNumber(PackLengthTextBox.Text, out var l)
            && ParseNumber(PackWidthTextBox.Text, out var w)
            && ParseNumber(PackHeightTextBox.Text, out var h))
        {
            PackCbmTextBox.Text = decimal.Round(l * w * h / 1_000_000m,
                4, MidpointRounding.AwayFromZero).ToString("0.####", CultureInfo.InvariantCulture);
        }
        else PackCbmTextBox.Text = "";
    }

    private void ClearPackageEditor()
    {
        _editingPackage = null;
        _loadingPackage = true;
        try
        {
            PackCartonTextBox.Text = "";
            PackCountTextBox.Value = null;
            PackCountTextBox.Text = "";
            PackContentsTextBox.Text = "";
            PackLengthTextBox.Text = "";
            PackWidthTextBox.Text = "";
            PackHeightTextBox.Text = "";
            PackNetTextBox.Text = "";
            PackGrossTextBox.Text = "";
            PackCbmTextBox.Text = "";
            PackSaveButton.IsEnabled = false;
        }
        finally { _loadingPackage = false; }
    }

    private void LoadPackageEditor(PackingPackageRowViewModel row)
    {
        _editingPackage = row;
        _loadingPackage = true;
        try
        {
            PackCartonTextBox.Text = row.CartonNumber;
            PackCountTextBox.Value = row.PackageCount;
            PackCountTextBox.Text = row.PackageCountText;
            PackContentsTextBox.Text = row.Contents ?? "";
            PackLengthTextBox.Text = row.LengthText;
            PackWidthTextBox.Text = row.WidthText;
            PackHeightTextBox.Text = row.HeightText;
            PackNetTextBox.Text = row.NetWeightText;
            PackGrossTextBox.Text = row.GrossWeightText;
            PackCbmTextBox.Text = row.Cbm.ToString("0.####", CultureInfo.InvariantCulture);
        }
        finally { _loadingPackage = false; }
        UpdatePackSaveState();
    }

    private async void NewPack_Click(object? sender, RoutedEventArgs e)
    {
        if (PackEditorDirty() && !await ConfirmationDialog.ShowAsync(this,
                "Discard Package Changes", "Discard unsaved package edits and start a new carton?", "Yes", "No"))
            return;
        ViewModel.SelectedPackage = null;
        ClearPackageEditor();
    }

    private void DiscardPack_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectedPackage = null;
        ClearPackageEditor();
    }

    private async void PackGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var row = ViewModel.SelectedPackage;
        if (row is null || ReferenceEquals(row, _editingPackage))
            return;
        if (PackEditorDirty() && !await ConfirmationDialog.ShowAsync(this,
                "Discard Package Changes", "Discard unsaved package changes?", "Yes", "No"))
            return;
        LoadPackageEditor(row);
    }

    private void SavePack_Click(object? sender, RoutedEventArgs e)
    {
        var carton = (PackCartonTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(carton)
            || !int.TryParse(PackCountTextBox.Text, out var count) || count <= 0)
        {
            ViewModel.SetStatusMessage("Carton number and positive package count are required.");
            return;
        }
        var controls = new[] { PackLengthTextBox, PackWidthTextBox, PackHeightTextBox,
            PackNetTextBox, PackGrossTextBox };
        var values = new decimal[controls.Length];
        for (var i = 0; i < controls.Length; i++)
        {
            var text = controls[i].Text;
            if (!string.IsNullOrWhiteSpace(text) && !ParseNumber(text, out values[i]))
            {
                ViewModel.SetStatusMessage("Dimensions and weights must be valid non-negative numbers.");
                return;
            }
            if (values[i] < 0)
            {
                ViewModel.SetStatusMessage("Dimensions and weights cannot be negative.");
                return;
            }
        }

        var row = _editingPackage ?? new PackingPackageRowViewModel();
        row.CartonNumber = carton;
        row.PackageCountText = count.ToString(CultureInfo.InvariantCulture);
        row.Contents = PackContentsTextBox.Text;
        row.LengthText = DecimalText(values[0]);
        row.WidthText = DecimalText(values[1]);
        row.HeightText = DecimalText(values[2]);
        row.NetWeightText = DecimalText(values[3]);
        row.GrossWeightText = DecimalText(values[4]);

        if (_editingPackage is null)
            ViewModel.AddPackagePosition(row);
        else
            ViewModel.SelectedPackage = row;

        ClearPackageEditor();
    }

    private async void DeletePack_Click(object? sender, RoutedEventArgs e)
    {
        if (PackEditorDirty() && !await ConfirmationDialog.ShowAsync(this,
                "Discard Package Changes", "Discard pending package edits and delete the selected row?", "Yes", "No"))
            return;
        var row = ViewModel.SelectedPackage ?? _editingPackage;
        if (row is null) { ViewModel.SetStatusMessage("Select a package row first."); return; }
        ViewModel.RemovePackagePosition(row);
        ClearPackageEditor();
    }

    private void PackGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        SelectLineGridSupport.ApplyAlternateRow(e);
        e.Row.Header = (e.Row.Index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private void MovePackUp_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePackingEditorsCommitted()) return;
        MovePackageUp_Click(sender, e);
    }

    private void MovePackDown_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePackingEditorsCommitted()) return;
        MovePackageDown_Click(sender, e);
    }

    private bool EnsurePackingEditorsCommitted()
    {
        if (!GoodsEditorDirty() && !PackEditorDirty())
            return true;
        ViewModel.SetStatusMessage(
            "The goods or package editor has unapplied changes. Click its Save or Discard first.");
        return false;
    }
}
