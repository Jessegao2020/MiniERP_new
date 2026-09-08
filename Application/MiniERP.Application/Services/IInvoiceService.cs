using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public interface IInvoiceService
{
    Task<IEnumerable<Invoice>> GetAllInvoicesAsync();
    Task<IEnumerable<Invoice>> GetInvoicesByTypeAsync(InvoiceType type);
    Task<Invoice?> GetInvoiceByIdAsync(int id);
    Task<Invoice?> GetInvoiceByNumberAsync(string invoiceNumber);
    Task CreateInvoiceAsync(Invoice invoice);
    Task UpdateInvoiceAsync(Invoice invoice);
    Task DeleteInvoiceAsync(int id);
}
