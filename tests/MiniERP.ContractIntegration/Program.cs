using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Contracts;
using MiniERP.ApplicationLayer.Invoices;
using MiniERP.ApplicationLayer.Quotations;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.Infrastructure;
using MiniERP.Infrastructure.Data;

var folder = Path.Combine(Path.GetTempPath(), "minierp-contract-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    var services = new ServiceCollection();
    services.AddInfrastructure(Connection("erp.db"), Connection("catalog.db"));
    services.AddApplication();
    services.AddSingleton(new QuotationPolicy { CnyPerUsd = 7m });
    services.AddSingleton(new InvoicePolicy { CnyPerUsd = 7m });
    var policy = new ContractPolicy { NumberPrefix = "AGR", CnyPerUsd = 7m,
        DeliveryTerm = "EXW", PaymentTerm = "50% advance", LeadTime = "3 weeks" };
    services.AddSingleton(policy);
    using var provider = services.BuildServiceProvider();
    int customerId, otherContactId, contactId, userId, articleId, inactiveId;
    using (var scope = provider.CreateScope())
    {
        var erp = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await erp.Database.EnsureCreatedAsync();
        await catalog.Database.EnsureCreatedAsync();
        var customer = new Customer { Name = "Test customer", Country = "DE" };
        var contact = new CustomerContact { Name = "Alice", Title = "Ms." };
        customer.Contacts.Add(contact);
        var other = new Customer { Name = "Other" };
        var otherContact = new CustomerContact { Name = "Bob" };
        other.Contacts.Add(otherContact);
        var inactive = new Customer { Name = "Inactive", IsActive = false };
        var user = new User { Name = "Test salesperson", Code = 3 };
        erp.Customers.AddRange(customer, other, inactive);
        erp.Users.Add(user);
        await erp.SaveChangesAsync();
        var article = new Article { Name = "RK3568", Name_EN = "Board", Price = 700m, Description_EN = "Test board" };
        catalog.Articles.Add(article);
        await catalog.SaveChangesAsync();
        customerId = customer.Id; inactiveId = inactive.Id; contactId = contact.Id;
        otherContactId = otherContact.Id; userId = user.Id; articleId = article.Id;
    }

    var date = new DateTime(2026, 1, 2, 12, 34, 56, 789);
    Check(ContractRules.NewNumber(date, new ContractPolicy()) == "CON-20260102-123456789", "legacy number format");
    Check(ContractRules.NewNumber(date, policy) == "AGR-20260102-123456789", "configured number prefix");
    var legacy = new Contract { Currency = "CNY", ExchangeRate = 0m };
    legacy.Items.Add(new ContractItem { Currency = "CNY", ExchangeRateSnapshot = 0m });
    Check(ContractRules.ResolveExchangeRate(legacy, 7m) == 0m, "historical CNY rate remains unchanged");
    Check(!ContractRules.TryPrepare(ContractRules.CreateDraft(date, policy), true, null, null, null,
        date, null, "USD", 7m, Array.Empty<ContractItem>(), out _, out _), "export preparation requires customer");

    var request = new ContractRequest(customerId, userId,
        new ContractItemRequest[] { new(articleId, 3m, DiscountPercent: 10m),
            new(null, 2m, UnitPrice: 25m, ArticleName: "Service") },
        ContactId: contactId, Date: date, BankInformation: "Test bank",
        Remarks: "Test remarks", TermsAndConditions: "Warranty: 12 months.\nSpecial clause.",
        CustomerPoNumber: "PO-001", CustomerPoDate: date.AddDays(-1));
    var original = await WithContract(w => w.CreateAsync(request));
    var id = original.Summary.Id;
    var firstId = original.Items[0].Id;
    var secondId = original.Items[1].Id;
    Check(id > 0 && firstId > 0 && secondId > 0 && firstId != secondId, "SQLite assigned identities");
    Check(original.Summary.Number.StartsWith("AGR-") && original.Summary.Date == date, "number and business date");
    Check(original.Summary.Total == 320m && original.Items[0].UnitPrice == 100m && original.ExchangeRate == 7m,
        "catalog price conversion, discount and manual line");
    Check(original.DeliveryTerm == "EXW" && original.PaymentTerm == "50% advance" && original.LeadTime == "3 weeks",
        "company policy defaults");
    Check(original.CustomerContact == "Ms. Alice" && original.BankInformation == "Test bank" &&
        original.TermsAndConditions == request.TermsAndConditions && original.Remarks == request.Remarks &&
        original.CustomerPoNumber == "PO-001" && original.CustomerPoDate == request.CustomerPoDate, "contract-specific fields");
    Check((await WithContract(w => w.GetDetailAsync(id)))?.TermsAndConditions == request.TermsAndConditions, "clauses read from fresh scope");
    await Reject("foreign contact", () => WithContract(w => w.CreateAsync(request with { ContactId = otherContactId })), "Contact");
    await Reject("inactive customer", () => WithContract(w => w.CreateAsync(request with { CustomerId = inactiveId, ContactId = null })), "inactive");
    await Reject("missing customer", () => WithContract(w => w.CreateAsync(request with { CustomerId = int.MaxValue })), "Customer");
    await Reject("missing salesperson", () => WithContract(w => w.CreateAsync(request with { UserId = int.MaxValue })), "User");
    await Reject("missing article", () => WithContract(w => w.CreateAsync(request with { Items = new[] { new ContractItemRequest(int.MaxValue, 1m) } })), "Article");
    await Reject("negative price", () => WithContract(w => w.CreateAsync(request with { Items = new[] { new ContractItemRequest(articleId, 1m, -1m) } })), "price");
    await Reject("zero quantity", () => WithContract(w => w.CreateAsync(request with { Items = new[] { new ContractItemRequest(articleId, 0m) } })), "Quantity");
    await Reject("invalid discount", () => WithContract(w => w.CreateAsync(request with { Items = new[] { new ContractItemRequest(articleId, 1m, DiscountPercent: 101m) } })), "Discount");
    await Reject("manual item needs price", () => WithContract(w => w.CreateAsync(request with { Items = new[] { new ContractItemRequest(null, 1m, ArticleName: "Manual") } })), "price");
    await Reject("duplicate number", () => WithContract(w => w.CreateAsync(request with { Number = original.Summary.Number })), "already exists");
    Check((await WithContract(w => w.GetHistoryAsync(customerId))).Count == 1, "rejected writes leave no records");

    var quote = await WithQuotation(w => w.CreateAsync(new CreateQuotationRequest(customerId, userId,
        new[] { new QuotationItemRequest(articleId, 2m) }, ContactId: contactId, QuotationDate: date)));
    var pi = await WithInvoice(w => w.CreateAsync(new InvoiceRequest(InvoiceType.Proforma, customerId, userId,
        new[] { new InvoiceItemRequest(articleId, 2m) }, ContactId: contactId, BankInformation: "Source bank",
        CustomerPoNumber: "SOURCE-PO", CustomerPoDate: date, Number: "PI-SOURCE-001")));
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
        Number = original.Summary.Number, ContactId = null,
        DeliveryTerm = original.DeliveryTerm, PaymentTerm = original.PaymentTerm, LeadTime = original.LeadTime,
        TermsAndConditions = "Revised clause",
        Items = new ContractItemRequest[] { new(articleId, 4m, DiscountPercent: 10m, ItemId: firstId), new(articleId, 1m) }
    };
    var updated = await WithContract(w => w.UpdateAsync(id, update));
    var addedId = updated.Items[1].Id;
    Check(updated.Items[0].Id == firstId && updated.Items[0].UnitPrice == 100m &&
        updated.Items[1].UnitPrice == 200m && addedId > 0 && addedId != firstId &&
        updated.Items.All(l => l.Id != secondId), "update preserves old price and returns inserted IDs");
    Check(updated.Summary.Total == 560m && updated.Summary.CustomerName == "Test customer" &&
        updated.CustomerContact == "Ms. Alice" && updated.TermsAndConditions == "Revised clause", "historical snapshots and edited clauses");
    var repeat = update with { Items = new[] { update.Items[0], update.Items[1] with { ItemId = addedId } } };
    var repeated = await WithContract(w => w.UpdateAsync(id, repeat));
    Check(repeated.Items.Select(l => l.Id).SequenceEqual(new[] { firstId, addedId }), "stable IDs across repeated saves");
    await Reject("duplicate item ID", () => WithContract(w => w.UpdateAsync(id, repeat with { Items = new[] { repeat.Items[0], repeat.Items[0] } })), "duplicate");
    await Reject("currency requires explicit target price", () => WithContract(w => w.UpdateAsync(id, repeat with { Currency = "CNY" })), "explicit unit price");

    var fromQuote = await WithContract(w => w.ConvertAsync(DocumentSourceType.Quotation, quote.Summary.Id));
    Check(fromQuote.Summary.SourceType == DocumentSourceType.Quotation &&
        fromQuote.Summary.SourceId == quote.Summary.Id && fromQuote.Summary.SourceNumber == quote.Summary.Number &&
        fromQuote.Summary.Number.StartsWith("AGR-") && fromQuote.Items[0].UnitPrice == 100m &&
        fromQuote.Summary.CustomerName == "Test customer", "quotation conversion keeps original snapshots");
    var fromPi = await WithContract(w => w.ConvertAsync(DocumentSourceType.ProformaInvoice, pi.Summary.Id));
    Check(fromPi.Summary.SourceType == DocumentSourceType.ProformaInvoice &&
        fromPi.Summary.SourceId == pi.Summary.Id && fromPi.Items[0].UnitPrice == 100m &&
        fromPi.BankInformation == "Source bank" && fromPi.CustomerPoNumber == "SOURCE-PO",
        "PI conversion keeps source price, bank and PO");
    await Reject("incorrect source type", () => WithContract(w => w.ConvertAsync(DocumentSourceType.CommercialInvoice, pi.Summary.Id)), "source type");
    await Reject("foreign line identity", () => WithContract(w => w.UpdateAsync(id, repeat with
    { Items = new[] { new ContractItemRequest(articleId, 1m, ItemId: fromPi.Items[0].Id) } })), "belong");

    using (var scope = provider.CreateScope())
    {
        var workflow = scope.ServiceProvider.GetRequiredService<IContractWorkflow>();
        var crud = scope.ServiceProvider.GetRequiredService<IContractService>();
        var bad = (await crud.GetContractByIdAsync(id))!;
        bad.Items.First().Currency = "CNY";
        Check((await workflow.SavePreparedAsync(bad, false))?.Contains("Currency") == true, "prepared path rejects mixed currency");
        bad.Items.First().Currency = "USD";
        bad.Items.First().ExchangeRateSnapshot = 99m;
        Check((await workflow.SavePreparedAsync(bad, false))?.Contains("exchange-rate") == true, "prepared path rejects mismatched rate");
    }
    using (var scope = provider.CreateScope())
    {
        var workflow = scope.ServiceProvider.GetRequiredService<IContractWorkflow>();
        var crud = scope.ServiceProvider.GetRequiredService<IContractService>();
        var contract = (await crud.GetContractByIdAsync(id))!;
        var lines = contract.Items.OrderBy(l => l.SortOrder).ToList();
        Check(CurrencyConversionService.TryPlan("USD", "CNY", contract.ExchangeRate,
            CurrencyConversionMode.ConvertExistingPrices, lines.Select(CurrencyConversionService.From).ToList(),
            Array.Empty<Article>(), out var plan, out _), "shared conversion planner accepts contract lines");
        contract.Currency = plan!.TargetCurrency;
        foreach (var (line, index) in lines.Select((l, n) => (l, n)))
        {
            line.Currency = contract.Currency;
            line.ExchangeRateSnapshot = plan.Rate;
            line.UnitPrice = plan.Lines[index].NewUnitPrice;
        }
        Check(await workflow.SavePreparedAsync(contract, false) is null, "save confirmed conversion");
    }
    var converted = await WithContract(w => w.GetDetailAsync(id));
    Check(converted!.Summary.Currency == "CNY" && converted.Summary.Total == 3920m &&
        converted.Items.Select(l => l.Id).SequenceEqual(new[] { firstId, addedId }) &&
        converted.Items.All(l => l.Currency == "CNY" && l.ExchangeRate == 7m) &&
        converted.TermsAndConditions == "Revised clause", "converted amounts, identities and clauses persisted");
    var downstream = await WithInvoice(w => w.ConvertAsync(DocumentSourceType.Contract, id, InvoiceType.Commercial));
    Check(downstream.Summary.SourceId == id && downstream.Summary.SourceType == DocumentSourceType.Contract &&
        downstream.Summary.Currency == "CNY" && downstream.Summary.Total == 3920m, "contract to commercial invoice");
    await Reject("contract currency downstream guard", () => WithContract(w => w.UpdateAsync(id,
        repeat with { Currency = "USD", Items = new[] { repeat.Items[0] with { UnitPrice = 100m }, repeat.Items[1] with { UnitPrice = 200m } } })), "downstream");
    Check((await WithContract(w => w.GetDetailAsync(id)))!.Summary.Currency == "CNY", "blocked change leaves stored currency unchanged");
    await Reject("contract deletion guard", () => WithContract(async w => { await w.DeleteAsync(id); return true; }), "downstream");
    await Reject("PI deletion guard", () => WithInvoice(async w => { await w.DeleteAsync(pi.Summary.Id); return true; }), "downstream");
    await Reject("quote deletion guard", () => WithQuotation(async w => { await w.DeleteAsync(quote.Summary.Id); return true; }), "downstream");

    // Existing desktop copy service still supplies unsaved packing list drafts.
    using (var scope = provider.CreateScope())
    {
        var copies = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        var packing = await copies.CreatePackingListFromContractAsync(id);
        Check(packing.Id == 0 && packing.SourceDocumentType == DocumentSourceType.Contract &&
            packing.SourceDocumentId == id && packing.Items.Count == 2, "contract to unsaved packing list draft");
    }
    var header = await WithContract(w => w.CreateAsync(request with { Number = "MANUAL-CON", Items = Array.Empty<ContractItemRequest>() }));
    Check(header.Summary.Number == "MANUAL-CON" && header.Items.Count == 0, "manual number and header-only save");
    await WithContract(async w => { await w.DeleteAsync(header.Summary.Id); return true; });
    await WithInvoice(async w => { await w.DeleteAsync(downstream.Summary.Id); return true; });
    foreach (var contractId in new[] { id, fromQuote.Summary.Id, fromPi.Summary.Id })
        await WithContract(async w => { await w.DeleteAsync(contractId); return true; });
    Check((await WithContract(w => w.GetHistoryAsync(customerId))).Count == 0, "delete in reverse dependency order");

    Console.WriteLine("PASS: Contract SQLite integration");

    string Connection(string name) => new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(folder, name), ForeignKeys = true, Pooling = false }.ToString();
    async Task<T> WithContract<T>(Func<IContractWorkflow, Task<T>> action)
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IContractWorkflow>());
    }
    async Task<T> WithQuotation<T>(Func<IQuotationWorkflow, Task<T>> action)
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IQuotationWorkflow>());
    }
    async Task<T> WithInvoice<T>(Func<IInvoiceWorkflow, Task<T>> action)
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IInvoiceWorkflow>());
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
