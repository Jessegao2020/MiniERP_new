using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using MiniERP.Domain;
using MiniERP.ApplicationLayer.Services;

namespace MiniERP.Desktop.ViewModels.Quotations;

public sealed class QuotationItemRowViewModel : INotifyPropertyChanged
{
    private int _positionNumber;
    private string _articleName;
    private string? _description;
    private string? _specification;
    private string _quantityText;
    private string _unit;
    private string _unitPriceText;
    private string _discountText;
    private string _currency;
    private decimal _exchangeRateSnapshot;

    public int Id { get; }
    public int? SourceArticleId { get; private set; }

    public string Currency
    {
        get => _currency;
        private set => SetField(ref _currency, value);
    }

    public decimal ExchangeRateSnapshot
    {
        get => _exchangeRateSnapshot;
        private set => SetField(ref _exchangeRateSnapshot, value);
    }

    // UI-only sequence number. It is recalculated from the current collection order,
    // so moving a position changes 1/2/3 rather than carrying the old number with the item.
    public int PositionNumber
    {
        get => _positionNumber;
        internal set => SetField(ref _positionNumber, value);
    }

    public string ArticleName
    {
        get => _articleName;
        set => SetField(ref _articleName, value ?? string.Empty);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public string? Specification
    {
        get => _specification;
        set => SetField(ref _specification, value);
    }

    public string QuantityText
    {
        get => _quantityText;
        set => SetDecimalText(ref _quantityText, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetField(ref _unit, value ?? string.Empty);
    }

    public string UnitPriceText
    {
        get => _unitPriceText;
        set => SetDecimalText(ref _unitPriceText, value);
    }

    public string DiscountText
    {
        get => _discountText;
        set => SetDecimalText(ref _discountText, value);
    }

    public decimal Quantity => ParseDecimal(QuantityText);
    public decimal UnitPrice => ParseDecimal(UnitPriceText);
    public decimal DiscountPercent => ParseDecimal(DiscountText);

    public decimal LineTotal
        => QuotationItem.CalculateLineTotal(Quantity, UnitPrice, DiscountPercent);

    public QuotationItemRowViewModel(QuotationItem source)
    {
        Id = source.Id;
        SourceArticleId = source.SourceArticleId;
        _currency = source.Currency;
        _exchangeRateSnapshot = source.ExchangeRateSnapshot;
        _articleName = source.ArticleName;
        _description = source.Description;
        _specification = source.Specification;
        _quantityText = ToText(source.Quantity);
        _unit = string.IsNullOrWhiteSpace(source.Unit) ? "PCS" : source.Unit;
        _unitPriceText = ToText(source.UnitPrice);
        _discountText = ToText(source.DiscountPercent);
    }

    public QuotationItemRowViewModel(Article source, string currency, decimal exchangeRateSnapshot, decimal unitPrice)
        : this(QuotationRules.CreateItemSnapshot(source, currency, exchangeRateSnapshot, unitPrice))
    {
    }

    public void SetSourceArticle(int? sourceArticleId)
    {
        if (SourceArticleId == sourceArticleId) return;
        SourceArticleId = sourceArticleId;
        OnPropertyChanged(nameof(SourceArticleId));
    }

    public void Reprice(string currency, decimal exchangeRateSnapshot, decimal unitPrice)
    {
        Currency = currency;
        ExchangeRateSnapshot = exchangeRateSnapshot;
        UnitPriceText = ToText(unitPrice);
    }

    public bool TryValidate(out string error)
        => QuotationRules.TryValidatePosition(ArticleName, Quantity, Unit, UnitPrice, DiscountPercent, out error);

    public QuotationItem ToEntity(int sortOrder) => new()
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
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder();
        var hasSeparator = false;

        foreach (var character in value)
        {
            if (char.IsDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if ((character == '.' || character == ',') && !hasSeparator)
            {
                builder.Append('.');
                hasSeparator = true;
            }
        }

        if (builder.Length > 0 && builder[0] == '.')
            builder.Insert(0, '0');

        return builder.ToString();
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;

    private static string ToText(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
