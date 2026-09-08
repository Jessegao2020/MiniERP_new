using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories;

public sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<User>> GetAllAsync()
        => await _dbSet.AsNoTracking().OrderBy(user => user.Name).ToListAsync();

    public async Task<IEnumerable<User>> SearchAsync(string keyword)
    {
        var normalized = keyword?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return await GetAllAsync();

        return await _dbSet
            .AsNoTracking()
            .Where(user =>
                user.Name.Contains(normalized) ||
                user.Email.Contains(normalized) ||
                user.Phone.Contains(normalized) ||
                user.Code.ToString().Contains(normalized))
            .OrderBy(user => user.Name)
            .ToListAsync();
    }

    public override async Task UpdateAsync(User user)
    {
        var existing = await _dbSet.FirstOrDefaultAsync(item => item.Id == user.Id)
            ?? throw new InvalidOperationException($"User {user.Id} no longer exists.");

        existing.Code = user.Code;
        existing.Name = user.Name;
        existing.Email = user.Email;
        existing.Phone = user.Phone;
        await _context.SaveChangesAsync();
    }

    public override async Task DeleteAsync(int id)
    {
        var isReferenced =
            await _context.Quotations.AnyAsync(q => q.UserId == id) ||
            await _context.Invoices.AnyAsync(i => i.UserId == id) ||
            await _context.PackingLists.AnyAsync(p => p.UserId == id);

        if (isReferenced)
            throw new InvalidOperationException("This user is referenced by one or more sales documents and cannot be deleted.");

        await base.DeleteAsync(id);
    }
}
