namespace MiniERP.Domain
{
    public class Customer : AuditableEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<CustomerContact> Contacts { get; set; } = new List<CustomerContact>();
    }
}
