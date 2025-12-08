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
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Article配置
            modelBuilder.Entity<Article>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("Articles");

                // Decimal字段配置
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinimumPrice).HasColumnType("decimal(18,2)");

            });

            // Customer配置
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            });

            modelBuilder.Entity<CustomerContact>(entity =>
            {
                entity.HasKey(e => e.Id);

            });

            // Quotation配置
            modelBuilder.Entity<Quotation>(entity =>
            {
                entity.HasKey(e=>e.QuotationNumber);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }
}

