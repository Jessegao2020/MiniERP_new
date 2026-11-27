namespace MiniERP.ApplicationLayer.Services;

public sealed record InvoicePolicy
{
    public string ProformaPrefix { get; init; } = "PI";
    public string CommercialPrefix { get; init; } = "INV";
    public string DefaultCurrency { get; init; } = "USD";
    public string DeliveryTerm { get; init; } = "FCA Baoding";
    public string PaymentTerm { get; init; } = "100% prepayment";
    public decimal? CnyPerUsd { get; init; }

    public void Validate()
    {
        foreach (var prefix in new[] { ProformaPrefix, CommercialPrefix })
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 20 || !prefix.All(char.IsAsciiLetterOrDigit))
                throw new ArgumentException("Invoice prefixes must contain 1-20 ASCII letters or digits.");
        if (string.Equals(ProformaPrefix, CommercialPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("PI and commercial invoice prefixes must differ.");
        if (!QuotationRules.IsSupportedCurrency(DefaultCurrency))
            throw new ArgumentException("Default invoice currency must be USD or CNY.");
        if (CnyPerUsd is <= 0)
            throw new ArgumentException("CNY per USD must be positive when configured.");
    }
}
