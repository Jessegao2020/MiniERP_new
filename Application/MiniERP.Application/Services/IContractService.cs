using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public interface IContractService
{
    Task<IEnumerable<Contract>> GetAllContractsAsync();
    Task<Contract?> GetContractByIdAsync(int id);
    Task<Contract?> GetContractByNumberAsync(string contractNumber);
    Task CreateContractAsync(Contract contract);
    Task UpdateContractAsync(Contract contract);
    Task DeleteContractAsync(int id);
}
