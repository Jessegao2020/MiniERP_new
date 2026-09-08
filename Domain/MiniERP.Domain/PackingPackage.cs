using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain;

public class PackingPackage : AuditableEntity
{
    public int Id { get; set; }
    public int PackingListId { get; set; }
    public PackingList? PackingList { get; set; }
    public int SortOrder { get; set; }
    public string CartonNumber { get; set; } = string.Empty;
    public int PackageCount { get; set; } = 1;
    public string? Contents { get; set; }
    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal NetWeightKg { get; set; }
    public decimal GrossWeightKg { get; set; }

    [NotMapped]
    public decimal Cbm
        => decimal.Round(LengthCm * WidthCm * HeightCm / 1_000_000m, 4, MidpointRounding.AwayFromZero);
}
