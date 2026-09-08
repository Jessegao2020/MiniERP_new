using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Interfaces;

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<Invoice?> GetByNumberAsync(string invoiceNumber);
    Task<IEnumerable<Invoice>> GetByTypeAsync(InvoiceType type);
    Task<IEnumerable<Invoice>> GetByCustomerIdAsync(int customerId);
}
