using MiniERP.ApplicationLayer.Contracts;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public sealed class ContractWorkflow : IContractWorkflow
{
    private readonly IContractService _contracts;
    private readonly ICustomerService _customers;
    private readonly IUserService _users;
    private readonly IArticleService _articles;
    private readonly ISalesDocumentCopyService _copies;
    private readonly IQuotationService _quotations;
    private readonly IInvoiceService _invoices;
    private readonly ContractPolicy _policy;

    public ContractWorkflow(IContractService contracts, ICustomerService customers, IUserService users,
        IArticleService articles, ISalesDocumentCopyService copies, IQuotationService quotations,
        IInvoiceService invoices, ContractPolicy policy)
    {
        _contracts = contracts; _customers = customers; _users = users; _articles = articles;
        _copies = copies; _quotations = quotations; _invoices = invoices; _policy = policy;
    }

    public Task<ContractDetailDto> CreateAsync(ContractRequest request) => WriteAsync(null, request);
    public Task<ContractDetailDto> UpdateAsync(int id, ContractRequest request) => WriteAsync(id, request);

    private async Task<ContractDetailDto> WriteAsync(int? id, ContractRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _policy.Validate();
        var previous = id is null ? null : await RequireContractAsync(id.Value);
        var contract = previous is null
            ? ContractRules.CreateDraft(DateTime.Now, _policy)
            : ContractRules.Clone(previous);
        var customer = await RequireCustomerAsync(request.CustomerId);
        var user = await RequireUserAsync(request.UserId);
        var contact = ResolveContact(customer, request.ContactId);
        contract.ContractNumber = request.Number?.Trim() ?? contract.ContractNumber;
        contract.ContractDate = request.Date ?? contract.ContractDate;
        contract.ExchangeRate = ContractRules.ResolveExchangeRate(contract, _policy.CnyPerUsd ?? 0m);
        contract.Currency = request.Currency is null ? contract.Currency : QuotationRules.NormalizeCurrency(request.Currency);
        contract.DeliveryTerm = previous is null ? request.DeliveryTerm ?? contract.DeliveryTerm : request.DeliveryTerm;
        contract.PaymentTerm = previous is null ? request.PaymentTerm ?? contract.PaymentTerm : request.PaymentTerm;
        contract.BankInformation = request.BankInformation;
        contract.Remarks = request.Remarks;
        contract.LeadTime = previous is null ? request.LeadTime ?? contract.LeadTime : request.LeadTime;
        contract.TermsAndConditions = request.TermsAndConditions;
        contract.CustomerPoNumber = request.CustomerPoNumber;
        contract.CustomerPoDate = request.CustomerPoDate;
        if (previous is not null && previous.Currency != contract.Currency)
        {
            contract.ExchangeRate = CurrencyConversionService.ResolveRate(previous.ExchangeRate, _policy.CnyPerUsd ?? 0m);
            if (request.Items.Count > 0 && contract.ExchangeRate <= 0m)
                throw new InvalidOperationException("A positive exchange rate is required to change the currency of an contract with items.");
        }
        // Existing lines require explicit target-currency prices on a DTO currency
        // change. Desktop applies a confirmed shared conversion plan before saving.
        var items = await BuildItemsAsync(request.Items, contract, previous);
        if (!ContractRules.TryPrepare(contract, false, customer, user, contact, contract.ContractDate,
            contract.CustomerPoDate, contract.Currency, contract.ExchangeRate, items, out var error, out _))
            throw new InvalidOperationException(error);
        ThrowOnError(await SavePreparedAsync(contract, previous is null, contact?.Id));
        return Detail(contract);
    }

    public async Task<ContractDetailDto> ConvertAsync(DocumentSourceType sourceType, int sourceId)
    {
        await ValidateSourceIdentityAsync(sourceType, sourceId);
        var contract = sourceType == DocumentSourceType.Quotation
            ? await _copies.CreateContractFromQuotationAsync(sourceId)
            : await _copies.CreateContractFromInvoiceAsync(sourceId);
        ContractRules.ApplyDraftDefaults(contract, _policy);
        ThrowOnError(await SavePreparedAsync(contract, true));
        return Detail(contract);
    }

    public async Task<string?> SavePreparedAsync(Contract contract, bool isNew, int? contactId = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        _policy.Validate();
        if (isNew ? contract.Id != 0 : contract.Id <= 0) return "Contract identity does not match the requested operation.";
        var previous = isNew ? null : await RequireContractAsync(contract.Id);
        var customer = await RequireCustomerAsync(contract.CustomerId);
        await RequireUserAsync(contract.UserId);
        ResolveContact(customer, contactId);
        if (!customer.IsActive && (previous is null || previous.CustomerId != customer.Id)) return "Customer is inactive.";
        if (string.IsNullOrWhiteSpace(contract.ContractNumber)) return "Document number is required.";
        if (contract.ContractDate == default) return "Date is required.";
        if (!QuotationRules.IsSupportedCurrency(contract.Currency)) return "Unsupported contract currency.";
        if (contract.Currency == "USD" && contract.Items.Count > 0 && contract.ExchangeRate <= 0) return "A valid USD exchange-rate snapshot is required.";
        if (previous is not null && (contract.SourceDocumentType != previous.SourceDocumentType ||
            contract.SourceDocumentId != previous.SourceDocumentId || contract.SourceDocumentNumber != previous.SourceDocumentNumber))
            return "Existing contract source provenance cannot be changed.";
        if (previous is not null && previous.Currency != contract.Currency && contract.Items.Count > 0 && contract.ExchangeRate <= 0m)
            return "A positive exchange rate is required to change currency.";
        if (isNew) await ValidateSourceAsync(contract);
        var seen = new HashSet<int>();
        foreach (var line in contract.Items)
        {
            if (!ContractRules.TryValidateItem(line, out var error)) return error;
            var old = line.Id > 0 ? previous?.Items.FirstOrDefault(i => i.Id == line.Id) : null;
            if (line.Id < 0 || (line.Id > 0 && (old is null || !seen.Add(line.Id))))
                return "Contract item identity is invalid or belongs to another contract.";
            var unchangedLegacy = old is not null && previous is not null
                && previous.Currency == contract.Currency && previous.ExchangeRate == contract.ExchangeRate
                && old.Currency == line.Currency && old.ExchangeRateSnapshot == line.ExchangeRateSnapshot
                && old.Quantity == line.Quantity && old.UnitPrice == line.UnitPrice
                && old.DiscountPercent == line.DiscountPercent && old.SourceArticleId == line.SourceArticleId;
            if (!unchangedLegacy && (line.Currency != contract.Currency || line.ExchangeRateSnapshot != contract.ExchangeRate))
                return $"Currency or exchange-rate snapshot for '{line.ArticleName}' does not match the contract.";
        }
        contract.ContractNumber = contract.ContractNumber.Trim();
        var same = await _contracts.GetContractByNumberAsync(contract.ContractNumber);
        if (same is not null && (isNew || same.Id != contract.Id)) return "Document number already exists.";
        if (isNew) await _contracts.CreateContractAsync(contract);
        else await _contracts.UpdateContractAsync(contract);
        return null;
    }

    private async Task ValidateSourceAsync(Contract contract)
    {
        if (contract.SourceDocumentType == DocumentSourceType.None)
        {
            if (contract.SourceDocumentId is not null || !string.IsNullOrWhiteSpace(contract.SourceDocumentNumber))
                throw new InvalidOperationException("Manual contract cannot carry source provenance.");
            return;
        }
        var id = contract.SourceDocumentId ?? throw new InvalidOperationException("Source document ID is required.");
        await ValidateSourceIdentityAsync(contract.SourceDocumentType, id);
    }

    private async Task ValidateSourceIdentityAsync(DocumentSourceType sourceType, int id)
    {
        switch (sourceType)
        {
            case DocumentSourceType.Quotation:
                if (await _quotations.GetQuotationByIdAsync(id) is null)
                    throw new InvalidOperationException("Source quotation was not found.");
                break;
            case DocumentSourceType.ProformaInvoice:
            case DocumentSourceType.CommercialInvoice:
                var source = await _invoices.GetInvoiceByIdAsync(id)
                    ?? throw new InvalidOperationException("Source invoice was not found.");
                if ((source.Type == InvoiceType.Proforma) != (sourceType == DocumentSourceType.ProformaInvoice))
                    throw new InvalidOperationException("Invoice source type does not match.");
                break;
            default: throw new InvalidOperationException("Unsupported contract source type.");
        }
    }

    private async Task<List<ContractItem>> BuildItemsAsync(IReadOnlyList<ContractItemRequest> requests, Contract contract, Contract? previous)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var items = new List<ContractItem>();
        var seen = new HashSet<int>();
        foreach (var request in requests)
        {
            ArgumentNullException.ThrowIfNull(request);
            ContractItem line;
            if (request.ItemId is int id)
            {
                if (id <= 0 || !seen.Add(id)) throw new InvalidOperationException("Invalid or duplicate contract item ID.");
                var old = previous?.Items.FirstOrDefault(i => i.Id == id)
                    ?? throw new InvalidOperationException("Contract item does not belong to this contract.");
                if (request.ArticleId is not null && request.ArticleId != old.SourceArticleId)
                    throw new InvalidOperationException("Use a new line to change source article.");
                if ((old.Currency != contract.Currency || old.ExchangeRateSnapshot != contract.ExchangeRate) && request.UnitPrice is null)
                    throw new InvalidOperationException("Supply an explicit unit price when changing currency or exchange rate.");
                line = ContractRules.CloneItem(old);
            }
            else if (request.ArticleId is int articleId)
            {
                var article = await _articles.GetArticleByIdAsync(articleId)
                    ?? throw new InvalidOperationException($"Article {articleId} was not found.");
                var price = request.UnitPrice ?? 0m;
                if (request.UnitPrice is null && !ContractRules.TryGetArticleUnitPrice(article, contract.Currency,
                    contract.ExchangeRate, out price, out var error)) throw new InvalidOperationException(error);
                line = ContractRules.CreateItemSnapshot(article, contract.Currency, contract.ExchangeRate, price);
            }
            else
            {
                if (request.UnitPrice is null) throw new InvalidOperationException("Manual contract items require an explicit unit price.");
                line = new ContractItem { ArticleName = request.ArticleName ?? string.Empty, Unit = "PCS" };
            }
            line.ArticleName = request.ArticleName ?? line.ArticleName;
            line.Description = request.Description ?? line.Description;
            line.Specification = request.Specification ?? line.Specification;
            line.Unit = request.Unit ?? line.Unit;
            line.Quantity = request.Quantity;
            line.UnitPrice = request.UnitPrice ?? line.UnitPrice;
            line.DiscountPercent = request.DiscountPercent;
            line.Currency = contract.Currency;
            line.ExchangeRateSnapshot = contract.ExchangeRate;
            line.SortOrder = items.Count;
            items.Add(line);
        }
        return items;
    }

    public async Task DeleteAsync(int id)
    {
        await RequireContractAsync(id);
        await _contracts.DeleteContractAsync(id);
    }
    public async Task<ContractDetailDto?> GetDetailAsync(int id)
    {
        var contract = await _contracts.GetContractByIdAsync(id);
        return contract is null ? null : Detail(contract);
    }
    public async Task<IReadOnlyList<ContractSummaryDto>> GetHistoryAsync(int customerId)
    {
        await RequireCustomerAsync(customerId);
        return (await _contracts.GetContractsByCustomerIdAsync(customerId))
            .OrderByDescending(i => i.ContractDate)
            .ThenByDescending(i => i.Id).Select(Summary).ToList();
    }
    private async Task<Contract> RequireContractAsync(int id)
        => await _contracts.GetContractByIdAsync(id) ?? throw new InvalidOperationException($"Contract {id} was not found.");
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
    private static ContractSummaryDto Summary(Contract i)
        => new(i.Id, i.ContractNumber, i.CustomerId, i.CustomerNameSnapshot, i.ContractDate,
            i.Currency, i.TotalAmount, i.SourceDocumentType, i.SourceDocumentId, i.SourceDocumentNumber);
    private static ContractDetailDto Detail(Contract i)
        => new(Summary(i), i.UserId, i.ExchangeRate, i.CustomerAddressSnapshot, i.CustomerContactSnapshot,
            i.SalesContactNameSnapshot, i.SalesContactEmailSnapshot, i.SalesContactPhoneSnapshot,
            i.DeliveryTerm, i.PaymentTerm, i.BankInformation, i.Remarks, i.LeadTime, i.TermsAndConditions, i.CustomerPoNumber, i.CustomerPoDate,
            i.Items.OrderBy(l => l.SortOrder).ThenBy(l => l.Id).Select(l => new ContractItemDto(l.Id,
                l.SourceArticleId, l.ArticleName, l.Description, l.Specification, l.Quantity, l.Unit, l.UnitPrice,
                l.DiscountPercent, l.Currency, l.ExchangeRateSnapshot, l.LineTotal)).ToList());
}
