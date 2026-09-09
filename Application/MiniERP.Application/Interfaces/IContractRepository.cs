using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Interfaces;

public interface IContractRepository : IRepository<Contract>
{
    Task<Contract?> GetByNumberAsync(string contractNumber);
    Task<IEnumerable<Contract>> GetByCustomerIdAsync(int customerId);
}
