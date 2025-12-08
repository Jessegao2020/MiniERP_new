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

        public async Task<Quotation?> GetByNumberAsync(string quotationNumber)
        {
            return await _dbSet
                .Include(q => q.Customer)
                .FirstOrDefaultAsync(q => q.QuotationNumber == quotationNumber);
        }

        public async Task<IEnumerable<Quotation>> GetByCustomerIdAsync(int customerId)
        {
            return await _dbSet
                .Include(q => q.Customer)
                .Where(q => q.CustomerId == customerId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Quotation>> GetByStatusAsync(string status)
        {
            throw new NotImplementedException();
        }
    }
}

