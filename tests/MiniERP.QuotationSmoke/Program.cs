using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

var date = new DateTime(2026, 11, 27);
var draft = QuotationRules.CreateDraft(date, 30, 7m);
Check(draft.ValidUntil == date.AddDays(30), "default validity");
Check(draft.Currency == "USD" && draft.ExchangeRate == 7m, "currency and rate defaults");
Check(draft.DeliveryTerm == "FCA Baoding" && draft.PaymentTerm == "100% prepayment", "commercial defaults");
Check(QuotationRules.NormalizeValidityDays(0) == 30, "invalid default validity");
Check(QuotationRules.FollowsDefaultValidity(true, date, date.AddDays(30), 30), "default validity follows date");
Check(!QuotationRules.FollowsDefaultValidity(true, date, date.AddDays(15), 30), "custom validity preserved");

var article = new Article { Id = 5, Name = "RK3568", Name_EN = "Board", Description_EN = "English description", Price = 700m };
Check(QuotationRules.TryCreateItem(article, "USD", 7m, out var item, out _), "create item");
var line = item!;
Check(line.SourceArticleId == 5 && line.ArticleName == "Board" && line.Description == "English description" && line.UnitPrice == 100m, "catalog snapshot");
line.Quantity = 3;
line.DiscountPercent = 10;
Check(line.LineTotal == 270m, "discount and quantity calculation");
Check(QuotationItem.CalculateLineTotal(1m, 1.005m, 0m) == 1.01m, "round away from zero");
Check(!QuotationRules.TryGetArticleUnitPrice(article, "USD", 0m, out _, out _), "missing USD rate");
Check(QuotationRules.TryGetArticleUnitPrice(article, "CNY", 0m, out var cny, out _) && cny == 700m, "CNY without exchange rate");
Check(!QuotationRules.TryValidatePosition("Board", 0m, "PCS", 100m, 0m, out _), "invalid quantity");
Check(!QuotationRules.TryValidatePosition("Board", 1m, "PCS", 100m, 101m, out _), "invalid discount");
Check(!QuotationRules.TryValidatePosition("Board", 1m, "PCS", null, 0m, out _), "invalid price input");
article.Price = 770m;
Check(line.UnitPrice == 100m, "catalog update does not change historical line");
Check(QuotationRules.TryPlanRepricing(new[] { line }, new[] { article }, "CNY", 7m, out var prices, out _) && prices.Single() == 770m, "explicit repricing");
Check(!QuotationRules.TryPlanRepricing(new[] { line, new QuotationItem { ArticleName = "Manual" } }, new[] { article }, "CNY", 7m, out var failedPrices, out _) && failedPrices.Count == 0 && line.UnitPrice == 100m, "failed repricing is atomic");

var customer = new Customer { Id = 1, Name = "Test customer", Country = "DE" };
var user = new User { Id = 2, Code = 3, Name = "Test sales" };
Check(QuotationRules.TryPrepare(draft, true, false, false, customer, user, null, date, draft.ValidUntil,
    draft.Currency, draft.ExchangeRate, new[] { line }, out _, out _), "prepare without UI");
Check(draft.CustomerNameSnapshot == "Test customer" && draft.SalesContactNameSnapshot == "Test sales", "header snapshots");
Check(draft.Items.Single().UnitPrice == 100m && draft.TotalAmount == 270m, "prepared totals");
Check(QuotationRules.ResolveExchangeRate(draft, 8m) == 7m, "stored rate preserved");
Check(!QuotationRules.CanRefreshExchangeRate(true, 1), "do not refresh populated draft rate");

var service = new MemoryQuotationService();
Check(await QuotationNumberAllocator.PreviewNextAsync(service, 3, date) == "26INTPAN1127301", "legacy number format");
draft.QuotationNumber = "26INTPAN1127301";
draft.Id = 1;
customer.Name = "Renamed customer";
Check(QuotationRules.TryPrepare(draft, false, false, false, customer, user, null, date, draft.ValidUntil,
    draft.Currency, draft.ExchangeRate, draft.Items.ToList(), out _, out _), "prepare existing");
Check(draft.CustomerNameSnapshot == "Test customer", "historical customer snapshot preserved");
var duplicate = QuotationCopy.CreateCopy(draft);
Check(duplicate.Id == 0 && duplicate.Items.Single().Id == 0 && duplicate.QuotationNumber == string.Empty, "copy clears identities");
Check(duplicate.Items.Single().UnitPrice == 100m && duplicate.ValidUntil == draft.ValidUntil, "copy preserves historical terms");
var policy = new QuotationPolicy { NumberCode = "ACME", DefaultCurrency = "CNY", DefaultValidityDays = 45, DeliveryTerm = "EXW" };
var custom = QuotationRules.CreateDraft(date, policy);
Check(custom.Currency == "CNY" && custom.ValidUntil == date.AddDays(45) && custom.DeliveryTerm == "EXW", "custom policy defaults");
Check(await QuotationNumberAllocator.PreviewNextAsync(service, 3, date, policy) == "26ACME1127301", "custom number code");
// Test the shared conversion planner independently from Avalonia or SQLite.
var conversionLines = new[]
{
    new CurrencyConversionLine("Negotiated board", 5, 3m, "PCS", 100m, 10m, "USD", 7m),
    new CurrencyConversionLine("Manual service", null, 2m, "PCS", 25m, 0m, "USD", 7m)
};
Check(CurrencyConversionService.TryPlan("USD", "CNY", 7m,
    CurrencyConversionMode.ConvertExistingPrices, conversionLines, new[] { article }, out var converted, out _),
    "convert catalog and manual lines together");
