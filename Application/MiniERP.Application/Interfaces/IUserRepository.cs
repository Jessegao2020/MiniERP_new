using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<IEnumerable<User>> SearchAsync(string keyword);
}
