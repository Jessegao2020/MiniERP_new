using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain;

public class PackingList : AuditableEntity
{
    public int Id { get; set; }
    public string PackingListNumber { get; set; } = string.Empty;
    public DateTime PackingDate { get; set; } = DateTime.Now;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public string? DeliveryTerm { get; set; }
    public string? Remarks { get; set; }
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

    public ICollection<PackingListItem> Items { get; set; } = new List<PackingListItem>();
    public ICollection<PackingPackage> Packages { get; set; } = new List<PackingPackage>();

    [NotMapped]
    public decimal TotalQuantity => Items.Sum(item => item.Quantity);

    [NotMapped]
    public int TotalCartons => Packages.Sum(package => Math.Max(1, package.PackageCount));

    [NotMapped]
    public decimal TotalNetWeight => Packages.Sum(package => package.NetWeightKg * Math.Max(1, package.PackageCount));

    [NotMapped]
    public decimal TotalGrossWeight => Packages.Sum(package => package.GrossWeightKg * Math.Max(1, package.PackageCount));

    [NotMapped]
    public decimal TotalCbm => Packages.Sum(package => package.Cbm * Math.Max(1, package.PackageCount));
}
