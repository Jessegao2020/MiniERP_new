using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain;

public class Contract : AuditableEntity
{
    public int Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateTime ContractDate { get; set; } = DateTime.Now;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1m;
    public string? DeliveryTerm { get; set; }
    public string? PaymentTerm { get; set; }
    public string? LeadTime { get; set; }
    public string? BankInformation { get; set; }
    public string? Remarks { get; set; }
    public string? TermsAndConditions { get; set; }

    public string? CustomerPoNumber { get; set; }
    public DateTime? CustomerPoDate { get; set; }

    public string? CustomerNameSnapshot { get; set; }
    public string? CustomerAddressSnapshot { get; set; }
    public string? CustomerContactSnapshot { get; set; }
    public string? SalesContactNameSnapshot { get; set; }
    public string? SalesContactPhoneSnapshot { get; set; }
    public string? SalesContactEmailSnapshot { get; set; }

    public DocumentSourceType SourceDocumentType { get; set; } = DocumentSourceType.None;
    public int? SourceDocumentId { get; set; }
    public string? SourceDocumentNumber { get; set; }

    public ICollection<ContractItem> Items { get; set; } = new List<ContractItem>();

    [NotMapped]
    public decimal TotalAmount => Items.Sum(item => item.LineTotal);
}
