namespace MiniERP.Domain
{
    public class CustomerContact : AuditableEntity
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public required string Name { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

    }
}
