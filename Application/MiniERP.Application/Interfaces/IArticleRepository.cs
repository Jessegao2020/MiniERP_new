using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Interfaces
{
    public interface IArticleRepository : IRepository<Article>
    {
        Task<Article?> GetByCodeAsync(string code);

        Task<IEnumerable<Article>> SearchAsync(string keyword);
    }
}

