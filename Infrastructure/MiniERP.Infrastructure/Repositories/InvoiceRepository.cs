using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories;

public sealed class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<IEnumerable<Invoice>> GetAllAsync()
        => await Query().OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).ToListAsync();

    public override async Task<Invoice?> GetByIdAsync(int id)
        => await Query().FirstOrDefaultAsync(i => i.Id == id);

    public Task<Invoice?> GetByNumberAsync(string invoiceNumber)
        => Query().FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);

    public async Task<IEnumerable<Invoice>> GetByTypeAsync(InvoiceType type)
        => await Query().Where(i => i.Type == type).OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).ToListAsync();

    public async Task<IEnumerable<Invoice>> GetByCustomerIdAsync(int customerId)
        => await Query().Where(i => i.CustomerId == customerId).OrderByDescending(i => i.InvoiceDate).ToListAsync();

    public override async Task UpdateAsync(Invoice invoice)
    {
        var existing = await _dbSet.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == invoice.Id)
            ?? throw new InvalidOperationException($"Invoice {invoice.Id} no longer exists.");

        CopyHeader(invoice, existing);
        SyncItems(invoice, existing);
        await _context.SaveChangesAsync();
    }

    public override async Task DeleteAsync(int id)
    {
        var isUsedAsInvoiceSource =
            await _context.Invoices.AnyAsync(invoice =>
                invoice.SourceDocumentId == id &&
                (invoice.SourceDocumentType == DocumentSourceType.ProformaInvoice ||
                 invoice.SourceDocumentType == DocumentSourceType.CommercialInvoice)) ||
            await _context.PackingLists.AnyAsync(packingList =>
                packingList.SourceDocumentId == id &&
                (packingList.SourceDocumentType == DocumentSourceType.ProformaInvoice ||
                 packingList.SourceDocumentType == DocumentSourceType.CommercialInvoice)) ||
            await _context.Contracts.AnyAsync(contract =>
                contract.SourceDocumentId == id &&
                (contract.SourceDocumentType == DocumentSourceType.ProformaInvoice ||
                 contract.SourceDocumentType == DocumentSourceType.CommercialInvoice));

        if (isUsedAsInvoiceSource)
            throw new InvalidOperationException("This invoice has downstream sales documents and cannot be deleted.");

        await base.DeleteAsync(id);
    }

    private IQueryable<Invoice> Query()
        => _dbSet.AsNoTracking().Include(i => i.Customer).Include(i => i.User).Include(i => i.Items);

    private void SyncItems(Invoice source, Invoice target)
    {
        var incoming = source.Items.ToList();
        var incomingIds = incoming.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();

        foreach (var old in target.Items.Where(i => !incomingIds.Contains(i.Id)).ToList())
            _context.InvoiceItems.Remove(old);

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

    private static void CopyHeader(Invoice source, Invoice target)
    {
        target.Type = source.Type;
        target.InvoiceNumber = source.InvoiceNumber;
        target.InvoiceDate = source.InvoiceDate;
        target.DueDate = source.DueDate;
        target.CustomerId = source.CustomerId;
        target.UserId = source.UserId;
        target.Currency = source.Currency;
        target.ExchangeRate = source.ExchangeRate;
        target.DeliveryTerm = source.DeliveryTerm;
        target.PaymentTerm = source.PaymentTerm;
        target.BankInformation = source.BankInformation;
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

    private static InvoiceItem CloneItem(InvoiceItem source)
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

    private static void CopyItem(InvoiceItem source, InvoiceItem target)
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
