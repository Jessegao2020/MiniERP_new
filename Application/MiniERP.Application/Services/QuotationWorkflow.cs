using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

/// <summary>Persist prepared quotations through the same service from every client.</summary>
public static class QuotationWorkflow
{
    // The shared process gate preserves local desktop behavior. The database's
    // unique index is still the final authority across processes; distributed
    // number allocation/retry and multi-user authorization are future work.
    public static async Task<string?> SaveAsync(IQuotationService service, Quotation quotation,
        bool isNew, bool manualNumber, int userCode)
    {
        if (!QuotationRules.TryValidatePrepared(quotation, isNew, manualNumber, out var error))
            return error;

        if (isNew && !manualNumber)
        {
            await QuotationNumberAllocator.CreateAsync(service, quotation, userCode);
            return null;
        }

        var existing = await service.GetQuotationByNumberAsync(quotation.QuotationNumber);
        if (existing is not null && (isNew || existing.Id != quotation.Id))
            return "Quotation number already exists.";

        if (isNew) await service.CreateQuotationAsync(quotation);
        else await service.UpdateQuotationAsync(quotation);
        return null;
    }
}
