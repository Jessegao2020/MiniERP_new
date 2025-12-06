using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories
{
    public class ArticleRepository : Repository<Article>, IArticleRepository
    {
        public ArticleRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Article?> GetByCodeAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            var normalized = code?.Trim();

            return await _dbSet.FirstOrDefaultAsync(a => a.Name == normalized);
        }

        public async Task<IEnumerable<Article>> SearchAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await GetAllAsync();

            var normalizedKeyword = keyword.Trim();

            return await _dbSet.Where(a =>
            (a.Name ?? "").Contains(normalizedKeyword) ||
            (a.Description ?? "").Contains(normalizedKeyword) ||
            (a.Specification ?? "").Contains(normalizedKeyword) ||
            (a.Category ?? "").Contains(normalizedKeyword))
                .ToListAsync();
        }
    }
}

