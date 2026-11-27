using MiniERP.ApplicationLayer.Quotations;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

/// <summary>Single quotation write workflow used by the desktop and future adapters.</summary>
public sealed class QuotationWorkflow : IQuotationWorkflow
{
    private readonly IQuotationService _quotations;
    private readonly ICustomerService _customers;
    private readonly IUserService _users;
    private readonly IArticleService _articles;
    private readonly QuotationPolicy _policy;

    public QuotationWorkflow(IQuotationService quotations, ICustomerService customers,
        IUserService users, IArticleService articles, QuotationPolicy policy)
    {
        _quotations = quotations;
        _customers = customers;
        _users = users;
        _articles = articles;
        _policy = policy;
    }

    public async Task<string> PreviewNumberAsync(int userId, DateTime date)
        => await QuotationNumberAllocator.PreviewNextAsync(_quotations, (await RequireUserAsync(userId)).Code, date, _policy);

    public async Task<QuotationDetailDto> CreateAsync(CreateQuotationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _policy.Validate();
        var customer = await RequireCustomerAsync(request.CustomerId);
        var user = await RequireUserAsync(request.UserId);
        var contact = ResolveContact(customer, request.ContactId);
        var q = QuotationRules.CreateDraft(request.QuotationDate ?? DateTime.Now, _policy);
        q.Currency = request.Currency is null ? q.Currency : QuotationRules.NormalizeCurrency(request.Currency);
        q.QuotationNumber = request.QuotationNumber?.Trim() ?? string.Empty;
        q.ValidUntil = request.ValidUntil ?? q.ValidUntil;
        q.DeliveryTerm = request.DeliveryTerm ?? q.DeliveryTerm;
        q.PaymentTerm = request.PaymentTerm ?? q.PaymentTerm;
        q.LeadTime = request.LeadTime ?? q.LeadTime;
        q.Remarks = request.Remarks;
        var items = await BuildItemsAsync(request.Items, q.Currency, q.ExchangeRate, null);
        var manual = request.QuotationNumber is not null;
        Prepare(q, true, manual, customer, user, contact, items);
        ThrowOnError(await SavePreparedAsync(q, true, manual, contact?.Id));
        return Detail(q);
    }

    public async Task<QuotationDetailDto> UpdateAsync(UpdateQuotationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var old = await RequireQuotationAsync(request.QuotationId);
        var customer = await RequireCustomerAsync(request.CustomerId);
        var user = await RequireUserAsync(request.UserId);
        var contact = ResolveContact(customer, request.ContactId);
        var q = QuotationRules.Clone(old);
        q.QuotationNumber = request.QuotationNumber?.Trim() ?? old.QuotationNumber;
        q.QuotationDate = request.QuotationDate;
        q.ValidUntil = request.ValidUntil;
        q.Currency = QuotationRules.NormalizeCurrency(request.Currency);
        // The historical FX snapshot remains the authority. A header-only legacy
        // quote can adopt a configured rate using the same rule as the desktop.
        q.ExchangeRate = QuotationRules.ResolveExchangeRate(old, _policy.CnyPerUsd ?? 0m);
        q.DeliveryTerm = request.DeliveryTerm;
        q.PaymentTerm = request.PaymentTerm;
        q.LeadTime = request.LeadTime;
        q.Remarks = request.Remarks;
        var items = await BuildItemsAsync(request.Items, q.Currency, q.ExchangeRate, old);
        Prepare(q, false, true, customer, user, contact, items);
        ThrowOnError(await SavePreparedAsync(q, false, true, contact?.Id));
        return Detail(q);
    }

    public async Task<QuotationDetailDto> DuplicateAsync(int quotationId)
        => Detail(await DuplicatePreparedAsync(await RequireQuotationAsync(quotationId)));

    public async Task<Quotation> DuplicatePreparedAsync(Quotation snapshot, int? contactId = null)
    {
        var copy = QuotationCopy.CreateCopy(snapshot);
        ThrowOnError(await SavePreparedAsync(copy, true, false, contactId));
        return copy;
    }

    public async Task DeleteAsync(int quotationId)
    {
        await RequireQuotationAsync(quotationId);
        // Repository still prevents deletion when downstream documents exist.
        await _quotations.DeleteQuotationAsync(quotationId);
    }

    public async Task<QuotationDetailDto?> GetDetailAsync(int quotationId)
    {
        var q = await _quotations.GetQuotationByIdAsync(quotationId);
        return q is null ? null : Detail(q);
    }

