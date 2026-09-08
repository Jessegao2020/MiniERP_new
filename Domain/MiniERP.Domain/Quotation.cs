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

        // Commercial document snapshots. A historical quotation must keep the
        // customer/sales contact text that was printed at the time of quotation.
        public string? CustomerNameSnapshot { get; set; }
        public string? CustomerAddressSnapshot { get; set; }
        public string? CustomerContactSnapshot { get; set; }
        public string? SalesContactNameSnapshot { get; set; }
        public string? SalesContactPhoneSnapshot { get; set; }
        public string? SalesContactEmailSnapshot { get; set; }

        public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();

        [NotMapped]
        public decimal TotalAmount => Items.Sum(item => item.LineTotal);
    }
}
