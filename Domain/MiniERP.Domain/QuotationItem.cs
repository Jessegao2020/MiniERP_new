using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain;

public class QuotationItem : AuditableEntity
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public Quotation? Quotation { get; set; }

    // This is deliberately not a foreign key. The quotation must remain valid even
    // if the source Article is later removed from the catalogue.
    public int? SourceArticleId { get; set; }

    // Snapshot fields. Editing the Article later must not rewrite history.
    public string ArticleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specification { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRateSnapshot { get; set; } = 1m;

    [NotMapped]
    public decimal LineTotal
        => decimal.Round(Quantity * UnitPrice * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
}
