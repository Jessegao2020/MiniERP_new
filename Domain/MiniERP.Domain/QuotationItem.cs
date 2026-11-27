using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain;

public class QuotationItem : AuditableEntity
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public Quotation? Quotation { get; set; }

    // Persistent display order inside a quotation. This controls editor order
    // and exported document order independently of database primary keys.
    public int SortOrder { get; set; }

    // This is deliberately not a foreign key. The quotation must remain valid even
    // if the source Article is later removed from the catalogue.
    public int? SourceArticleId { get; set; }

    // Snapshot fields. Editing the Article later must not rewrite history.
    public string ArticleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specification { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public string Unit { get; set; } = "PCS";
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRateSnapshot { get; set; } = 1m;

    [NotMapped]
    public decimal LineTotal
        => CalculateLineTotal(Quantity, UnitPrice, DiscountPercent);

    public static decimal CalculateLineTotal(decimal quantity, decimal unitPrice, decimal discountPercent)
        => decimal.Round(quantity * unitPrice * (1m - discountPercent / 100m), 2, MidpointRounding.AwayFromZero);
}
