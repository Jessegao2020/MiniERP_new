using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories
{
    public class CustomerRepository : Repository<Customer>, ICustomerRepository
    {
        public CustomerRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Customer?> GetByCodeAsync(string code)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Customer>> SearchAsync(string keyword)
        {
            throw new NotImplementedException();

        }
    }
}

