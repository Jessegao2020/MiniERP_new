namespace MiniERP.ApplicationLayer.Services;

public sealed record ContractPolicy
{
    public string NumberPrefix { get; init; } = "CON";
    public string DefaultCurrency { get; init; } = "USD";
    public string DeliveryTerm { get; init; } = "FCA Baoding";
    public string PaymentTerm { get; init; } = "100% prepayment";
    public string LeadTime { get; init; } = "2 business days";
    public decimal? CnyPerUsd { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(NumberPrefix) || NumberPrefix.Length > 20 || !NumberPrefix.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("Contract prefix must contain 1-20 ASCII letters or digits.");
        if (!QuotationRules.IsSupportedCurrency(DefaultCurrency))
            throw new ArgumentException("Default contract currency must be USD or CNY.");
        if (CnyPerUsd is <= 0) throw new ArgumentException("CNY per USD must be positive when configured.");
    }
}
