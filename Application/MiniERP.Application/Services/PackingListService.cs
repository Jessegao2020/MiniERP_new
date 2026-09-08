using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public sealed class PackingListService : IPackingListService
{
    private readonly IPackingListRepository _repository;

    public PackingListService(IPackingListRepository repository) => _repository = repository;

    public Task<IEnumerable<PackingList>> GetAllPackingListsAsync() => _repository.GetAllAsync();
    public Task<PackingList?> GetPackingListByIdAsync(int id) => _repository.GetByIdAsync(id);
    public Task<PackingList?> GetPackingListByNumberAsync(string packingListNumber) => _repository.GetByNumberAsync(packingListNumber);
    public async Task CreatePackingListAsync(PackingList packingList) => await _repository.AddAsync(packingList);
    public Task UpdatePackingListAsync(PackingList packingList) => _repository.UpdateAsync(packingList);
    public Task DeletePackingListAsync(int id) => _repository.DeleteAsync(id);
}
