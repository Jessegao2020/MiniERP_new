using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Interfaces;

public interface IPackingListRepository : IRepository<PackingList>
{
    Task<PackingList?> GetByNumberAsync(string packingListNumber);
    Task<IEnumerable<PackingList>> GetByCustomerIdAsync(int customerId);
}
