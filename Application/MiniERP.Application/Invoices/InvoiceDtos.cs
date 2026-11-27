using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Invoices;

// Explicit prices are in document currency, not catalog currency. Omitted
// prices on existing items retain their snapshots; new catalog items use the rate.
public sealed record InvoiceItemRequest(int? ArticleId, decimal Quantity, decimal? UnitPrice = null,
    decimal DiscountPercent = 0m, string? ArticleName = null, string? Unit = null,
    string? Description = null, string? Specification = null, int? ItemId = null);

public sealed record InvoiceRequest(InvoiceType Type, int CustomerId, int UserId,
    IReadOnlyList<InvoiceItemRequest> Items, int? ContactId = null, string? Currency = null,
    string? Number = null, DateTime? Date = null, DateTime? DueDate = null,
    string? DeliveryTerm = null, string? PaymentTerm = null, string? BankInformation = null,
    string? Remarks = null, string? CustomerPoNumber = null, DateTime? CustomerPoDate = null);

public sealed record InvoiceItemDto(int Id, int? ArticleId, string Name, string? Description,
    string? Specification, decimal Quantity, string Unit, decimal UnitPrice,
    decimal DiscountPercent, string Currency, decimal ExchangeRate, decimal Total);

public sealed record InvoiceSummaryDto(int Id, InvoiceType Type, string Number, int CustomerId,
    string? CustomerName, DateTime Date, DateTime? DueDate, string Currency, decimal Total,
    DocumentSourceType SourceType, int? SourceId, string? SourceNumber);

public sealed record InvoiceDetailDto(InvoiceSummaryDto Summary, int UserId, decimal ExchangeRate,
    string? CustomerAddress, string? CustomerContact, string? SalesContact, string? SalesEmail,
    string? SalesPhone, string? DeliveryTerm, string? PaymentTerm, string? BankInformation,
    string? Remarks, string? CustomerPoNumber, DateTime? CustomerPoDate, IReadOnlyList<InvoiceItemDto> Items);
