using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories;

public sealed class PackingListRepository : Repository<PackingList>, IPackingListRepository
{
    public PackingListRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<IEnumerable<PackingList>> GetAllAsync()
        => await Query().OrderByDescending(p => p.PackingDate).ThenByDescending(p => p.Id).ToListAsync();

    public override async Task<PackingList?> GetByIdAsync(int id)
        => await Query().FirstOrDefaultAsync(p => p.Id == id);

    public Task<PackingList?> GetByNumberAsync(string packingListNumber)
        => Query().FirstOrDefaultAsync(p => p.PackingListNumber == packingListNumber);

    public async Task<IEnumerable<PackingList>> GetByCustomerIdAsync(int customerId)
        => await Query().Where(p => p.CustomerId == customerId).OrderByDescending(p => p.PackingDate).ToListAsync();

    public override async Task UpdateAsync(PackingList packingList)
    {
        var existing = await _dbSet
            .Include(p => p.Items)
            .Include(p => p.Packages)
            .FirstOrDefaultAsync(p => p.Id == packingList.Id)
            ?? throw new InvalidOperationException($"Packing list {packingList.Id} no longer exists.");

        CopyHeader(packingList, existing);
        SyncItems(packingList, existing);
        SyncPackages(packingList, existing);
        await _context.SaveChangesAsync();
    }

    private IQueryable<PackingList> Query()
        => _dbSet.AsNoTracking().Include(p => p.Customer).Include(p => p.User).Include(p => p.Items).Include(p => p.Packages);

    private void SyncItems(PackingList source, PackingList target)
    {
        var incoming = source.Items.ToList();
        var incomingIds = incoming.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
        foreach (var old in target.Items.Where(i => !incomingIds.Contains(i.Id)).ToList())
            _context.PackingListItems.Remove(old);

        foreach (var item in incoming)
        {
            var tracked = item.Id > 0 ? target.Items.FirstOrDefault(i => i.Id == item.Id) : null;
            if (tracked is null) target.Items.Add(CloneItem(item));
            else CopyItem(item, tracked);
        }
    }

    private void SyncPackages(PackingList source, PackingList target)
    {
        var incoming = source.Packages.ToList();
        var incomingIds = incoming.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
        foreach (var old in target.Packages.Where(i => !incomingIds.Contains(i.Id)).ToList())
            _context.PackingPackages.Remove(old);

        foreach (var package in incoming)
        {
            var tracked = package.Id > 0 ? target.Packages.FirstOrDefault(i => i.Id == package.Id) : null;
            if (tracked is null) target.Packages.Add(ClonePackage(package));
            else CopyPackage(package, tracked);
        }
    }

    private static void CopyHeader(PackingList source, PackingList target)
    {
        target.PackingListNumber = source.PackingListNumber;
        target.PackingDate = source.PackingDate;
        target.CustomerId = source.CustomerId;
        target.UserId = source.UserId;
        target.DeliveryTerm = source.DeliveryTerm;
        target.Remarks = source.Remarks;
        target.CustomerPoNumber = source.CustomerPoNumber;
        target.CustomerPoDate = source.CustomerPoDate;
        target.CustomerNameSnapshot = source.CustomerNameSnapshot;
        target.CustomerAddressSnapshot = source.CustomerAddressSnapshot;
        target.CustomerContactSnapshot = source.CustomerContactSnapshot;
        target.SalesContactNameSnapshot = source.SalesContactNameSnapshot;
        target.SalesContactPhoneSnapshot = source.SalesContactPhoneSnapshot;
        target.SalesContactEmailSnapshot = source.SalesContactEmailSnapshot;
        target.SourceDocumentType = source.SourceDocumentType;
        target.SourceDocumentId = source.SourceDocumentId;
        target.SourceDocumentNumber = source.SourceDocumentNumber;
        target.LastModifiedBy = source.LastModifiedBy;
        target.LastModifiedAt = DateTime.Now;
    }

    private static PackingListItem CloneItem(PackingListItem source)
        => new()
        {
            SortOrder = source.SortOrder,
            SourceArticleId = source.SourceArticleId,
            ArticleName = source.ArticleName,
            Description = source.Description,
            Specification = source.Specification,
            Quantity = source.Quantity,
            Unit = source.Unit,
            CreatedBy = source.CreatedBy,
            CreatedAt = source.CreatedAt ?? DateTime.Now,
            LastModifiedBy = source.LastModifiedBy,
            LastModifiedAt = DateTime.Now
        };

    private static void CopyItem(PackingListItem source, PackingListItem target)
    {
        target.SortOrder = source.SortOrder;
        target.SourceArticleId = source.SourceArticleId;
        target.ArticleName = source.ArticleName;
        target.Description = source.Description;
        target.Specification = source.Specification;
        target.Quantity = source.Quantity;
        target.Unit = source.Unit;
        target.LastModifiedBy = source.LastModifiedBy;
        target.LastModifiedAt = DateTime.Now;
    }

    private static PackingPackage ClonePackage(PackingPackage source)
        => new()
        {
            SortOrder = source.SortOrder,
            CartonNumber = source.CartonNumber,
            PackageCount = source.PackageCount,
            Contents = source.Contents,
            LengthCm = source.LengthCm,
            WidthCm = source.WidthCm,
            HeightCm = source.HeightCm,
            NetWeightKg = source.NetWeightKg,
            GrossWeightKg = source.GrossWeightKg,
            CreatedBy = source.CreatedBy,
            CreatedAt = source.CreatedAt ?? DateTime.Now,
            LastModifiedBy = source.LastModifiedBy,
            LastModifiedAt = DateTime.Now
        };

    private static void CopyPackage(PackingPackage source, PackingPackage target)
    {
        target.SortOrder = source.SortOrder;
        target.CartonNumber = source.CartonNumber;
        target.PackageCount = source.PackageCount;
        target.Contents = source.Contents;
        target.LengthCm = source.LengthCm;
        target.WidthCm = source.WidthCm;
        target.HeightCm = source.HeightCm;
        target.NetWeightKg = source.NetWeightKg;
        target.GrossWeightKg = source.GrossWeightKg;
        target.LastModifiedBy = source.LastModifiedBy;
        target.LastModifiedAt = DateTime.Now;
    }
}
