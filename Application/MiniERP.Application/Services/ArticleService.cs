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
            {
                throw new KeyNotFoundException($"物料 ID {article.Id} 不存在");
            }

            var normalizedName = article.Name?.Trim();

            // 如果编码改变，检查新编码是否已存在
            if (!string.Equals(existing.Name, normalizedName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(normalizedName))
            {
                var codeExists = await _articleRepository.GetByCodeAsync(normalizedName);
                if (codeExists != null && codeExists.Id != article.Id)
                {
                    throw new InvalidOperationException($"物料名称/编码 {normalizedName} 已存在");
                }
            }

            article.Name = normalizedName;
            await _articleRepository.UpdateAsync(article);
        }

        public async Task DeleteArticleAsync(int id)
        {
            var existing = await _articleRepository.GetByIdAsync(id);
            if (existing == null)
            {
                throw new KeyNotFoundException($"物料 ID {id} 不存在");
            }

            await _articleRepository.DeleteAsync(id);
        }
    }
}

