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
                .OrderByDescending(q => q.QuotationDate)
                .ThenByDescending(q => q.Id)
                .ToListAsync();

        public override async Task<Quotation?> GetByIdAsync(int id)
            => await _dbSet
                .AsNoTracking()
                .Include(q => q.Customer)
                .Include(q => q.User)
                .FirstOrDefaultAsync(q => q.Id == id);

        public async Task<Quotation?> GetByNumberAsync(string quotationNumber)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(q => q.Customer)
                .Include(q => q.User)
                .FirstOrDefaultAsync(q => q.QuotationNumber == quotationNumber);
        }

        public async Task<IEnumerable<Quotation>> GetByCustomerIdAsync(int customerId)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(q => q.Customer)
                .Include(q => q.User)
                .Where(q => q.CustomerId == customerId)
                .OrderByDescending(q => q.QuotationDate)
                .ToListAsync();
        }

        public override async Task UpdateAsync(Quotation quotation)
        {
            var existing = await _dbSet.FirstOrDefaultAsync(item => item.Id == quotation.Id)
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
            existing.LastModifiedBy = quotation.LastModifiedBy;
            existing.LastModifiedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }
    }
}
