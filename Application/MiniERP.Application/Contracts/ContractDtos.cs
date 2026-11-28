using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Contracts;

// Explicit prices are in document currency, not catalog currency. Omitted
// prices on existing items retain their snapshots; new catalog items use the rate.
public sealed record ContractItemRequest(int? ArticleId, decimal Quantity, decimal? UnitPrice = null,
    decimal DiscountPercent = 0m, string? ArticleName = null, string? Unit = null,
    string? Description = null, string? Specification = null, int? ItemId = null);

public sealed record ContractRequest(int CustomerId, int UserId,
    IReadOnlyList<ContractItemRequest> Items, int? ContactId = null, string? Currency = null,
    string? Number = null, DateTime? Date = null,
    string? DeliveryTerm = null, string? PaymentTerm = null, string? BankInformation = null,
    string? Remarks = null, string? LeadTime = null, string? TermsAndConditions = null, string? CustomerPoNumber = null, DateTime? CustomerPoDate = null);

public sealed record ContractItemDto(int Id, int? ArticleId, string Name, string? Description,
    string? Specification, decimal Quantity, string Unit, decimal UnitPrice,
    decimal DiscountPercent, string Currency, decimal ExchangeRate, decimal Total);

public sealed record ContractSummaryDto(int Id, string Number, int CustomerId,
    string? CustomerName, DateTime Date, string Currency, decimal Total,
    DocumentSourceType SourceType, int? SourceId, string? SourceNumber);

public sealed record ContractDetailDto(ContractSummaryDto Summary, int UserId, decimal ExchangeRate,
    string? CustomerAddress, string? CustomerContact, string? SalesContact, string? SalesEmail,
    string? SalesPhone, string? DeliveryTerm, string? PaymentTerm, string? BankInformation,
    string? Remarks, string? LeadTime, string? TermsAndConditions, string? CustomerPoNumber, DateTime? CustomerPoDate, IReadOnlyList<ContractItemDto> Items);
