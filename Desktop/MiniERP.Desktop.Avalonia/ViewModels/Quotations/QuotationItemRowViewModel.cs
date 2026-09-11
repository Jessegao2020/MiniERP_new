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
    private string _quantityText;
    private string _unit;
    private string _unitPriceText;
    private string _discountText;

    public int Id { get; }
    public int? SourceArticleId { get; private set; }
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
        => decimal.Round(Quantity * UnitPrice * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

    public QuotationItemRowViewModel(QuotationItem source)
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

    public QuotationItemRowViewModel(Article source, string currency, decimal exchangeRateSnapshot, decimal unitPrice)
    {
        SourceArticleId = source.Id;
        Currency = currency;
        ExchangeRateSnapshot = exchangeRateSnapshot;
        _articleName = FirstNonEmpty(source.Name_EN, source.Name) ?? $"Article {source.Id}";

        // Quotation template rule: the small text printed below the bold product
        // name is specifically Article.Description_EN. Do not fall back to Chinese.
        _description = source.Description_EN;
        _specification = FirstNonEmpty(source.Specs_EN, source.Specification);
        _quantityText = "1";
        _unit = "PCS";
        _unitPriceText = ToText(unitPrice);
        _discountText = "0";
    }

    public void SetSourceArticle(int? sourceArticleId)
    {
        if (SourceArticleId == sourceArticleId) return;
        SourceArticleId = sourceArticleId;
        OnPropertyChanged(nameof(SourceArticleId));
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(ArticleName))
        {
            error = "Every quotation item needs an article name.";
            return false;
        }

        if (Quantity <= 0)
        {
            error = $"Quantity for '{ArticleName}' must be greater than zero.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Unit))
        {
            error = $"Unit for '{ArticleName}' is required.";
            return false;
        }

        if (UnitPrice < 0)
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

    private static string? FirstNonEmpty(string? preferred, string? fallback)
        => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
