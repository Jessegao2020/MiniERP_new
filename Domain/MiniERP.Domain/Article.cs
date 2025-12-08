namespace MiniERP.Domain
{
    public class Article : AuditableEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public decimal? MinimumPrice { get; set; }
        public string? Description { get; set; }
        public string? Specification { get; set; }
        public string? Discount { get; set; }
        public string? Note { get; set; }
        public string? Specs_EN { get; set; }
        public string? Category { get; set; }
        public string? Name_EN { get; set; }
        public string? Description_EN { get; set; }
    }
}
