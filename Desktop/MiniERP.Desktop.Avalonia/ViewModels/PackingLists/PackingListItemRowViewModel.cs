using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.PackingLists;

public sealed class PackingListItemRowViewModel : INotifyPropertyChanged
{
    private string _articleName;
    private string? _description;
    private string? _specification;
    private string _quantityText;
    private string _unit;

    public int Id { get; }
    public int? SourceArticleId { get; }
    public string ArticleName { get => _articleName; set => SetField(ref _articleName, value ?? string.Empty); }
    public string? Description { get => _description; set => SetField(ref _description, value); }
    public string? Specification { get => _specification; set => SetField(ref _specification, value); }
    public string QuantityText { get => _quantityText; set => SetDecimalText(ref _quantityText, value); }
    public string Unit { get => _unit; set => SetField(ref _unit, value ?? string.Empty); }
    public decimal Quantity => ParseDecimal(QuantityText);

    public PackingListItemRowViewModel(PackingListItem source)
    {
        Id = source.Id;
        SourceArticleId = source.SourceArticleId;
        _articleName = source.ArticleName ?? string.Empty;
        _description = source.Description;
        _specification = source.Specification;
        _quantityText = ToText(source.Quantity);
        _unit = string.IsNullOrWhiteSpace(source.Unit) ? "PCS" : source.Unit;
    }

    public PackingListItemRowViewModel(Article source)
    {
        SourceArticleId = source.Id;
        _articleName = !string.IsNullOrWhiteSpace(source.Name_EN)
            ? source.Name_EN
            : !string.IsNullOrWhiteSpace(source.Name)
                ? source.Name
                : $"Article {source.Id}";
        _description = source.Description_EN;
        _specification = !string.IsNullOrWhiteSpace(source.Specs_EN) ? source.Specs_EN : source.Specification;
        _quantityText = "1";
        _unit = "PCS";
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(ArticleName)) { error = "Every packing-list item needs an article name."; return false; }
        if (Quantity <= 0) { error = $"Quantity for '{ArticleName}' must be greater than zero."; return false; }
        if (string.IsNullOrWhiteSpace(Unit)) { error = $"Unit for '{ArticleName}' is required."; return false; }
        error = string.Empty;
        return true;
    }

    public PackingListItem ToEntity(int sortOrder) => new()
    {
        Id = Id,
        SourceArticleId = SourceArticleId,
        ArticleName = ArticleName.Trim(),
        Description = Description,
        Specification = Specification,
        Quantity = Quantity,
        Unit = Unit.Trim().ToUpperInvariant(),
        SortOrder = sortOrder
    };

    private void SetDecimalText(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = NormalizeDecimalInput(value);
        if (field == normalized) return;
        field = normalized;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(Quantity));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private static string NormalizeDecimalInput(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var builder = new StringBuilder();
        var hasSeparator = false;
        foreach (var c in value)
        {
            if (char.IsDigit(c)) { builder.Append(c); continue; }
            if ((c == '.' || c == ',') && !hasSeparator) { builder.Append('.'); hasSeparator = true; }
        }
        if (builder.Length > 0 && builder[0] == '.') builder.Insert(0, '0');
        return builder.ToString();
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    private static string ToText(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
