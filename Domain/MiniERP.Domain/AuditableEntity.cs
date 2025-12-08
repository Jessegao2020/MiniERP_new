using System.ComponentModel.DataAnnotations.Schema;

namespace MiniERP.Domain
{
    public abstract class AuditableEntity
    {
        public string? CreatedByUserName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? LastModifiedByUserName { get; set; }
        public DateTime? LastModifiedAt { get; set; } = DateTime.Now;
    }
}
