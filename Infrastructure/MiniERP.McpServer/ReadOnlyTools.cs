using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using ModelContextProtocol.Server;

namespace MiniERP.McpServer;

[McpServerToolType]
public static class ReadOnlyTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "search_customers", ReadOnly = true),
     Description("Search MiniERP customers by company name or address/country. Returns customer IDs for subsequent quotation lookups. Search follows MiniERP's existing case-sensitive matching.")]
    public static async Task<string> SearchCustomers(
        IServiceScopeFactory scopeFactory,
        [Description("Non-empty search keyword")] string keyword,
        [Description("Maximum results, 1 to 100")] int limit = 25)
    {
        Validate(keyword, limit);
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var matches = (await service.SearchCustomersAsync(keyword.Trim())).ToList();
        var results = matches.Take(limit).Select(c => new
        {
            c.Id, c.Name, c.City, c.State, c.Country, c.IsActive
        }).ToList();
        return JsonSerializer.Serialize(new { totalMatches = matches.Count, returned = results.Count, results }, JsonOptions);
    }

    [McpServerTool(Name = "search_articles", ReadOnly = true),
     Description("Search MiniERP catalog by product name, description, specification or category. Prices are stored catalog values; the catalog does not identify a currency per article, so do not assume USD or quote directly.")]
    public static async Task<string> SearchArticles(
        IServiceScopeFactory scopeFactory,
        [Description("Non-empty product keyword or model")] string keyword,
        [Description("Maximum results, 1 to 100")] int limit = 25)
    {
        Validate(keyword, limit);
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IArticleService>();
        var matches = (await service.SearchArticlesAsync(keyword.Trim())).OrderBy(a => a.Id).ToList();
        var results = matches.Take(limit).Select(a => new
        {
            a.Id, a.Name, a.Name_EN, a.Description, a.Description_EN,
            a.Specification, a.Specs_EN, a.Category, a.Price
        }).ToList();
        return JsonSerializer.Serialize(new { totalMatches = matches.Count, returned = results.Count, priceCurrency = "not specified by article schema", results }, JsonOptions);
    }

    [McpServerTool(Name = "get_customer_quotations", ReadOnly = true),
     Description("List historical quotations for an existing customer ID, newest first. Returns quotation IDs and document currency and totals; use get_quotation for line items.")]
    public static async Task<string> GetCustomerQuotations(
        IServiceScopeFactory scopeFactory,
        [Description("Customer ID from search_customers")] int customerId,
        [Description("Maximum results, 1 to 100")] int limit = 25)
    {
        if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
        ValidateLimit(limit);
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        if (await customers.GetCustomerByIdAsync(customerId) is null)
            return JsonSerializer.Serialize(new { customerId, found = false }, JsonOptions);
        var matches = (await service.GetQuotationsByCustomerIdAsync(customerId))
            .OrderByDescending(q => q.QuotationDate).ThenByDescending(q => q.Id).ToList();
        var results = matches.Take(limit).Select(q => new
        {
            q.Id, q.QuotationNumber, q.QuotationDate, q.ValidUntil,
            q.Currency, q.TotalAmount, itemCount = q.Items.Count,
            q.CustomerNameSnapshot
        }).ToList();
        return JsonSerializer.Serialize(new { customerId, found = true, totalMatches = matches.Count, returned = results.Count, results }, JsonOptions);
    }

    [McpServerTool(Name = "get_quotation", ReadOnly = true),
     Description("Read a quotation by ID, including historical line-item snapshots, currency, price, quantity and discount. Does not recalculate from today's catalog.")]
    public static async Task<string> GetQuotation(
        IServiceScopeFactory scopeFactory,
        [Description("Quotation ID from get_customer_quotations")] int quotationId,
        [Description("Maximum line items, 1 to 100")] int limit = 100)
    {
        if (quotationId <= 0) throw new ArgumentOutOfRangeException(nameof(quotationId));
        ValidateLimit(limit);
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
        var q = await service.GetQuotationByIdAsync(quotationId);
        if (q is null)
            return JsonSerializer.Serialize(new { quotationId, found = false }, JsonOptions);

        var items = q.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Take(limit).Select(i => new
        {
            i.Id, i.SourceArticleId, i.ArticleName, i.Description, i.Specification,
            i.Quantity, i.Unit, i.UnitPrice, i.DiscountPercent, i.Currency,
            i.ExchangeRateSnapshot, i.LineTotal
        }).ToList();
        return JsonSerializer.Serialize(new
        {
            found = true, q.Id, q.QuotationNumber, q.CustomerId, q.CustomerNameSnapshot,
            q.QuotationDate, q.ValidUntil, q.Currency, q.ExchangeRate, q.TotalAmount,
            q.DeliveryTerm, q.LeadTime, q.PaymentTerm,
            totalItems = q.Items.Count, returnedItems = items.Count, items
        }, JsonOptions);
    }

    private static void Validate(string keyword, int limit)
    {
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length > 200)
            throw new ArgumentException("A non-empty keyword of at most 200 characters is required.", nameof(keyword));
        ValidateLimit(limit);
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
    }
}
