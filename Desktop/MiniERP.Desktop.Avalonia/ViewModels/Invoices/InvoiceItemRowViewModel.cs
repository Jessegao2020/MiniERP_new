using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using MiniERP.Domain;
using MiniERP.ApplicationLayer.Services;

namespace MiniERP.Desktop.ViewModels.Invoices;

public sealed class InvoiceItemRowViewModel : INotifyPropertyChanged
{
    private string _articleName;
    private string? _description;
    private string? _specification;
    private string _quantityText;
    private string _unit;
    private string _unitPriceText;
    private string _discountText;

    public int Id { get; private set; }
    internal void AcceptSavedIdentity(int id) => Id = id;
    public int? SourceArticleId { get; private set; }

    public void SetSourceArticle(int? sourceArticleId) => SourceArticleId = sourceArticleId;
    public string Currency { get; }
    public decimal ExchangeRateSnapshot { get; }

    public string ArticleName { get => _articleName; set => SetField(ref _articleName, value ?? string.Empty); }
    public string? Description { get => _description; set => SetField(ref _description, value); }
    public string? Specification { get => _specification; set => SetField(ref _specification, value); }
    public string QuantityText { get => _quantityText; set => SetDecimalText(ref _quantityText, value); }
    public string Unit { get => _unit; set => SetField(ref _unit, value ?? string.Empty); }
    public string UnitPriceText { get => _unitPriceText; set => SetDecimalText(ref _unitPriceText, value); }
    public string DiscountText { get => _discountText; set => SetDecimalText(ref _discountText, value); }

    public decimal Quantity => ParseDecimal(QuantityText);
    public decimal UnitPrice => ParseDecimal(UnitPriceText);
    public decimal DiscountPercent => ParseDecimal(DiscountText);
    public decimal LineTotal => SalesLineAmounts.Calculate(Quantity, UnitPrice, DiscountPercent);

    public InvoiceItemRowViewModel(InvoiceItem source)
    {
        Id = source.Id;
        SourceArticleId = source.SourceArticleId;
        Currency = source.Currency;
        ExchangeRateSnapshot = source.ExchangeRateSnapshot;
        _articleName = source.ArticleName;
        _description = source.Description;
        _specification = source.Specification;
        _quantityText = ToText(source.Quantity);
        _unit = string.IsNullOrWhiteSpace(source.Unit) ? "PCS" : source.Unit;
        _unitPriceText = ToText(source.UnitPrice);
        _discountText = ToText(source.DiscountPercent);
    }

    public InvoiceItemRowViewModel(Article source, string currency, decimal exchangeRateSnapshot, decimal unitPrice)
        : this(InvoiceRules.CreateItemSnapshot(source, currency, exchangeRateSnapshot, unitPrice)) { }

    // Used by the quotation-style position editor. No master Article is required:
    // manual invoice lines preserve the operator's visible text and price snapshot.
    public InvoiceItemRowViewModel(
        string articleName, string? description, string? specification, int? sourceArticleId,
        string currency, decimal exchangeRateSnapshot,
        decimal quantity, string unit, decimal unitPrice, decimal discountPercent)
    {
        SourceArticleId = sourceArticleId;
        Currency = currency;
        ExchangeRateSnapshot = exchangeRateSnapshot;
        _articleName = articleName;
        _description = description;
        _specification = specification;
        _quantityText = ToText(quantity);
        _unit = unit;
        _unitPriceText = ToText(unitPrice);
        _discountText = ToText(discountPercent);
    }

    public bool TryValidate(out string error)
        => InvoiceRules.TryValidatePosition(ArticleName, Quantity, Unit, UnitPrice, DiscountPercent, out error);

    public InvoiceItem ToEntity(int sortOrder) => new()
    {
        Id = Id,
        SourceArticleId = SourceArticleId,
        ArticleName = ArticleName.Trim(),
        Description = Description,
        Specification = Specification,
        Quantity = Quantity,
        Unit = Unit.Trim().ToUpperInvariant(),
        UnitPrice = UnitPrice,
        DiscountPercent = DiscountPercent,
        Currency = Currency,
        ExchangeRateSnapshot = ExchangeRateSnapshot,
        SortOrder = sortOrder
    };

    private void SetDecimalText(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = NormalizeDecimalInput(value);
        if (field == normalized) return;
        field = normalized;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(LineTotal));
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
    private static string? FirstNonEmpty(string? preferred, string? fallback) => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
