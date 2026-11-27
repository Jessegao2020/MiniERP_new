namespace MiniERP.ApplicationLayer.Services;

/// <summary>Deployment/company quotation defaults. A host supplies policy; clients supply business input.</summary>
public sealed record QuotationPolicy
{
    public string NumberCode { get; init; } = "INTPAN";
    public string DefaultCurrency { get; init; } = "USD";
    public int DefaultValidityDays { get; init; } = 30;
    public string DeliveryTerm { get; init; } = "FCA Baoding";
    public string PaymentTerm { get; init; } = "100% prepayment";
    public string LeadTime { get; init; } = "2 business days";
    public decimal? CnyPerUsd { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(NumberCode) || NumberCode.Length > 20 ||
            !NumberCode.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("Quotation number code must contain 1-20 ASCII letters or digits.");
        if (!QuotationRules.IsSupportedCurrency(DefaultCurrency))
            throw new ArgumentException("Default quotation currency must be USD or CNY.");
        if (DefaultValidityDays is < 1 or > 3650)
            throw new ArgumentException("Default quotation validity must be between 1 and 3650 days.");
        if (CnyPerUsd is <= 0)
            throw new ArgumentException("CNY per USD must be positive when configured.");
    }
}
