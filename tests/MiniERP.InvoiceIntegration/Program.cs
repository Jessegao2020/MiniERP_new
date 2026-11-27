using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Invoices;
using MiniERP.ApplicationLayer.Quotations;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.Infrastructure;
using MiniERP.Infrastructure.Data;

var folder = Path.Combine(Path.GetTempPath(), "minierp-invoice-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);

try
{
    var services = new ServiceCollection();
    services.AddInfrastructure(Connection("erp.db"), Connection("catalog.db"));
    services.AddApplication();
    services.AddSingleton(new QuotationPolicy { CnyPerUsd = 7m });
    var policy = new InvoicePolicy { ProformaPrefix = "PRO", CommercialPrefix = "COM", CnyPerUsd = 7m, DeliveryTerm = "EXW", PaymentTerm = "50% advance" };
    services.AddSingleton(policy);
    using var provider = services.BuildServiceProvider();
    int customerId, otherContactId, contactId, userId, articleId;
    using (var scope = provider.CreateScope())
    {
        var erp = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await erp.Database.EnsureCreatedAsync();
        await catalog.Database.EnsureCreatedAsync();
        var customer = new Customer { Name = "Test customer", Country = "DE" };
        var contact = new CustomerContact { Name = "Alice", Title = "Ms." };
        customer.Contacts.Add(contact);
        var other = new Customer { Name = "Other customer" };
        var otherContact = new CustomerContact { Name = "Bob" };
        other.Contacts.Add(otherContact);
        var user = new User { Name = "Test salesperson", Code = 3 };
        erp.Customers.AddRange(customer, other);
        erp.Users.Add(user);
        await erp.SaveChangesAsync();
        var article = new Article { Name = "RK3568", Name_EN = "Board", Price = 700m, Description_EN = "Test board" };
        catalog.Articles.Add(article);
        await catalog.SaveChangesAsync();
        customerId = customer.Id; otherContactId = otherContact.Id; contactId = contact.Id;
        userId = user.Id; articleId = article.Id;
    }

    var date = new DateTime(2026, 1, 2, 12, 34, 56, 789);
    Check(InvoiceRules.NewNumber(InvoiceType.Proforma, date, policy) == "PRO-20260102-123456789", "configured PI prefix and legacy format");
    Check(InvoiceRules.NewNumber(InvoiceType.Commercial, date, policy) == "COM-20260102-123456789", "configured commercial prefix");
    Check(!InvoiceRules.TryChangeCurrency("CNY", 1, out _) && InvoiceRules.TryChangeCurrency("CNY", 0, out _), "currency change rule");
    Check(SalesLineAmounts.Calculate(1m, 1.005m, 0m) == 1.01m, "shared line rounding");
    var cnyLegacy = new Invoice { Currency = "CNY", ExchangeRate = 0m };
    cnyLegacy.Items.Add(new InvoiceItem { Currency = "CNY", ExchangeRateSnapshot = 0m });
    Check(InvoiceRules.ResolveExchangeRate(cnyLegacy, 7m) == 0m, "historical CNY rate not replaced");

    var request = new InvoiceRequest(InvoiceType.Proforma, customerId, userId,
        new InvoiceItemRequest[] { new(articleId, 3m, DiscountPercent: 10m), new(null, 2m, UnitPrice: 25m, ArticleName: "Service") },
        ContactId: contactId, Date: date, BankInformation: "Test bank", CustomerPoNumber: "PO-001", CustomerPoDate: date.AddDays(-1));
    var pi = await WithInvoice(w => w.CreateAsync(request));
    var id = pi.Summary.Id;
    var firstId = pi.Items[0].Id;
    var secondId = pi.Items[1].Id;
    Check(id > 0 && firstId > 0 && secondId > 0 && firstId != secondId, "real invoice and line identities");
    Check(pi.Summary.Type == InvoiceType.Proforma && pi.Summary.Number.StartsWith("PRO-") && pi.Summary.Date == date, "PI number and business date");
    Check(pi.Summary.Total == 320m && pi.Items[0].UnitPrice == 100m && pi.ExchangeRate == 7m, "catalog conversion and manual line");
    Check(pi.DeliveryTerm == "EXW" && pi.PaymentTerm == "50% advance" && pi.CustomerContact == "Ms. Alice", "policy and contact");
    Check(pi.BankInformation == "Test bank" && pi.CustomerPoNumber == "PO-001", "bank and PO fields");
    var loaded = await WithInvoice(w => w.GetDetailAsync(id));
    Check(loaded is not null && loaded.Summary.Total == 320m && loaded.Items[0].Id == firstId, "read back from fresh SQLite scope");

    await Reject("wrong contact", () => WithInvoice(w => w.CreateAsync(request with { ContactId = otherContactId })), "Contact");
    await Reject("unknown customer", () => WithInvoice(w => w.CreateAsync(request with { CustomerId = int.MaxValue })), "Customer");
    await Reject("unknown salesperson", () => WithInvoice(w => w.CreateAsync(request with { UserId = int.MaxValue })), "User");
    await Reject("unknown article", () => WithInvoice(w => w.CreateAsync(request with { Items = new[] { new InvoiceItemRequest(int.MaxValue, 1m) } })), "Article");
    await Reject("invalid quantity", () => WithInvoice(w => w.CreateAsync(request with { Items = new[] { new InvoiceItemRequest(articleId, 0m) } })), "Quantity");
    await Reject("invalid price", () => WithInvoice(w => w.CreateAsync(request with { Items = new[] { new InvoiceItemRequest(articleId, 1m, -1m) } })), "price");
    await Reject("invalid discount", () => WithInvoice(w => w.CreateAsync(request with { Items = new[] { new InvoiceItemRequest(articleId, 1m, DiscountPercent: 101m) } })), "Discount");
    await Reject("duplicate number", () => WithInvoice(w => w.CreateAsync(request with { Number = pi.Summary.Number })), "already exists");
    Check((await WithInvoice(w => w.GetHistoryAsync(customerId))).Count == 1, "rejected writes leave no records");

    // Create a real source quotation before changing the catalog price/customer.
    var quote = await WithQuotation(w => w.CreateAsync(new CreateQuotationRequest(customerId, userId,
        new[] { new QuotationItemRequest(articleId, 2m) }, ContactId: contactId, QuotationDate: date)));
    using (var scope = provider.CreateScope())
    {
        var erp = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        (await erp.Customers.SingleAsync(c => c.Id == customerId)).Name = "Renamed customer";
        (await catalog.Articles.SingleAsync(a => a.Id == articleId)).Price = 1400m;
        await erp.SaveChangesAsync();
        await catalog.SaveChangesAsync();
    }

    var update = request with
    {
        Items = new InvoiceItemRequest[]
        {
            new(articleId, 4m, DiscountPercent: 10m, ItemId: firstId),
            new(articleId, 1m)
        },
        ContactId = null, Number = pi.Summary.Number, DeliveryTerm = pi.DeliveryTerm, PaymentTerm = pi.PaymentTerm
    };
    var updated = await WithInvoice(w => w.UpdateAsync(id, update));
    var addedId = updated.Items[1].Id;
    Check(updated.Items[0].Id == firstId && updated.Items[0].UnitPrice == 100m
        && updated.Items[1].UnitPrice == 200m && addedId > 0 && addedId != firstId
        && updated.Items.All(i => i.Id != secondId), "update retains historical price, inserts/deletes lines");
    Check(updated.Summary.Total == 560m && updated.Summary.CustomerName == "Test customer"
        && updated.CustomerContact == "Ms. Alice", "old customer/contact snapshot retained");
    var repeat = update with { Items = new[] { update.Items[0], update.Items[1] with { ItemId = addedId } } };
    var repeated = await WithInvoice(w => w.UpdateAsync(id, repeat));
    Check(repeated.Items.Select(i => i.Id).SequenceEqual(new[] { firstId, addedId }), "stable line IDs across repeated saves");
    await Reject("type change", () => WithInvoice(w => w.UpdateAsync(id, repeat with { Type = InvoiceType.Commercial })), "conversion");
    await Reject("currency change with lines", () => WithInvoice(w => w.UpdateAsync(id, repeat with { Currency = "CNY" })), "Remove invoice items");
    await Reject("duplicate line ID", () => WithInvoice(w => w.UpdateAsync(id, repeat with { Items = new[] { repeat.Items[0], repeat.Items[0] } })), "duplicate");

    // Actual quotation -> PI -> commercial invoice chain. Conversion must use
    // source snapshots, not re-query current product pricing.
    var fromQuote = await WithInvoice(w => w.ConvertAsync(DocumentSourceType.Quotation, quote.Summary.Id, InvoiceType.Proforma));
    Check(fromQuote.Summary.SourceType == DocumentSourceType.Quotation && fromQuote.Summary.SourceId == quote.Summary.Id
        && fromQuote.Summary.SourceNumber == quote.Summary.Number && fromQuote.Items[0].UnitPrice == 100m
        && fromQuote.Summary.CustomerName == "Test customer", "quotation to PI preserves provenance and snapshots");
    var commercial = await WithInvoice(w => w.ConvertAsync(DocumentSourceType.ProformaInvoice, fromQuote.Summary.Id, InvoiceType.Commercial));
    Check(commercial.Summary.Type == InvoiceType.Commercial && commercial.Summary.Number.StartsWith("COM-")
        && commercial.Summary.SourceId == fromQuote.Summary.Id && commercial.Items[0].UnitPrice == 100m, "PI to commercial invoice");
    await Reject("wrong source type", () => WithInvoice(w => w.ConvertAsync(DocumentSourceType.CommercialInvoice, fromQuote.Summary.Id, InvoiceType.Commercial)), "source type");
    await Reject("foreign line identity", () => WithInvoice(w => w.UpdateAsync(id, repeat with
        { Items = new[] { new InvoiceItemRequest(articleId, 1m, ItemId: commercial.Items[0].Id) } })), "belong");
    Check((await WithInvoice(w => w.GetHistoryAsync(customerId, InvoiceType.Proforma))).Count == 2
        && (await WithInvoice(w => w.GetHistoryAsync(customerId, InvoiceType.Commercial))).Count == 1, "separate PI/commercial histories");

    using (var scope = provider.CreateScope())
    {
        var workflow = scope.ServiceProvider.GetRequiredService<IInvoiceWorkflow>();
        var crud = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var bad = (await crud.GetInvoiceByIdAsync(id))!;
        bad.Items.First().Currency = "CNY";
        Check((await workflow.SavePreparedAsync(bad, false))?.Contains("Currency") == true, "desktop path rejects mismatched currency");
        bad.Items.First().Currency = "USD";
        bad.Items.First().ExchangeRateSnapshot = 99m;
        Check((await workflow.SavePreparedAsync(bad, false))?.Contains("exchange-rate") == true, "desktop path rejects mismatched rate");
    }

    await Reject("PI downstream guard", () => WithInvoice(async w => { await w.DeleteAsync(fromQuote.Summary.Id); return true; }), "downstream");
    await Reject("quotation downstream guard", () => WithQuotation(async w => { await w.DeleteAsync(quote.Summary.Id); return true; }), "downstream");
    await WithInvoice(async w => { await w.DeleteAsync(commercial.Summary.Id); return true; });
    await WithInvoice(async w => { await w.DeleteAsync(fromQuote.Summary.Id); return true; });
    await WithInvoice(async w => { await w.DeleteAsync(id); return true; });
    Check((await WithInvoice(w => w.GetHistoryAsync(customerId))).Count == 0, "delete in reverse dependency order");
    Console.WriteLine("PASS: PI and commercial invoice SQLite integration");

    string Connection(string name) => new SqliteConnectionStringBuilder
    { DataSource = Path.Combine(folder, name), ForeignKeys = true, Pooling = false }.ToString();

    async Task<T> WithInvoice<T>(Func<IInvoiceWorkflow, Task<T>> action)
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IInvoiceWorkflow>());
    }
    async Task<T> WithQuotation<T>(Func<IQuotationWorkflow, Task<T>> action)
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IQuotationWorkflow>());
    }
}
finally { Directory.Delete(folder, recursive: true); }

static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
}
static async Task Reject(string label, Func<Task> action, string expected)
{
    try { await action(); }
    catch (InvalidOperationException ex) when (ex.Message.Contains(expected, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("PASS: " + label);
        return;
    }
    throw new InvalidOperationException("FAIL: expected rejection for " + label);
}
