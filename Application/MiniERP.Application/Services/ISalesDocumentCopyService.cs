using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public interface ISalesDocumentCopyService
{
    Task<Invoice> CreateInvoiceFromQuotationAsync(int quotationId, InvoiceType targetType);
    Task<Invoice> CreateInvoiceFromInvoiceAsync(int invoiceId, InvoiceType targetType);
    Task<Invoice> CreateInvoiceFromContractAsync(int contractId, InvoiceType targetType);
    Task<PackingList> CreatePackingListFromQuotationAsync(int quotationId);
    Task<PackingList> CreatePackingListFromInvoiceAsync(int invoiceId);
    Task<PackingList> CreatePackingListFromContractAsync(int contractId);
    Task<Contract> CreateContractFromQuotationAsync(int quotationId);
    Task<Contract> CreateContractFromInvoiceAsync(int invoiceId);
}
