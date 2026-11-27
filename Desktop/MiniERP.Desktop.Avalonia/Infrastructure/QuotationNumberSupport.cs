using MiniERP.ApplicationLayer.Services;

namespace MiniERP.Desktop.Infrastructure;

// Legacy preview adapter. Persisting quotations must go through IQuotationWorkflow.
public static class QuotationNumberSupport
{
    public static Task<string> PreviewNextAsync(IQuotationService service, int userCode, DateTime date)
        => QuotationNumberAllocator.PreviewNextAsync(service, userCode, date);
}
