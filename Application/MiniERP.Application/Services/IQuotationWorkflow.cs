using MiniERP.ApplicationLayer.Quotations;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

/// <summary>Quotation use cases; remote adapters must use DTOs and enforce authorization first.</summary>
public interface IQuotationWorkflow
{
    Task<QuotationDetailDto> CreateAsync(CreateQuotationRequest request);
    Task<QuotationDetailDto> UpdateAsync(UpdateQuotationRequest request);
    Task<QuotationDetailDto> DuplicateAsync(int quotationId);
    Task DeleteAsync(int quotationId);
    Task<QuotationDetailDto?> GetDetailAsync(int quotationId);
    Task<IReadOnlyList<QuotationSummaryDto>> GetHistoryAsync(int customerId);
    Task<string> PreviewNumberAsync(int userId, DateTime date);

    // Trusted in-process desktop adapters only: preserve its existing edit buffer
    // and historical snapshots. Never bind these entity methods to remote input.
    Task<string?> SavePreparedAsync(Quotation quotation, bool isNew, bool manualNumber, int? contactId = null);
    Task<Quotation> DuplicatePreparedAsync(Quotation snapshot, int? contactId = null);
}