Check(converted!.OldTotal == 320m && converted.NewTotal == 2240m &&
    converted.Lines[0].NewUnitPrice == 700m && converted.Lines[1].NewUnitPrice == 175m,
    "negotiated prices, discount and totals preserved by conversion");
Check(conversionLines[0].UnitPrice == 100m && conversionLines[1].Currency == "USD",
    "preview does not mutate its input");
Check(!CurrencyConversionService.TryPlan("USD", "CNY", 7m, CurrencyConversionMode.RepriceFromCatalog,
    conversionLines, new[] { article }, out var failed, out _) && failed is null,
    "catalog repricing fails atomically for a manual line");
Check(CurrencyConversionService.TryPlan("USD", "CNY", 7m, CurrencyConversionMode.RepriceFromCatalog,
    new[] { conversionLines[0] }, new[] { article }, out var catalogPlan, out _) &&
    catalogPlan!.Lines[0].NewUnitPrice == 770m && catalogPlan.NewTotal == 2079m,
    "explicit catalog repricing uses current catalog");
Check(CurrencyConversionService.TryPlan("USD", "CNY", 7m, CurrencyConversionMode.KeepNumericPrices,
    conversionLines, new[] { article }, out var numeric, out _) && numeric!.NewTotal == 320m,
    "keep numeric mode changes currency without repricing");
var reverse = conversionLines.Select((l, index) => l with
    { Currency = "CNY", UnitPrice = converted.Lines[index].NewUnitPrice }).ToList();
Check(CurrencyConversionService.TryPlan("CNY", "USD", 7m, CurrencyConversionMode.ConvertExistingPrices,
    reverse, Array.Empty<Article>(), out var reversed, out _) && reversed!.NewTotal == 320m,
    "reverse conversion");
Check(CurrencyConversionService.TryPlan("CNY", "USD", 8m, CurrencyConversionMode.ConvertExistingPrices,
    new[] { new CurrencyConversionLine("Rounding", null, 1m, "PCS", 8.04m, 0m, "CNY", 8m) },
    Array.Empty<Article>(), out var rounded, out _) && rounded!.NewTotal == 1.01m, "half-up unit-price rounding");
Check(!CurrencyConversionService.TryPlan("USD", "CNY", 0m, CurrencyConversionMode.ConvertExistingPrices,
    conversionLines, Array.Empty<Article>(), out _, out _), "invalid exchange rate blocked");
Check(CurrencyConversionService.TryPlan("USD", "CNY", 0m, CurrencyConversionMode.ConvertExistingPrices,
    Array.Empty<CurrencyConversionLine>(), Array.Empty<Article>(), out var empty, out _) && empty!.NewTotal == 0m,
    "empty document currency switch without rate");
Check(!CurrencyConversionService.TryPlan("USD", "CNY", 7m, CurrencyConversionMode.ConvertExistingPrices,
    new[] { conversionLines[0] with { Currency = "CNY" } }, Array.Empty<Article>(), out _, out _),
    "mixed currencies rejected");
Check(!CurrencyConversionService.TryPlan("USD", "EUR", 7m, CurrencyConversionMode.ConvertExistingPrices,
    conversionLines, Array.Empty<Article>(), out _, out _), "unsupported currency rejected");
Check(!CurrencyConversionService.TryPlan("USD", "CNY", 7m, CurrencyConversionMode.ConvertExistingPrices,
    new[] { conversionLines[0] with { UnitPrice = decimal.MaxValue } }, Array.Empty<Article>(), out var overflow, out _) && overflow is null,
    "overflow rejected atomically");
Check(CurrencyConversionService.ResolveRate(7m, 8m) == 7m && CurrencyConversionService.ResolveRate(0m, 8m) == 8m,
    "prefer historical snapshot and use settings only as fallback");
Console.WriteLine("PASS: shared currency conversion planner");
Console.WriteLine("PASS: headless quotation rules, pricing, snapshots, numbering, copy and policy");

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
}

sealed class MemoryQuotationService : IQuotationService
{
    public List<Quotation> Records { get; } = new();
    public Task<IEnumerable<Quotation>> GetAllQuotationsAsync() => Task.FromResult(Records.AsEnumerable());
    public Task<Quotation?> GetQuotationByIdAsync(int id) => Task.FromResult(Records.FirstOrDefault(x => x.Id == id));
    public Task<Quotation?> GetQuotationByNumberAsync(string quotationNumber) => Task.FromResult(Records.FirstOrDefault(x => x.QuotationNumber == quotationNumber));
    public Task<IReadOnlyList<string>> GetQuotationNumbersByPrefixAsync(string prefix) =>
        Task.FromResult<IReadOnlyList<string>>(Records.Select(x => x.QuotationNumber).Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToList());
    public Task<IEnumerable<Quotation>> GetQuotationsByCustomerIdAsync(int customerId) => Task.FromResult(Records.Where(x => x.CustomerId == customerId));
    public Task CreateQuotationAsync(Quotation quotation)
    {
        quotation.Id = Records.Count + 1;
        Records.Add(quotation);
        return Task.CompletedTask;
    }
    public Task UpdateQuotationAsync(Quotation quotation) => Task.CompletedTask;
    public Task DeleteQuotationAsync(int id) { Records.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
}
