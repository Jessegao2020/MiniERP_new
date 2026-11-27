using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

// Compatibility adapter; business rules live in Application.
public static class QuotationNumberSupport
{
    public static Task<string> PreviewNextAsync(IQuotationService service, int userCode, DateTime date)
        => QuotationNumberAllocator.PreviewNextAsync(service, userCode, date);

    public static Task CreateAsync(IQuotationService service, Quotation quotation, int userCode)
        => QuotationNumberAllocator.CreateAsync(service, quotation, userCode);
}
