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

        public override async Task<IEnumerable<Customer>> GetAllAsync()
            => await _dbSet
                .AsNoTracking()
                .Include(customer => customer.Contacts)
                .OrderBy(customer => customer.Name)
                .ToListAsync();

        public override async Task<Customer?> GetByIdAsync(int id)
            => await _dbSet
                .AsNoTracking()
                .Include(customer => customer.Contacts)
                .FirstOrDefaultAsync(customer => customer.Id == id);

        public async Task<Customer?> GetByCodeAsync(string code)
        {
            // Customer does not currently have a dedicated Code property.
            // Keep this legacy API useful by treating an exact name as the lookup key.
            var normalized = code?.Trim();
            if (string.IsNullOrEmpty(normalized))
                return null;

            return await _dbSet
                .AsNoTracking()
                .Include(customer => customer.Contacts)
                .FirstOrDefaultAsync(customer => customer.Name == normalized);
        }

        public async Task<IEnumerable<Customer>> SearchAsync(string keyword)
        {
            var normalized = keyword?.Trim();
            if (string.IsNullOrEmpty(normalized))
                return await GetAllAsync();

            return await _dbSet
                .AsNoTracking()
                .Include(customer => customer.Contacts)
                .Where(customer =>
                    customer.Name.Contains(normalized) ||
                    (customer.AddressLine1 != null && customer.AddressLine1.Contains(normalized)) ||
                    (customer.AddressLine2 != null && customer.AddressLine2.Contains(normalized)) ||
                    (customer.City != null && customer.City.Contains(normalized)) ||
                    (customer.State != null && customer.State.Contains(normalized)) ||
                    (customer.PostalCode != null && customer.PostalCode.Contains(normalized)) ||
                    (customer.Country != null && customer.Country.Contains(normalized)))
                .OrderBy(customer => customer.Name)
                .ToListAsync();
        }

        public override async Task UpdateAsync(Customer customer)
        {
            var existing = await _dbSet
                .Include(item => item.Contacts)
                .FirstOrDefaultAsync(item => item.Id == customer.Id)
                ?? throw new InvalidOperationException($"Customer {customer.Id} no longer exists.");

            existing.Name = customer.Name;
            existing.AddressLine1 = customer.AddressLine1;
            existing.AddressLine2 = customer.AddressLine2;
            existing.City = customer.City;
            existing.State = customer.State;
            existing.PostalCode = customer.PostalCode;
            existing.Country = customer.Country;
            existing.IsActive = customer.IsActive;
            existing.LastModifiedBy = customer.LastModifiedBy;
            existing.LastModifiedAt = DateTime.Now;

            var incomingContacts = customer.Contacts.ToList();
            var incomingIds = incomingContacts
                .Where(contact => contact.Id > 0)
                .Select(contact => contact.Id)
                .ToHashSet();

            foreach (var oldContact in existing.Contacts.Where(contact => !incomingIds.Contains(contact.Id)).ToList())
                _context.Contacts.Remove(oldContact);

            foreach (var incoming in incomingContacts)
            {
                if (string.IsNullOrWhiteSpace(incoming.Name))
                    continue;

                var tracked = incoming.Id > 0
                    ? existing.Contacts.FirstOrDefault(contact => contact.Id == incoming.Id)
                    : null;

                if (tracked is not null)
                {
                    tracked.Title = incoming.Title;
                    tracked.Name = incoming.Name.Trim();
                    tracked.LastModifiedBy = incoming.LastModifiedBy;
                    tracked.LastModifiedAt = DateTime.Now;
                    continue;
                }

                existing.Contacts.Add(new CustomerContact
                {
                    Title = incoming.Title,
                    Name = incoming.Name.Trim(),
                    CreatedBy = incoming.CreatedBy,
                    CreatedAt = incoming.CreatedAt ?? DateTime.Now,
                    LastModifiedBy = incoming.LastModifiedBy,
                    LastModifiedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        public override async Task DeleteAsync(int id)
        {
            var existing = await _dbSet
                .Include(customer => customer.Contacts)
                .FirstOrDefaultAsync(customer => customer.Id == id);

            if (existing is null)
                return;

            _dbSet.Remove(existing);
            await _context.SaveChangesAsync();
        }
    }
}
