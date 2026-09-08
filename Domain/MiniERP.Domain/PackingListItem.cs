namespace MiniERP.Domain;

public class PackingListItem : AuditableEntity
{
    public int Id { get; set; }
    public int PackingListId { get; set; }
    public PackingList? PackingList { get; set; }
    public int SortOrder { get; set; }
    public int? SourceArticleId { get; set; }
    public string ArticleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specification { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public string Unit { get; set; } = "PCS";
}
