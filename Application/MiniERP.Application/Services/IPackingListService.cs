using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public interface IPackingListService
{
    Task<IEnumerable<PackingList>> GetAllPackingListsAsync();
    Task<PackingList?> GetPackingListByIdAsync(int id);
    Task<PackingList?> GetPackingListByNumberAsync(string packingListNumber);
    Task CreatePackingListAsync(PackingList packingList);
    Task UpdatePackingListAsync(PackingList packingList);
    Task DeletePackingListAsync(int id);
}
