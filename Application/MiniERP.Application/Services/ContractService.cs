using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public sealed class ContractService : IContractService
{
    private readonly IContractRepository _repository;

    public ContractService(IContractRepository repository) => _repository = repository;

    public Task<IEnumerable<Contract>> GetAllContractsAsync() => _repository.GetAllAsync();
    public Task<Contract?> GetContractByIdAsync(int id) => _repository.GetByIdAsync(id);
    public Task<Contract?> GetContractByNumberAsync(string contractNumber) => _repository.GetByNumberAsync(contractNumber);
    public async Task CreateContractAsync(Contract contract) => await _repository.AddAsync(contract);
    public Task UpdateContractAsync(Contract contract) => _repository.UpdateAsync(contract);
    public Task DeleteContractAsync(int id) => _repository.DeleteAsync(id);
}
