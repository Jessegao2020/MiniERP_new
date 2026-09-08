using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public sealed class QuotationService : IQuotationService
{
    private readonly IQuotationRepository _repository;

    public QuotationService(IQuotationRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Quotation>> GetAllQuotationsAsync()
        => _repository.GetAllAsync();

    public Task<Quotation?> GetQuotationByIdAsync(int id)
        => _repository.GetByIdAsync(id);

    public Task<Quotation?> GetQuotationByNumberAsync(string quotationNumber)
        => _repository.GetByNumberAsync(quotationNumber);

    public Task<IEnumerable<Quotation>> GetQuotationsByCustomerIdAsync(int customerId)
        => _repository.GetByCustomerIdAsync(customerId);

    public async Task CreateQuotationAsync(Quotation quotation)
        => await _repository.AddAsync(quotation);

    public Task UpdateQuotationAsync(Quotation quotation)
        => _repository.UpdateAsync(quotation);

    public Task DeleteQuotationAsync(int id)
        => _repository.DeleteAsync(id);
}
