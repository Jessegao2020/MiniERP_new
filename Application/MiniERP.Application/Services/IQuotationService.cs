using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public interface IQuotationService
{
    Task<IEnumerable<Quotation>> GetAllQuotationsAsync();
    Task<Quotation?> GetQuotationByIdAsync(int id);
    Task<Quotation?> GetQuotationByNumberAsync(string quotationNumber);
    Task<IReadOnlyList<string>> GetQuotationNumbersByPrefixAsync(string prefix);
    Task<IEnumerable<Quotation>> GetQuotationsByCustomerIdAsync(int customerId);
    Task CreateQuotationAsync(Quotation quotation);
    Task UpdateQuotationAsync(Quotation quotation);
    Task DeleteQuotationAsync(int id);
}
