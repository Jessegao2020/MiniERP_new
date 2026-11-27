using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

// Compatibility adapter for desktop copy commands.
public static class QuotationCopySupport
{
    public static Quotation CreateCopy(Quotation source) => QuotationCopy.CreateCopy(source);
}
