using Microsoft.EntityFrameworkCore;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Infrastructure.Repositories;

public sealed class DocumentCurrencyGuard : IDocumentCurrencyGuard
{
    private readonly ApplicationDbContext _context;
    public DocumentCurrencyGuard(ApplicationDbContext context) => _context = context;

    public async Task<bool> HasDownstreamAsync(DocumentSourceType sourceType, int id)
    {
        if (id <= 0) return false;
        var invoice = sourceType is DocumentSourceType.ProformaInvoice or DocumentSourceType.CommercialInvoice;
        // Use the same conservative matching as invoice deletion, including old
        // records whose source type may not match the invoice type precisely.
        return await _context.Invoices.AnyAsync(d => d.SourceDocumentId == id &&
                (d.SourceDocumentType == sourceType || (invoice &&
                    (d.SourceDocumentType == DocumentSourceType.ProformaInvoice || d.SourceDocumentType == DocumentSourceType.CommercialInvoice))))
            || await _context.Contracts.AnyAsync(d => d.SourceDocumentId == id &&
                (d.SourceDocumentType == sourceType || (invoice &&
                    (d.SourceDocumentType == DocumentSourceType.ProformaInvoice || d.SourceDocumentType == DocumentSourceType.CommercialInvoice))))
            || await _context.PackingLists.AnyAsync(d => d.SourceDocumentId == id &&
                (d.SourceDocumentType == sourceType || (invoice &&
                    (d.SourceDocumentType == DocumentSourceType.ProformaInvoice || d.SourceDocumentType == DocumentSourceType.CommercialInvoice))));
    }
}
