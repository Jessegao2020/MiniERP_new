using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories
{
    public class QuotationRepository : Repository<Quotation>, IQuotationRepository
    {
        public QuotationRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task<IEnumerable<Quotation>> GetAllAsync()
            => await _dbSet
                .AsNoTracking()
                .Include(q => q.Customer)
                .Include(q => q.User)
                .Include(q => q.Items)
                .OrderByDescending(q => q.QuotationDate)
                .ThenByDescending(q => q.Id)
                .ToListAsync();

        public override async Task<Quotation?> GetByIdAsync(int id)
            => await _dbSet
                .AsNoTracking()
                .Include(q => q.Customer)
                .Include(q => q.User)
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

        public async Task<Quotation?> GetByNumberAsync(string quotationNumber)
            => await _dbSet
                .AsNoTracking()
                .Include(q => q.Customer)
                .Include(q => q.User)
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.QuotationNumber == quotationNumber);

        public async Task<IEnumerable<Quotation>> GetByCustomerIdAsync(int customerId)
            => await _dbSet
                .AsNoTracking()
                .Include(q => q.Customer)
                .Include(q => q.User)
                .Include(q => q.Items)
                .Where(q => q.CustomerId == customerId)
                .OrderByDescending(q => q.QuotationDate)
                .ToListAsync();

        public override async Task UpdateAsync(Quotation quotation)
        {
            var existing = await _dbSet
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == quotation.Id)
                ?? throw new InvalidOperationException($"Quotation {quotation.Id} no longer exists.");

            existing.QuotationNumber = quotation.QuotationNumber;
            existing.CustomerId = quotation.CustomerId;
            existing.UserId = quotation.UserId;
            existing.DeliveryTerm = quotation.DeliveryTerm;
            existing.LeadTime = quotation.LeadTime;
            existing.PaymentTerm = quotation.PaymentTerm;
            existing.Remarks = quotation.Remarks;
            existing.QuotationDate = quotation.QuotationDate;
            existing.ValidUntil = quotation.ValidUntil;
            existing.Currency = quotation.Currency;
            existing.ExchangeRate = quotation.ExchangeRate;
            existing.CustomerNameSnapshot = quotation.CustomerNameSnapshot;
            existing.CustomerAddressSnapshot = quotation.CustomerAddressSnapshot;
            existing.CustomerContactSnapshot = quotation.CustomerContactSnapshot;
            existing.SalesContactNameSnapshot = quotation.SalesContactNameSnapshot;
            existing.SalesContactPhoneSnapshot = quotation.SalesContactPhoneSnapshot;
            existing.SalesContactEmailSnapshot = quotation.SalesContactEmailSnapshot;
            existing.LastModifiedBy = quotation.LastModifiedBy;
            existing.LastModifiedAt = DateTime.Now;

            var incomingItems = quotation.Items.ToList();
            var incomingIds = incomingItems.Where(item => item.Id > 0).Select(item => item.Id).ToHashSet();

            foreach (var oldItem in existing.Items.Where(item => !incomingIds.Contains(item.Id)).ToList())
                _context.QuotationItems.Remove(oldItem);

            foreach (var incoming in incomingItems)
            {
                var tracked = incoming.Id > 0
                    ? existing.Items.FirstOrDefault(item => item.Id == incoming.Id)
                    : null;

                if (tracked is not null)
                {
                    CopyItem(incoming, tracked);
                    tracked.LastModifiedAt = DateTime.Now;
                    continue;
                }

                existing.Items.Add(new QuotationItem
                {
                    SortOrder = incoming.SortOrder,
                    SourceArticleId = incoming.SourceArticleId,
                    ArticleName = incoming.ArticleName,
                    Description = incoming.Description,
                    Specification = incoming.Specification,
                    Quantity = incoming.Quantity,
                    Unit = incoming.Unit,
                    UnitPrice = incoming.UnitPrice,
                    DiscountPercent = incoming.DiscountPercent,
                    Currency = incoming.Currency,
                    ExchangeRateSnapshot = incoming.ExchangeRateSnapshot,
                    CreatedBy = incoming.CreatedBy,
                    CreatedAt = incoming.CreatedAt ?? DateTime.Now,
                    LastModifiedBy = incoming.LastModifiedBy,
                    LastModifiedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        private static void CopyItem(QuotationItem source, QuotationItem target)
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
}
