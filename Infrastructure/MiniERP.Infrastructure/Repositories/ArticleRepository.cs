using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories;

/// <summary>Article persistence is exclusive to catalog.db for the Avalonia application.</summary>
public sealed class ArticleRepository : IArticleRepository
{
    private readonly CatalogDbContext _context;
    private readonly DbSet<Article> _articles;

    public ArticleRepository(CatalogDbContext context)
    {
        _context = context;
        _articles = context.Articles;
    }

    public async Task<IEnumerable<Article>> GetAllAsync()
        => await _articles.AsNoTracking().ToListAsync();

    public async Task<Article?> GetByIdAsync(int id)
        => await _articles.FindAsync(id);

    public async Task<Article> AddAsync(Article article)
    {
        await _articles.AddAsync(article);
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task UpdateAsync(Article article)
    {
        // ArticleService obtains the existing tracked record from this scoped context.
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var article = await _articles.FindAsync(id);
        if (article is null) return;
        _articles.Remove(article);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id)
        => await _articles.AnyAsync(article => article.Id == id);

    public async Task<Article?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var normalized = code.Trim();
        return await _articles.FirstOrDefaultAsync(article => article.Name == normalized);
    }

    public async Task<IEnumerable<Article>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return await GetAllAsync();

        var term = keyword.Trim();
        return await _articles.AsNoTracking().Where(article =>
            (article.Name ?? "").Contains(term) ||
            (article.Description ?? "").Contains(term) ||
            (article.Specification ?? "").Contains(term) ||
            (article.Category ?? "").Contains(term)).ToListAsync();
    }
}
