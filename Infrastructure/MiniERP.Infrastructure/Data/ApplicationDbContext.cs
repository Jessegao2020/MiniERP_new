using Microsoft.EntityFrameworkCore;
using MiniERP.Domain;

namespace MiniERP.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Article> Articles { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerContact> Contacts { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<PackingList> PackingLists { get; set; }
        public DbSet<PackingListItem> PackingListItems { get; set; }
        public DbSet<PackingPackage> PackingPackages { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Invoice>()
                .HasIndex(invoice => invoice.InvoiceNumber)
                .IsUnique();

            modelBuilder.Entity<PackingList>()
                .HasIndex(packingList => packingList.PackingListNumber)
                .IsUnique();

            // Historical sales documents must not disappear when a customer or user is removed.
            // The UI/repositories provide friendly validation and the database enforces the same rule.
            modelBuilder.Entity<Invoice>()
                .HasOne(invoice => invoice.Customer)
                .WithMany()
                .HasForeignKey(invoice => invoice.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(invoice => invoice.User)
                .WithMany()
                .HasForeignKey(invoice => invoice.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PackingList>()
                .HasOne(packingList => packingList.Customer)
                .WithMany()
                .HasForeignKey(packingList => packingList.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PackingList>()
                .HasOne(packingList => packingList.User)
                .WithMany()
                .HasForeignKey(packingList => packingList.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
