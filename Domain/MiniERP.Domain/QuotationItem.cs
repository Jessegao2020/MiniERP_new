using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain;

public class QuotationItem : AuditableEntity
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public Quotation? Quotation { get; set; }

    // Deliberately not a foreign key. The quotation must remain valid even if
    // the source Article is later removed from the catalogue.
    public int? SourceArticleId { get; set; }

    // Snapshot fields. Editing the Article later must not rewrite history.
    public string ArticleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specification { get; set; }
    public string Unit { get; set; } = "PCS";

    // Tier 1 reuses the original Quantity/UnitPrice columns for compatibility.
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }

    public decimal Quantity2 { get; set; }
    public decimal UnitPrice2 { get; set; }

    public decimal Quantity3 { get; set; }
    public decimal UnitPrice3 { get; set; }

    public decimal DiscountPercent { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRateSnapshot { get; set; } = 1m;

    [NotMapped]
    public decimal NetUnitPrice
        => decimal.Round(UnitPrice * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

    [NotMapped]
    public decimal NetUnitPrice2
        => decimal.Round(UnitPrice2 * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

    [NotMapped]
    public decimal NetUnitPrice3
        => decimal.Round(UnitPrice3 * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

    [NotMapped]
    public decimal LineTotal
        => decimal.Round(Quantity * NetUnitPrice, 2, MidpointRounding.AwayFromZero);

    [NotMapped]
    public decimal LineTotal2
        => decimal.Round(Quantity2 * NetUnitPrice2, 2, MidpointRounding.AwayFromZero);

    [NotMapped]
    public decimal LineTotal3
        => decimal.Round(Quantity3 * NetUnitPrice3, 2, MidpointRounding.AwayFromZero);
}
