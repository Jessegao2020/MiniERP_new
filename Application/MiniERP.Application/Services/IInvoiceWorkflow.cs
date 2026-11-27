using MiniERP.ApplicationLayer.Invoices;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public interface IInvoiceWorkflow
{
    Task<InvoiceDetailDto> CreateAsync(InvoiceRequest request);
    Task<InvoiceDetailDto> UpdateAsync(int id, InvoiceRequest request);
    Task<InvoiceDetailDto> ConvertAsync(DocumentSourceType sourceType, int sourceId, InvoiceType targetType);
    Task<InvoiceDetailDto?> GetDetailAsync(int id);
    Task<IReadOnlyList<InvoiceSummaryDto>> GetHistoryAsync(int customerId, InvoiceType? type = null);
    Task DeleteAsync(int id);
    // Trusted desktop edit-buffer adapter only; never bind remote input to entities.
    Task<string?> SavePreparedAsync(Invoice invoice, bool isNew, int? contactId = null);
}
