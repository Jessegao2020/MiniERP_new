using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services
{
    public interface IArticleService
    {
        Task<IEnumerable<Article>> GetAllArticlesAsync();
        Task<Article?> GetArticleByIdAsync(int id);
        Task<Article?> GetArticleByCodeAsync(string code);
        Task<IEnumerable<Article>> SearchArticlesAsync(string keyword);
        Task<Article> CreateArticleAsync(Article article);
        Task UpdateArticleAsync(Article article);
        Task DeleteArticleAsync(int id);
    }
}

