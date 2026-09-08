using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain;

public class InvoiceItem : AuditableEntity
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int SortOrder { get; set; }
    public int? SourceArticleId { get; set; }
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
        => decimal.Round(Quantity * UnitPrice * (1m - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
}
