namespace MiniERP.Domain;

public static class SalesLineAmounts
{
    public static decimal Calculate(decimal quantity, decimal unitPrice, decimal discountPercent)
        => decimal.Round(quantity * unitPrice * (1m - discountPercent / 100m), 2, MidpointRounding.AwayFromZero);
}