    public async Task<IReadOnlyList<QuotationSummaryDto>> GetHistoryAsync(int customerId)
    {
        await RequireCustomerAsync(customerId);
        return (await _quotations.GetQuotationsByCustomerIdAsync(customerId))
            .OrderByDescending(q => q.QuotationDate).ThenByDescending(q => q.Id).Select(Summary).ToList();
    }

    public async Task<string?> SavePreparedAsync(Quotation quotation, bool isNew, bool manualNumber, int? contactId = null)
    {
        ArgumentNullException.ThrowIfNull(quotation);
        _policy.Validate();
        if (isNew ? quotation.Id != 0 : quotation.Id <= 0)
            return "Quotation identity does not match the requested operation.";
        var previous = isNew ? null : await RequireQuotationAsync(quotation.Id);
        var customer = await RequireCustomerAsync(quotation.CustomerId);
        var user = await RequireUserAsync(quotation.UserId);
        ResolveContact(customer, contactId);
        if (!customer.IsActive && (previous is null || previous.CustomerId != customer.Id))
            return "Customer is inactive.";
        if (!QuotationRules.TryValidatePrepared(quotation, isNew, manualNumber, out var error))
            return error;
        if (quotation.ValidUntil is { } expiry && expiry.Date < quotation.QuotationDate.Date &&
            (previous is null || previous.ValidUntil != quotation.ValidUntil || previous.QuotationDate != quotation.QuotationDate))
            return "Quotation validity cannot end before the quotation date.";

        var seen = new HashSet<int>();
        foreach (var line in quotation.Items)
        {
            var oldLine = line.Id > 0 ? previous?.Items.FirstOrDefault(i => i.Id == line.Id) : null;
            if (line.Id < 0 || (line.Id > 0 && (oldLine is null || !seen.Add(line.Id))))
                return "Quotation item identity is invalid or belongs to another quotation.";
            // Read/export compatibility for unchanged legacy monetary snapshots;
            // never introduce or change a line with mismatched currency/rate.
            var unchangedLegacy = oldLine is not null && previous is not null
                && previous.Currency == quotation.Currency && previous.ExchangeRate == quotation.ExchangeRate
                && oldLine.Currency == line.Currency && oldLine.ExchangeRateSnapshot == line.ExchangeRateSnapshot
                && oldLine.Quantity == line.Quantity && oldLine.UnitPrice == line.UnitPrice
                && oldLine.DiscountPercent == line.DiscountPercent && oldLine.SourceArticleId == line.SourceArticleId;
            if (!unchangedLegacy && (line.Currency != quotation.Currency || line.ExchangeRateSnapshot != quotation.ExchangeRate))
                return $"Currency or exchange-rate snapshot for '{line.ArticleName}' does not match the quotation.";
        }
        quotation.QuotationNumber = quotation.QuotationNumber.Trim();
        return await PersistAsync(_quotations, quotation, isNew, manualNumber, user.Code, _policy);
    }

    // Low-level persistence is deliberately private; all client writes pass the workflow checks.
    private static async Task<string?> PersistAsync(IQuotationService service, Quotation quotation,
        bool isNew, bool manualNumber, int userCode, QuotationPolicy policy)
    {
        if (isNew && !manualNumber)
        {
            await QuotationNumberAllocator.CreateAsync(service, quotation, userCode, policy);
            return null;
        }
        var existing = await service.GetQuotationByNumberAsync(quotation.QuotationNumber);
        if (existing is not null && (isNew || existing.Id != quotation.Id))
            return "Quotation number already exists.";
        if (isNew) await service.CreateQuotationAsync(quotation);
        else await service.UpdateQuotationAsync(quotation);
        return null;
    }

