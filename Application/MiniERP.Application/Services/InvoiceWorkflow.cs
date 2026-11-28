using MiniERP.ApplicationLayer.Invoices;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public sealed class InvoiceWorkflow : IInvoiceWorkflow
{
    private readonly IInvoiceService _invoices;
    private readonly ICustomerService _customers;
    private readonly IUserService _users;
    private readonly IArticleService _articles;
    private readonly ISalesDocumentCopyService _copies;
    private readonly IQuotationService _quotations;
    private readonly IContractService _contracts;
    private readonly InvoicePolicy _policy;

    public InvoiceWorkflow(IInvoiceService invoices, ICustomerService customers, IUserService users,
        IArticleService articles, ISalesDocumentCopyService copies, IQuotationService quotations,
        IContractService contracts, InvoicePolicy policy)
    {
        _invoices = invoices; _customers = customers; _users = users; _articles = articles;
        _copies = copies; _quotations = quotations; _contracts = contracts; _policy = policy;
    }

    public Task<InvoiceDetailDto> CreateAsync(InvoiceRequest request) => WriteAsync(null, request);
    public Task<InvoiceDetailDto> UpdateAsync(int id, InvoiceRequest request) => WriteAsync(id, request);

    private async Task<InvoiceDetailDto> WriteAsync(int? id, InvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _policy.Validate();
        var previous = id is null ? null : await RequireInvoiceAsync(id.Value);
        if (previous is not null && request.Type != previous.Type)
            throw new InvalidOperationException("Use conversion to change document type.");
        var invoice = previous is null
            ? InvoiceRules.CreateDraft(request.Type, DateTime.Now, _policy)
            : InvoiceRules.Clone(previous);
        var customer = await RequireCustomerAsync(request.CustomerId);
        var user = await RequireUserAsync(request.UserId);
        var contact = ResolveContact(customer, request.ContactId);
        invoice.InvoiceNumber = request.Number?.Trim() ?? invoice.InvoiceNumber;
        invoice.InvoiceDate = request.Date ?? invoice.InvoiceDate;
        invoice.ExchangeRate = InvoiceRules.ResolveExchangeRate(invoice, _policy.CnyPerUsd ?? 0m);
        invoice.DueDate = request.DueDate;
        invoice.Currency = request.Currency is null ? invoice.Currency : QuotationRules.NormalizeCurrency(request.Currency);
        invoice.DeliveryTerm = previous is null ? request.DeliveryTerm ?? invoice.DeliveryTerm : request.DeliveryTerm;
        invoice.PaymentTerm = previous is null ? request.PaymentTerm ?? invoice.PaymentTerm : request.PaymentTerm;
        invoice.BankInformation = request.BankInformation;
        invoice.Remarks = request.Remarks;
        invoice.CustomerPoNumber = request.CustomerPoNumber;
        invoice.CustomerPoDate = request.CustomerPoDate;
        // Existing lines require explicit target-currency prices on a DTO currency
        // change. Desktop applies a confirmed shared conversion plan before saving.
        var items = await BuildItemsAsync(request.Items, invoice, previous);
        if (!InvoiceRules.TryPrepare(invoice, false, customer, user, contact, invoice.InvoiceDate, invoice.DueDate,
            invoice.CustomerPoDate, invoice.Currency, invoice.ExchangeRate, items, out var error, out _))
            throw new InvalidOperationException(error);
        ThrowOnError(await SavePreparedAsync(invoice, previous is null, contact?.Id));
        return Detail(invoice);
    }

    public async Task<InvoiceDetailDto> ConvertAsync(DocumentSourceType sourceType, int sourceId, InvoiceType targetType)
    {
        if (targetType is not (InvoiceType.Proforma or InvoiceType.Commercial))
            throw new InvalidOperationException("Unsupported invoice type.");
        if (sourceType is DocumentSourceType.ProformaInvoice or DocumentSourceType.CommercialInvoice)
        {
            var source = await RequireInvoiceAsync(sourceId);
            var actual = source.Type == InvoiceType.Proforma ? DocumentSourceType.ProformaInvoice : DocumentSourceType.CommercialInvoice;
            if (actual != sourceType) throw new InvalidOperationException("Invoice source type does not match.");
        }
        var invoice = sourceType switch
        {
            DocumentSourceType.Quotation => await _copies.CreateInvoiceFromQuotationAsync(sourceId, targetType),
            DocumentSourceType.ProformaInvoice or DocumentSourceType.CommercialInvoice => await _copies.CreateInvoiceFromInvoiceAsync(sourceId, targetType),
            DocumentSourceType.Contract => await _copies.CreateInvoiceFromContractAsync(sourceId, targetType),
            _ => throw new InvalidOperationException("Unsupported invoice source type.")
        };
        InvoiceRules.ApplyDraftDefaults(invoice, _policy);
        ThrowOnError(await SavePreparedAsync(invoice, true));
        return Detail(invoice);
    }

    public async Task<string?> SavePreparedAsync(Invoice invoice, bool isNew, int? contactId = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        _policy.Validate();
        if (isNew ? invoice.Id != 0 : invoice.Id <= 0) return "Invoice identity does not match the requested operation.";
        var previous = isNew ? null : await RequireInvoiceAsync(invoice.Id);
        if (invoice.Type is not (InvoiceType.Proforma or InvoiceType.Commercial)) return "Unsupported invoice type.";
        if (previous is not null && invoice.Type != previous.Type) return "Use conversion to change document type.";
        var customer = await RequireCustomerAsync(invoice.CustomerId);
        await RequireUserAsync(invoice.UserId);
        ResolveContact(customer, contactId);
        if (!customer.IsActive && (previous is null || previous.CustomerId != customer.Id)) return "Customer is inactive.";
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber)) return "Document number is required.";
        if (invoice.InvoiceDate == default) return "Date is required.";
        if (!QuotationRules.IsSupportedCurrency(invoice.Currency)) return "Unsupported invoice currency.";
        if (invoice.Currency == "USD" && invoice.Items.Count > 0 && invoice.ExchangeRate <= 0) return "A valid USD exchange-rate snapshot is required.";
        // Do not change existing due-date semantics; advance/prepayment dates may precede the invoice.
        if (previous is not null && (invoice.SourceDocumentType != previous.SourceDocumentType ||
            invoice.SourceDocumentId != previous.SourceDocumentId || invoice.SourceDocumentNumber != previous.SourceDocumentNumber))
            return "Existing invoice source provenance cannot be changed.";
        if (isNew) await ValidateSourceAsync(invoice);
        var seen = new HashSet<int>();
        foreach (var line in invoice.Items)
        {
            if (!InvoiceRules.TryValidateItem(line, out var error)) return error;
            var old = line.Id > 0 ? previous?.Items.FirstOrDefault(i => i.Id == line.Id) : null;
            if (line.Id < 0 || (line.Id > 0 && (old is null || !seen.Add(line.Id))))
                return "Invoice item identity is invalid or belongs to another invoice.";
            var unchangedLegacy = old is not null && previous is not null
                && previous.Currency == invoice.Currency && previous.ExchangeRate == invoice.ExchangeRate
                && old.Currency == line.Currency && old.ExchangeRateSnapshot == line.ExchangeRateSnapshot
                && old.Quantity == line.Quantity && old.UnitPrice == line.UnitPrice
                && old.DiscountPercent == line.DiscountPercent && old.SourceArticleId == line.SourceArticleId;
            if (!unchangedLegacy && (line.Currency != invoice.Currency || line.ExchangeRateSnapshot != invoice.ExchangeRate))
                return $"Currency or exchange-rate snapshot for '{line.ArticleName}' does not match the invoice.";
        }
        invoice.InvoiceNumber = invoice.InvoiceNumber.Trim();
        var same = await _invoices.GetInvoiceByNumberAsync(invoice.InvoiceNumber);
        if (same is not null && (isNew || same.Id != invoice.Id)) return "Document number already exists.";
        if (isNew) await _invoices.CreateInvoiceAsync(invoice);
        else await _invoices.UpdateInvoiceAsync(invoice);
        return null;
    }

    private async Task ValidateSourceAsync(Invoice invoice)
    {
        if (invoice.SourceDocumentType == DocumentSourceType.None)
        {
            if (invoice.SourceDocumentId is not null || !string.IsNullOrWhiteSpace(invoice.SourceDocumentNumber))
                throw new InvalidOperationException("Manual invoice cannot carry source provenance.");
            return;
        }
        var id = invoice.SourceDocumentId ?? throw new InvalidOperationException("Source document ID is required.");
        // Source number is a historical snapshot; it may have been edited since
        // the draft was opened, so verify existence/type, not current number.
        switch (invoice.SourceDocumentType)
        {
            case DocumentSourceType.Quotation:
                if (await _quotations.GetQuotationByIdAsync(id) is null) throw new InvalidOperationException("Source quotation was not found.");
                break;
            case DocumentSourceType.Contract:
                if (await _contracts.GetContractByIdAsync(id) is null) throw new InvalidOperationException("Source contract was not found.");
                break;
            case DocumentSourceType.ProformaInvoice:
            case DocumentSourceType.CommercialInvoice:
                var source = await RequireInvoiceAsync(id);
                if ((source.Type == InvoiceType.Proforma) != (invoice.SourceDocumentType == DocumentSourceType.ProformaInvoice))
                    throw new InvalidOperationException("Invoice source type does not match.");
                break;
            default: throw new InvalidOperationException("Unsupported invoice source type.");
        }
    }

    private async Task<List<InvoiceItem>> BuildItemsAsync(IReadOnlyList<InvoiceItemRequest> requests, Invoice invoice, Invoice? previous)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var items = new List<InvoiceItem>();
        var seen = new HashSet<int>();
        foreach (var request in requests)
        {
            ArgumentNullException.ThrowIfNull(request);
            InvoiceItem line;
            if (request.ItemId is int id)
            {
                if (id <= 0 || !seen.Add(id)) throw new InvalidOperationException("Invalid or duplicate invoice item ID.");
                var old = previous?.Items.FirstOrDefault(i => i.Id == id)
                    ?? throw new InvalidOperationException("Invoice item does not belong to this invoice.");
                if (request.ArticleId is not null && request.ArticleId != old.SourceArticleId)
                    throw new InvalidOperationException("Use a new line to change source article.");
                if ((old.Currency != invoice.Currency || old.ExchangeRateSnapshot != invoice.ExchangeRate) && request.UnitPrice is null)
                    throw new InvalidOperationException("Supply an explicit unit price when changing currency or exchange rate.");
                line = InvoiceRules.CloneItem(old);
            }
            else if (request.ArticleId is int articleId)
            {
                var article = await _articles.GetArticleByIdAsync(articleId)
                    ?? throw new InvalidOperationException($"Article {articleId} was not found.");
                var price = request.UnitPrice ?? 0m;
                if (request.UnitPrice is null && !InvoiceRules.TryGetArticleUnitPrice(article, invoice.Currency,
                    invoice.ExchangeRate, out price, out var error)) throw new InvalidOperationException(error);
                line = InvoiceRules.CreateItemSnapshot(article, invoice.Currency, invoice.ExchangeRate, price);
            }
            else
            {
                if (request.UnitPrice is null) throw new InvalidOperationException("Manual invoice items require an explicit unit price.");
                line = new InvoiceItem { ArticleName = request.ArticleName ?? string.Empty, Unit = "PCS" };
            }
            line.ArticleName = request.ArticleName ?? line.ArticleName;
            line.Description = request.Description ?? line.Description;
            line.Specification = request.Specification ?? line.Specification;
            line.Unit = request.Unit ?? line.Unit;
            line.Quantity = request.Quantity;
            line.UnitPrice = request.UnitPrice ?? line.UnitPrice;
            line.DiscountPercent = request.DiscountPercent;
            line.Currency = invoice.Currency;
            line.ExchangeRateSnapshot = invoice.ExchangeRate;
            line.SortOrder = items.Count;
            items.Add(line);
        }
        return items;
    }

    public async Task DeleteAsync(int id)
    {
        await RequireInvoiceAsync(id);
        await _invoices.DeleteInvoiceAsync(id);
    }
    public async Task<InvoiceDetailDto?> GetDetailAsync(int id)
    {
        var invoice = await _invoices.GetInvoiceByIdAsync(id);
        return invoice is null ? null : Detail(invoice);
    }
    public async Task<IReadOnlyList<InvoiceSummaryDto>> GetHistoryAsync(int customerId, InvoiceType? type = null)
    {
        await RequireCustomerAsync(customerId);
        return (await _invoices.GetInvoicesByCustomerIdAsync(customerId))
            .Where(i => type is null || i.Type == type).OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id).Select(Summary).ToList();
    }
    private async Task<Invoice> RequireInvoiceAsync(int id)
        => await _invoices.GetInvoiceByIdAsync(id) ?? throw new InvalidOperationException($"Invoice {id} was not found.");
    private async Task<Customer> RequireCustomerAsync(int id)
        => await _customers.GetCustomerByIdAsync(id) ?? throw new InvalidOperationException($"Customer {id} was not found.");
    private async Task<User> RequireUserAsync(int id)
        => (await _users.GetAllUsersAsync()).FirstOrDefault(u => u.Id == id) ?? throw new InvalidOperationException($"User {id} was not found.");
    private static CustomerContact? ResolveContact(Customer customer, int? id)
        => id is null ? null : customer.Contacts.FirstOrDefault(c => c.Id == id.Value)
            ?? throw new InvalidOperationException("Contact does not belong to the selected customer.");
    private static void ThrowOnError(string? error)
    {
        if (error is not null) throw new InvalidOperationException(error);
    }
    private static InvoiceSummaryDto Summary(Invoice i)
        => new(i.Id, i.Type, i.InvoiceNumber, i.CustomerId, i.CustomerNameSnapshot, i.InvoiceDate,
            i.DueDate, i.Currency, i.TotalAmount, i.SourceDocumentType, i.SourceDocumentId, i.SourceDocumentNumber);
    private static InvoiceDetailDto Detail(Invoice i)
        => new(Summary(i), i.UserId, i.ExchangeRate, i.CustomerAddressSnapshot, i.CustomerContactSnapshot,
            i.SalesContactNameSnapshot, i.SalesContactEmailSnapshot, i.SalesContactPhoneSnapshot,
            i.DeliveryTerm, i.PaymentTerm, i.BankInformation, i.Remarks, i.CustomerPoNumber, i.CustomerPoDate,
            i.Items.OrderBy(l => l.SortOrder).ThenBy(l => l.Id).Select(l => new InvoiceItemDto(l.Id,
                l.SourceArticleId, l.ArticleName, l.Description, l.Specification, l.Quantity, l.Unit, l.UnitPrice,
                l.DiscountPercent, l.Currency, l.ExchangeRateSnapshot, l.LineTotal)).ToList());
}
