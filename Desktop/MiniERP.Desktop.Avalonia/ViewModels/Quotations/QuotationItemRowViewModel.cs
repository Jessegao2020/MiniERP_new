using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Quotations;

public sealed class QuotationItemRowViewModel : INotifyPropertyChanged
{
    private string _articleName;
    private string? _description;
    private string? _specification;
    private string _unit;
    private string _quantityText;
    private string _unitPriceText;
    private string _quantity2Text;
    private string _unitPrice2Text;
    private string _quantity3Text;
    private string _unitPrice3Text;
    private string _discountText;

    public int Id { get; }
    public int? SourceArticleId { get; }
    public string Currency { get; }
    public decimal ExchangeRateSnapshot { get; }

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

    public string Unit
    {
        get => _unit;
        set => SetField(ref _unit, value ?? string.Empty);
    }

    public string QuantityText
    {
        get => _quantityText;
        set => SetDecimalText(ref _quantityText, value);
    }

    public string UnitPriceText
    {
        get => _unitPriceText;
        set => SetDecimalText(ref _unitPriceText, value);
    }

    public string Quantity2Text
    {
        get => _quantity2Text;
        set => SetDecimalText(ref _quantity2Text, value);
    }

    public string UnitPrice2Text
    {
        get => _unitPrice2Text;
        set => SetDecimalText(ref _unitPrice2Text, value);
    }

    public string Quantity3Text
    {
        get => _quantity3Text;
        set => SetDecimalText(ref _quantity3Text, value);
    }

    public string UnitPrice3Text
    {
        get => _unitPrice3Text;
        set => SetDecimalText(ref _unitPrice3Text, value);
    }

    public string DiscountText
    {
        get => _discountText;
        set => SetDecimalText(ref _discountText, value);
    }

    public decimal Quantity => ParseDecimal(QuantityText);
    public decimal UnitPrice => ParseDecimal(UnitPriceText);
    public decimal Quantity2 => ParseDecimal(Quantity2Text);
    public decimal UnitPrice2 => ParseDecimal(UnitPrice2Text);
    public decimal Quantity3 => ParseDecimal(Quantity3Text);
    public decimal UnitPrice3 => ParseDecimal(UnitPrice3Text);
    public decimal DiscountPercent => ParseDecimal(DiscountText);

    public decimal NetUnitPrice
        => decimal.Round(UnitPrice * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

    public decimal NetUnitPrice2
        => decimal.Round(UnitPrice2 * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

    public decimal NetUnitPrice3
        => decimal.Round(UnitPrice3 * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

    public decimal LineTotal
        => decimal.Round(Quantity * NetUnitPrice, 2, MidpointRounding.AwayFromZero);

    public decimal LineTotal2
        => decimal.Round(Quantity2 * NetUnitPrice2, 2, MidpointRounding.AwayFromZero);

    public decimal LineTotal3
        => decimal.Round(Quantity3 * NetUnitPrice3, 2, MidpointRounding.AwayFromZero);

    public QuotationItemRowViewModel(QuotationItem source)
    {
        Id = source.Id;
        SourceArticleId = source.SourceArticleId;
        Currency = source.Currency;
        ExchangeRateSnapshot = source.ExchangeRateSnapshot;
        _articleName = source.ArticleName;
        _description = source.Description;
        _specification = source.Specification;
        _unit = string.IsNullOrWhiteSpace(source.Unit) ? "PCS" : source.Unit;
        _quantityText = ToText(source.Quantity);
        _unitPriceText = ToText(source.UnitPrice);
        _quantity2Text = ToText(source.Quantity2);
        _unitPrice2Text = ToText(source.UnitPrice2);
        _quantity3Text = ToText(source.Quantity3);
        _unitPrice3Text = ToText(source.UnitPrice3);
        _discountText = ToText(source.DiscountPercent);
    }

    public QuotationItemRowViewModel(
        Article source,
        string currency,
        decimal exchangeRateSnapshot,
        decimal unitPrice,
        string tier1Label,
        string tier2Label,
        string tier3Label)
    {
        SourceArticleId = source.Id;
        Currency = currency;
        ExchangeRateSnapshot = exchangeRateSnapshot;
        _articleName = FirstNonEmpty(source.Name_EN, source.Name) ?? $"Article {source.Id}";
        _description = FirstNonEmpty(source.Description_EN, source.Description);
        _specification = FirstNonEmpty(source.Specs_EN, source.Specification);
        _unit = "PCS";
        _quantityText = ToText(ParseSuggestedQuantity(tier1Label));
        _unitPriceText = ToText(unitPrice);
        _quantity2Text = ToText(ParseSuggestedQuantity(tier2Label));
        _unitPrice2Text = ToText(unitPrice);
        _quantity3Text = ToText(ParseSuggestedQuantity(tier3Label));
        _unitPrice3Text = ToText(unitPrice);
        _discountText = "0";
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(ArticleName))
        {
            error = "Every quotation item needs an article name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Unit))
        {
            error = $"Unit for '{ArticleName}' is required.";
            return false;
        }

        if (Quantity < 0 || Quantity2 < 0 || Quantity3 < 0)
        {
            error = $"Quantity for '{ArticleName}' cannot be negative.";
            return false;
        }

        if (Quantity <= 0 && Quantity2 <= 0 && Quantity3 <= 0)
        {
            error = $"At least one quotation tier for '{ArticleName}' needs a quantity greater than zero.";
            return false;
        }

        if (UnitPrice < 0 || UnitPrice2 < 0 || UnitPrice3 < 0)
        {
            error = $"Unit price for '{ArticleName}' cannot be negative.";
            return false;
        }

        if (DiscountPercent < 0 || DiscountPercent > 100)
        {
            error = $"Discount for '{ArticleName}' must be between 0 and 100%.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public QuotationItem ToEntity() => new()
    {
        Id = Id,
        SourceArticleId = SourceArticleId,
        ArticleName = ArticleName.Trim(),
        Description = Description,
        Specification = Specification,
        Unit = string.IsNullOrWhiteSpace(Unit) ? "PCS" : Unit.Trim().ToUpperInvariant(),
        Quantity = Quantity,
        UnitPrice = UnitPrice,
        Quantity2 = Quantity2,
        UnitPrice2 = UnitPrice2,
        Quantity3 = Quantity3,
        UnitPrice3 = UnitPrice3,
        DiscountPercent = DiscountPercent,
        Currency = Currency,
        ExchangeRateSnapshot = ExchangeRateSnapshot
    };

    private void SetDecimalText(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = NormalizeDecimalInput(value);
        if (field == normalized) return;

        field = normalized;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(NetUnitPrice));
        OnPropertyChanged(nameof(NetUnitPrice2));
        OnPropertyChanged(nameof(NetUnitPrice3));
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(LineTotal2));
        OnPropertyChanged(nameof(LineTotal3));
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

    private static decimal ParseSuggestedQuantity(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return 1m;

        var builder = new StringBuilder();
        var started = false;
        var hasSeparator = false;

        foreach (var character in label)
        {
            if (char.IsDigit(character))
            {
                builder.Append(character);
                started = true;
                continue;
            }

            if (started && (character == '.' || character == ',') && !hasSeparator)
            {
                builder.Append('.');
                hasSeparator = true;
                continue;
            }

            if (started)
                break;
        }

        var parsed = ParseDecimal(builder.ToString());
        return parsed > 0m ? parsed : 1m;
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;

    private static string ToText(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string? FirstNonEmpty(string? preferred, string? fallback)
        => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
