using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services
{
    public class ArticleService : IArticleService
    {
        private readonly IArticleRepository _articleRepository;

        public ArticleService(IArticleRepository articleRepository)
        {
            _articleRepository = articleRepository;
        }

        public async Task<IEnumerable<Article>> GetAllArticlesAsync()
        {
            return await _articleRepository.GetAllAsync();
        }

        public async Task<Article?> GetArticleByIdAsync(int id)
            => await _articleRepository.GetByIdAsync(id);

        public async Task<Article?> GetArticleByCodeAsync(string code)
            => await _articleRepository.GetByCodeAsync(code);

        public async Task<IEnumerable<Article>> SearchArticlesAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await GetAllArticlesAsync();

            return await _articleRepository.SearchAsync(keyword);
        }

        public async Task<Article> CreateArticleAsync(Article article)
            => await _articleRepository.AddAsync(article);

        public async Task UpdateArticleAsync(Article article)
        {
            var existing = await _articleRepository.GetByIdAsync(article.Id);
            if (existing == null)
                throw new KeyNotFoundException($"物料 ID {article.Id} 不存在");

            existing.Name           = article.Name?.Trim();
            existing.Description    = article.Description;
            existing.Price          = article.Price;
            existing.MinimumPrice   = article.MinimumPrice;
            existing.Category       = article.Category;
            existing.Specification  = article.Specification;
            existing.Name_EN        = article.Name_EN;
            existing.Specs_EN       = article.Specs_EN;
            existing.Description_EN = article.Description_EN;
            existing.Discount       = article.Discount;
            existing.Note           = article.Note;

            await _articleRepository.UpdateAsync(existing);
        }

        public async Task DeleteArticleAsync(int id)
        {
            var existing = await _articleRepository.GetByIdAsync(id);
            if (existing == null)
            {
                throw new KeyNotFoundException($"Article: {id} doesn't exist.");
            }

            await _articleRepository.DeleteAsync(id);
        }
    }
}

