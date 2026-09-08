using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public sealed class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _repository;

    public InvoiceService(IInvoiceRepository repository) => _repository = repository;

    public Task<IEnumerable<Invoice>> GetAllInvoicesAsync() => _repository.GetAllAsync();
    public Task<IEnumerable<Invoice>> GetInvoicesByTypeAsync(InvoiceType type) => _repository.GetByTypeAsync(type);
    public Task<Invoice?> GetInvoiceByIdAsync(int id) => _repository.GetByIdAsync(id);
    public Task<Invoice?> GetInvoiceByNumberAsync(string invoiceNumber) => _repository.GetByNumberAsync(invoiceNumber);
    public async Task CreateInvoiceAsync(Invoice invoice) => await _repository.AddAsync(invoice);
    public Task UpdateInvoiceAsync(Invoice invoice) => _repository.UpdateAsync(invoice);
    public Task DeleteInvoiceAsync(int id) => _repository.DeleteAsync(id);
}
