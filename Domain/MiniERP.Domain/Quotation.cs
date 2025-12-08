namespace MiniERP.Domain
{
    public class Quotation
    {
        public int Id { get; set; }
        public string QuotationNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string UserId { get; set; }
        public User? User { get; set; }
        public string? DeliveryTerm { get; set; }
        public string? LeadTime { get; set; }
        public string? PaymentTerm  { get; set; }
        public string? Remarks { get; set; }
        public DateTime QuotationDate { get; set; } = DateTime.Now;
        public DateTime? ValidUntil { get; set; }
        public string? CratedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        
        // 报价明细（如果需要，可以创建QuotationItem实体）
        // public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
    }
}