    private async Task<List<QuotationItem>> BuildItemsAsync(IReadOnlyList<QuotationItemRequest> requests,
        string currency, decimal exchangeRate, Quotation? previous)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (!QuotationRules.IsSupportedCurrency(currency))
            throw new InvalidOperationException($"Unsupported quotation currency: {currency}.");
        var items = new List<QuotationItem>(requests.Count);
        var seen = new HashSet<int>();
        foreach (var input in requests)
        {
            ArgumentNullException.ThrowIfNull(input);
            QuotationItem line;
            if (input.ItemId is int id)
            {
                if (id <= 0 || !seen.Add(id))
                    throw new InvalidOperationException("Invalid or duplicate quotation item ID.");
                var old = previous?.Items.FirstOrDefault(i => i.Id == id)
                    ?? throw new InvalidOperationException("Quotation item does not belong to this quotation.");
                if (input.ArticleId is not null && input.ArticleId != old.SourceArticleId)
                    throw new InvalidOperationException("Use a new line to change the source article.");
                if ((currency != old.Currency || exchangeRate != old.ExchangeRateSnapshot) && input.UnitPrice is null)
                    throw new InvalidOperationException("Supply an explicit unit price when changing a line's currency or exchange-rate snapshot.");
                line = new QuotationItem
                {
                    Id = old.Id, QuotationId = old.QuotationId, SourceArticleId = old.SourceArticleId,
                    ArticleName = old.ArticleName, Description = old.Description, Specification = old.Specification,
                    Unit = old.Unit, UnitPrice = old.UnitPrice,
                    CreatedAt = old.CreatedAt, CreatedBy = old.CreatedBy
                };
            }
            else if (input.ArticleId is int articleId)
            {
                var article = await _articles.GetArticleByIdAsync(articleId)
                    ?? throw new InvalidOperationException($"Article {articleId} was not found.");
                if (input.UnitPrice is decimal explicitPrice)
                    line = QuotationRules.CreateItemSnapshot(article, currency, exchangeRate, explicitPrice);
                else
                {
                    if (!QuotationRules.TryCreateItem(article, currency, exchangeRate, out var priced, out var error))
                        throw new InvalidOperationException(error);
                    line = priced!;
                }
            }
            else
            {
                if (input.UnitPrice is null)
                    throw new InvalidOperationException("Manual quotation items require an explicit unit price.");
                line = new QuotationItem { ArticleName = input.ArticleName ?? string.Empty, Unit = "PCS" };
            }
            line.ArticleName = input.ArticleName ?? line.ArticleName;
            line.Description = input.Description ?? line.Description;
            line.Specification = input.Specification ?? line.Specification;
            line.Unit = input.Unit ?? line.Unit;
            line.Quantity = input.Quantity;
            line.UnitPrice = input.UnitPrice ?? line.UnitPrice;
            line.DiscountPercent = input.DiscountPercent;
            line.Currency = currency;
            line.ExchangeRateSnapshot = exchangeRate;
            line.SortOrder = items.Count;
            items.Add(line);
        }
        return items;
    }

    private static void Prepare(Quotation q, bool isNew, bool manual, Customer customer, User user,
        CustomerContact? contact, IReadOnlyList<QuotationItem> items)
    {
        if (!QuotationRules.TryPrepare(q, isNew, manual, false, customer, user, contact,
            q.QuotationDate, q.ValidUntil, q.Currency, q.ExchangeRate, items, out var error, out _))
            throw new InvalidOperationException(error);
    }

    private async Task<Customer> RequireCustomerAsync(int id)
        => await _customers.GetCustomerByIdAsync(id)
            ?? throw new InvalidOperationException($"Customer {id} was not found.");

    private async Task<User> RequireUserAsync(int id)
        => (await _users.GetAllUsersAsync()).FirstOrDefault(u => u.Id == id)
            ?? throw new InvalidOperationException($"User {id} was not found.");

    private async Task<Quotation> RequireQuotationAsync(int id)
        => await _quotations.GetQuotationByIdAsync(id)
            ?? throw new InvalidOperationException($"Quotation {id} was not found.");

    private static CustomerContact? ResolveContact(Customer customer, int? id)
        => id is null ? null : customer.Contacts.FirstOrDefault(c => c.Id == id.Value)
            ?? throw new InvalidOperationException("Contact does not belong to the selected customer.");

    private static void ThrowOnError(string? error)
    {
        if (error is not null) throw new InvalidOperationException(error);
    }

    private static QuotationSummaryDto Summary(Quotation q)
        => new(q.Id, q.QuotationNumber, q.CustomerId, q.CustomerNameSnapshot, q.QuotationDate,
            q.ValidUntil, q.Currency, q.TotalAmount);

    private static QuotationDetailDto Detail(Quotation q)
        => new(Summary(q), q.UserId, q.ExchangeRate, q.CustomerAddressSnapshot, q.CustomerContactSnapshot,
            q.SalesContactNameSnapshot, q.SalesContactPhoneSnapshot, q.SalesContactEmailSnapshot,
            q.DeliveryTerm, q.PaymentTerm, q.LeadTime, q.Remarks, q.Items.OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id).Select(i => new QuotationItemDto(i.Id, i.SourceArticleId, i.ArticleName,
                i.Description, i.Specification, i.Quantity, i.Unit, i.UnitPrice, i.DiscountPercent,
                i.Currency, i.ExchangeRateSnapshot, i.LineTotal)).ToList());
}
