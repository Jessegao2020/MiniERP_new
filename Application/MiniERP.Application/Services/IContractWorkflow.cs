using MiniERP.ApplicationLayer.Contracts;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public interface IContractWorkflow
{
    Task<ContractDetailDto> CreateAsync(ContractRequest request);
    Task<ContractDetailDto> UpdateAsync(int id, ContractRequest request);
    Task<ContractDetailDto> ConvertAsync(DocumentSourceType sourceType, int sourceId);
    Task<ContractDetailDto?> GetDetailAsync(int id);
    Task<IReadOnlyList<ContractSummaryDto>> GetHistoryAsync(int customerId);
    Task DeleteAsync(int id);
    // Trusted in-process desktop edit buffer only.
    Task<string?> SavePreparedAsync(Contract contract, bool isNew, int? contactId = null);
}
