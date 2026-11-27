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
