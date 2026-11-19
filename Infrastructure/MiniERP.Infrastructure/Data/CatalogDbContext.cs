using Microsoft.EntityFrameworkCore;
using MiniERP.Domain;

namespace MiniERP.Infrastructure.Data;

/// <summary>
/// The shared product catalogue has its own SQLite database and migration history.
/// Sales documents retain independent product snapshots in ApplicationDbContext.
/// </summary>
public sealed class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Article>().ToTable("Articles");
    }
}
