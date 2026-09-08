using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<User>> GetAllUsersAsync()
        => _repository.GetAllAsync();

    public Task<IEnumerable<User>> SearchUsersAsync(string keyword)
        => _repository.SearchAsync(keyword);

    public async Task CreateUserAsync(User user)
        => await _repository.AddAsync(user);

    public Task UpdateUserAsync(User user)
        => _repository.UpdateAsync(user);

    public Task DeleteUserAsync(int id)
        => _repository.DeleteAsync(id);
}
