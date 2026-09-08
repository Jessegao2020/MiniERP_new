using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain
{
    public class Quotation : AuditableEntity
    {
        public int Id { get; set; }
        public string QuotationNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string? DeliveryTerm { get; set; }
        public string? LeadTime { get; set; }
        public string? PaymentTerm { get; set; }
        public string? Remarks { get; set; }
        public DateTime QuotationDate { get; set; } = DateTime.Now;
        public DateTime? ValidUntil { get; set; }

        // Currency and exchange rate are snapshots belonging to this quotation.
        // ExchangeRate means CNY per 1 USD.
        public string Currency { get; set; } = "USD";
        public decimal ExchangeRate { get; set; } = 1m;

        // The legacy quotation template presents three independent quantity/price tiers.
        public string Tier1Label { get; set; } = "100 Sets";
        public string Tier2Label { get; set; } = "500 Sets";
        public string Tier3Label { get; set; } = "1000 Sets";

        // Commercial-document snapshots. Historical PDFs must not change when a
        // customer, contact or salesperson record is edited later.
        public string? CustomerNameSnapshot { get; set; }
        public string? CustomerAddressSnapshot { get; set; }
        public string? CustomerContactSnapshot { get; set; }
        public string? SalesContactNameSnapshot { get; set; }
        public string? SalesContactPhoneSnapshot { get; set; }
        public string? SalesContactEmailSnapshot { get; set; }

        public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();

        [NotMapped]
        public decimal Tier1Total => Items.Sum(item => item.LineTotal);

        [NotMapped]
        public decimal Tier2Total => Items.Sum(item => item.LineTotal2);

        [NotMapped]
        public decimal Tier3Total => Items.Sum(item => item.LineTotal3);

        // Kept for list screens and older code; this is the first quotation tier.
        [NotMapped]
        public decimal TotalAmount => Tier1Total;
    }
}
