namespace MiniERP.ApplicationLayer.Quotations;

// All explicit prices are in the document currency. Omit a new linked item's
// price to use the catalog; an existing item's omitted price retains its snapshot.
public sealed record QuotationItemRequest(
    int? ArticleId, decimal Quantity, decimal? UnitPrice = null,
    decimal DiscountPercent = 0m, string? ArticleName = null, string? Unit = null,
    string? Description = null, string? Specification = null, int? ItemId = null);

public sealed record CreateQuotationRequest(
    int CustomerId, int UserId, IReadOnlyList<QuotationItemRequest> Items,
    int? ContactId = null, string? Currency = null, string? QuotationNumber = null,
    DateTime? QuotationDate = null, DateTime? ValidUntil = null,
    string? DeliveryTerm = null, string? PaymentTerm = null,
    string? LeadTime = null, string? Remarks = null);

// Update replaces the item collection. Existing line IDs must belong to this
// quotation; omitted lines are removed. Null contact preserves a historical
// contact when the customer is unchanged. Null ValidUntil clears it.
public sealed record UpdateQuotationRequest(
    int QuotationId, int CustomerId, int UserId, IReadOnlyList<QuotationItemRequest> Items,
    DateTime QuotationDate, string Currency, int? ContactId = null,
    string? QuotationNumber = null, DateTime? ValidUntil = null,
    string? DeliveryTerm = null, string? PaymentTerm = null,
    string? LeadTime = null, string? Remarks = null);

public sealed record QuotationItemDto(
    int Id, int? ArticleId, string ArticleName, string? Description,
    string? Specification, decimal Quantity, string Unit, decimal UnitPrice,
    decimal DiscountPercent, string Currency, decimal ExchangeRate, decimal LineTotal);

public sealed record QuotationSummaryDto(
    int Id, string Number, int CustomerId, string? CustomerName, DateTime Date,
    DateTime? ValidUntil, string Currency, decimal TotalAmount);

public sealed record QuotationDetailDto(
    QuotationSummaryDto Summary, int UserId, decimal ExchangeRate,
    string? CustomerAddress, string? CustomerContact, string? SalesContact,
    string? SalesPhone, string? SalesEmail, string? DeliveryTerm,
    string? PaymentTerm, string? LeadTime, string? Remarks,
    IReadOnlyList<QuotationItemDto> Items);
