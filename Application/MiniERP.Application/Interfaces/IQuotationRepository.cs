using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Interfaces
{
    public interface IQuotationRepository : IRepository<Quotation>
    {
        Task<Quotation?> GetByNumberAsync(string quotationNumber);
        Task<IEnumerable<Quotation>> GetByCustomerIdAsync(int customerId);
    }
}
