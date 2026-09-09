using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories;

public sealed class ContractRepository : Repository<Contract>, IContractRepository
{
    public ContractRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<IEnumerable<Contract>> GetAllAsync()
        => await Query().OrderByDescending(c => c.ContractDate).ThenByDescending(c => c.Id).ToListAsync();

    public override async Task<Contract?> GetByIdAsync(int id)
        => await Query().FirstOrDefaultAsync(c => c.Id == id);

    public Task<Contract?> GetByNumberAsync(string contractNumber)
        => Query().FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);

    public async Task<IEnumerable<Contract>> GetByCustomerIdAsync(int customerId)
        => await Query().Where(c => c.CustomerId == customerId).OrderByDescending(c => c.ContractDate).ToListAsync();

    public override async Task UpdateAsync(Contract contract)
    {
        var existing = await _dbSet.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == contract.Id)
            ?? throw new InvalidOperationException($"Contract {contract.Id} no longer exists.");

        CopyHeader(contract, existing);
        SyncItems(contract, existing);
        await _context.SaveChangesAsync();
    }

    public override async Task DeleteAsync(int id)
    {
        var hasDownstreamDocuments =
            await _context.Invoices.AnyAsync(invoice =>
                invoice.SourceDocumentType == DocumentSourceType.Contract && invoice.SourceDocumentId == id) ||
            await _context.PackingLists.AnyAsync(packingList =>
                packingList.SourceDocumentType == DocumentSourceType.Contract && packingList.SourceDocumentId == id);

        if (hasDownstreamDocuments)
            throw new InvalidOperationException("This contract has downstream sales documents and cannot be deleted.");

        await base.DeleteAsync(id);
    }

    private IQueryable<Contract> Query()
        => _dbSet.AsNoTracking().Include(c => c.Customer).Include(c => c.User).Include(c => c.Items);

    private void SyncItems(Contract source, Contract target)
    {
        var incoming = source.Items.ToList();
        var incomingIds = incoming.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();

        foreach (var old in target.Items.Where(i => !incomingIds.Contains(i.Id)).ToList())
            _context.ContractItems.Remove(old);

        foreach (var item in incoming)
        {
            var tracked = item.Id > 0 ? target.Items.FirstOrDefault(i => i.Id == item.Id) : null;
            if (tracked is null)
            {
                target.Items.Add(CloneItem(item));
                continue;
            }

            CopyItem(item, tracked);
            tracked.LastModifiedAt = DateTime.Now;
        }
    }

    private static void CopyHeader(Contract source, Contract target)
    {
        target.ContractNumber = source.ContractNumber;
        target.ContractDate = source.ContractDate;
        target.CustomerId = source.CustomerId;
        target.UserId = source.UserId;
        target.Currency = source.Currency;
        target.ExchangeRate = source.ExchangeRate;
        target.DeliveryTerm = source.DeliveryTerm;
        target.PaymentTerm = source.PaymentTerm;
        target.LeadTime = source.LeadTime;
        target.BankInformation = source.BankInformation;
        target.Remarks = source.Remarks;
        target.TermsAndConditions = source.TermsAndConditions;
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

    private static ContractItem CloneItem(ContractItem source)
        => new()
        {
            SortOrder = source.SortOrder,
            SourceArticleId = source.SourceArticleId,
            ArticleName = source.ArticleName,
            Description = source.Description,
            Specification = source.Specification,
            Quantity = source.Quantity,
            Unit = source.Unit,
            UnitPrice = source.UnitPrice,
            DiscountPercent = source.DiscountPercent,
            Currency = source.Currency,
            ExchangeRateSnapshot = source.ExchangeRateSnapshot,
            CreatedBy = source.CreatedBy,
            CreatedAt = source.CreatedAt ?? DateTime.Now,
            LastModifiedBy = source.LastModifiedBy,
            LastModifiedAt = DateTime.Now
        };

    private static void CopyItem(ContractItem source, ContractItem target)
    {
        target.SortOrder = source.SortOrder;
        target.SourceArticleId = source.SourceArticleId;
        target.ArticleName = source.ArticleName;
        target.Description = source.Description;
        target.Specification = source.Specification;
        target.Quantity = source.Quantity;
        target.Unit = source.Unit;
        target.UnitPrice = source.UnitPrice;
        target.DiscountPercent = source.DiscountPercent;
        target.Currency = source.Currency;
        target.ExchangeRateSnapshot = source.ExchangeRateSnapshot;
        target.LastModifiedBy = source.LastModifiedBy;
    }
}
