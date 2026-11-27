using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Quotations;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.Infrastructure;
using MiniERP.Infrastructure.Data;

var folder = Path.Combine(Path.GetTempPath(), "minierp-quotation-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);

try
{
    var services = new ServiceCollection();
    services.AddInfrastructure(Connection("erp.db"), Connection("catalog.db"));
    services.AddApplication();
    services.AddSingleton(new QuotationPolicy
    {
        NumberCode = "ACME", CnyPerUsd = 7m, DefaultValidityDays = 45,
        DeliveryTerm = "EXW", PaymentTerm = "50% advance", LeadTime = "3 weeks"
    });
    using var provider = services.BuildServiceProvider();
    int customerId, otherCustomerId, inactiveCustomerId, contactId, otherContactId, userId, articleId;

    using (var scope = provider.CreateScope())
    {
        var erp = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        // Exercise the actual EF model, SQLite provider and repositories on separate
        // disposable database files. Migration compatibility has its own CatalogSmoke suite.
        await erp.Database.EnsureCreatedAsync();
        await catalog.Database.EnsureCreatedAsync();
        var customer = new Customer { Name = "Test customer", Country = "DE" };
        var contact = new CustomerContact { Name = "Alice", Title = "Ms." };
        customer.Contacts.Add(contact);
        var other = new Customer { Name = "Other customer" };
        var otherContact = new CustomerContact { Name = "Bob" };
        other.Contacts.Add(otherContact);
        var inactive = new Customer { Name = "Inactive customer", IsActive = false };
        var user = new User { Name = "Test salesperson", Code = 3 };
        erp.Customers.AddRange(customer, other, inactive);
        erp.Users.Add(user);
        await erp.SaveChangesAsync();
        var article = new Article
        {
            Name = "RK3568", Name_EN = "Board", Description_EN = "English description",
            Price = 700m, MinimumPrice = 500m
        };
        catalog.Articles.Add(article);
        await catalog.SaveChangesAsync();
        customerId = customer.Id; otherCustomerId = other.Id; inactiveCustomerId = inactive.Id;
        contactId = contact.Id; otherContactId = otherContact.Id; userId = user.Id; articleId = article.Id;
    }

    var date = DateTime.Today;
    var request = new CreateQuotationRequest(customerId, userId,
        new QuotationItemRequest[]
        {
            new(articleId, 3m, DiscountPercent: 10m),
            new(null, 2m, UnitPrice: 25m, ArticleName: "Service")
        },
        ContactId: contactId, QuotationDate: date);

    var created = await WithWorkflow(w => w.CreateAsync(request));
    var id = created.Summary.Id;
    var firstLineId = created.Items[0].Id;
    var secondLineId = created.Items[1].Id;
    Check(id > 0 && firstLineId > 0 && secondLineId > 0 && firstLineId != secondLineId,
        "SQLite assigned quotation and item identities");
    Check(created.Summary.Number.Contains("ACME", StringComparison.Ordinal)
        && created.Summary.ValidUntil == date.AddDays(45) && created.DeliveryTerm == "EXW"
        && created.PaymentTerm == "50% advance" && created.LeadTime == "3 weeks",
        "configured company policy");
    Check(created.Items[0].UnitPrice == 100m && created.Summary.TotalAmount == 320m
        && created.ExchangeRate == 7m && created.CustomerContact == "Ms. Alice",
        "catalog conversion, manual item, discount and contact snapshot");

    var loaded = await WithWorkflow(w => w.GetDetailAsync(id));
    Check(loaded is not null && loaded.Summary.TotalAmount == 320m
        && loaded.Items[0].Id == firstLineId && loaded.Items[1].Id == secondLineId,
        "persisted values in a fresh database scope");
    var history = await WithWorkflow(w => w.GetHistoryAsync(customerId));
    Check(history.Count == 1 && history[0].Id == id, "customer quotation history");
    Check((await WithWorkflow(w => w.GetHistoryAsync(otherCustomerId))).Count == 0,
        "history scoped to requested customer");

    await Reject("missing customer", () => WithWorkflow(w => w.CreateAsync(request with { CustomerId = int.MaxValue })), "Customer");
    await Reject("missing salesperson", () => WithWorkflow(w => w.CreateAsync(request with { UserId = int.MaxValue })), "User");
    await Reject("foreign contact", () => WithWorkflow(w => w.CreateAsync(request with { ContactId = otherContactId })), "Contact");
    await Reject("inactive customer", () => WithWorkflow(w => w.CreateAsync(request with { CustomerId = inactiveCustomerId, ContactId = null })), "inactive");
    await Reject("missing catalog item", () => WithWorkflow(w => w.CreateAsync(request with { Items = new[] { new QuotationItemRequest(int.MaxValue, 1m) } })), "Article");
    await Reject("negative quantity", () => WithWorkflow(w => w.CreateAsync(request with { Items = new[] { new QuotationItemRequest(articleId, -1m) } })), "Quantity");
    await Reject("manual item without price", () => WithWorkflow(w => w.CreateAsync(request with { Items = new[] { new QuotationItemRequest(null, 1m, ArticleName: "Manual") } })), "price");
    await Reject("negative price", () => WithWorkflow(w => w.CreateAsync(request with { Items = new[] { new QuotationItemRequest(articleId, 1m, -1m) } })), "price");
    await Reject("invalid discount", () => WithWorkflow(w => w.CreateAsync(request with { Items = new[] { new QuotationItemRequest(articleId, 1m, DiscountPercent: 101m) } })), "Discount");
    await Reject("unsupported currency", () => WithWorkflow(w => w.CreateAsync(request with { Currency = "EUR" })), "currency");
    await Reject("expiry before date", () => WithWorkflow(w => w.CreateAsync(request with { ValidUntil = date.AddDays(-1) })), "validity");
    await Reject("duplicate manual number", () => WithWorkflow(w => w.CreateAsync(request with { QuotationNumber = created.Summary.Number })), "already exists");
    Check((await WithWorkflow(w => w.GetHistoryAsync(customerId))).Count == 1,
        "rejected requests did not persist partial quotations");

    using (var scope = provider.CreateScope())
    {
        var erp = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        (await erp.Customers.SingleAsync(c => c.Id == customerId)).Name = "Renamed customer";
        (await catalog.Articles.SingleAsync(a => a.Id == articleId)).Price = 1400m;
        await erp.SaveChangesAsync();
        await catalog.SaveChangesAsync();
    }

    var update = new UpdateQuotationRequest(id, customerId, userId,
        new QuotationItemRequest[]
        {
            new(articleId, 4m, DiscountPercent: 10m, ItemId: firstLineId),
            new(articleId, 1m)
        }, date, "USD", ValidUntil: created.Summary.ValidUntil,
        DeliveryTerm: created.DeliveryTerm, PaymentTerm: created.PaymentTerm, LeadTime: created.LeadTime);
    var updated = await WithWorkflow(w => w.UpdateAsync(update));
    var addedLineId = updated.Items[1].Id;
    Check(updated.Items[0].Id == firstLineId && updated.Items[0].UnitPrice == 100m
        && updated.Items[1].UnitPrice == 200m && addedLineId > 0 && addedLineId != firstLineId
        && updated.Items.All(i => i.Id != secondLineId),
        "update preserves historical price and returns inserted item ID");
    Check(updated.Summary.TotalAmount == 560m && updated.Summary.CustomerName == "Test customer"
        && updated.CustomerContact == "Ms. Alice" && updated.Summary.Number == created.Summary.Number,
        "snapshot and number preserved after customer/catalog changes");

    var again = update with
    {
        Items = new[]
        {
            new QuotationItemRequest(articleId, 4m, DiscountPercent: 10m, ItemId: firstLineId),
            new QuotationItemRequest(articleId, 1m, ItemId: addedLineId)
        }
    };
    var repeated = await WithWorkflow(w => w.UpdateAsync(again));
    Check(repeated.Items.Select(i => i.Id).SequenceEqual(new[] { firstLineId, addedLineId }),
        "repeated save preserves both line identities");
    using (var scope = provider.CreateScope())
    {
        var erp = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Check(await erp.QuotationItems.CountAsync(i => i.QuotationId == id) == 2,
            "no extra rows after repeated save");
    }

    var copy = await WithWorkflow(w => w.DuplicateAsync(id));
    Check(copy.Summary.Id != id && copy.Summary.Number != created.Summary.Number
        && copy.Items.All(i => i.Id != firstLineId && i.Id != addedLineId)
        && copy.Summary.TotalAmount == 560m && copy.Items[0].UnitPrice == 100m,
        "copy uses new identities and preserves historical prices");
    await Reject("item belongs to another quotation",
        () => WithWorkflow(w => w.UpdateAsync(update with
        { Items = new[] { new QuotationItemRequest(articleId, 1m, ItemId: copy.Items[0].Id) } })), "belong");
    await Reject("duplicate item ID",
        () => WithWorkflow(w => w.UpdateAsync(update with
        { Items = new[] { again.Items[0], again.Items[0] } })), "duplicate");
    await Reject("currency change without explicit price",
        () => WithWorkflow(w => w.UpdateAsync(again with { Currency = "CNY" })), "explicit unit price");

    // The trusted desktop path must use the same validations as the DTO path.
    using (var scope = provider.CreateScope())
    {
        var workflow = scope.ServiceProvider.GetRequiredService<IQuotationWorkflow>();
        var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
        var bad = (await service.GetQuotationByIdAsync(id))!;
        bad.Items.First().Currency = "CNY";
        var error = await workflow.SavePreparedAsync(bad, false, true);
        Check(error is not null && error.Contains("Currency", StringComparison.OrdinalIgnoreCase),
            "desktop path rejects currency mismatch");
        bad.Items.First().Currency = "USD";
        bad.Items.First().ExchangeRateSnapshot = 99m;
        error = await workflow.SavePreparedAsync(bad, false, true);
        Check(error is not null && error.Contains("exchange-rate", StringComparison.OrdinalIgnoreCase),
            "desktop path rejects exchange-rate mismatch");
    }

    var manual = await WithWorkflow(w => w.CreateAsync(request with
    {
        Items = Array.Empty<QuotationItemRequest>(), QuotationNumber = "MANUAL-001"
    }));
    Check(manual.Summary.Number == "MANUAL-001" && manual.Items.Count == 0,
        "manual number and header-only quotation");
    await WithWorkflow(async w => { await w.DeleteAsync(manual.Summary.Id); return true; });
    Check(await WithWorkflow(w => w.GetDetailAsync(manual.Summary.Id)) is null, "delete independent quotation");

    using (var scope = provider.CreateScope())
    {
        var erp = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        erp.Invoices.Add(new Invoice
        {
            InvoiceNumber = "TEST-PI-001", CustomerId = customerId, UserId = userId,
            SourceDocumentType = DocumentSourceType.Quotation, SourceDocumentId = id
        });
        await erp.SaveChangesAsync();
    }
    await Reject("downstream document deletion guard",
        () => WithWorkflow(async w => { await w.DeleteAsync(id); return true; }), "downstream");
    Check(await WithWorkflow(w => w.GetDetailAsync(id)) is not null,
        "referenced quotation remains persisted");
    await WithWorkflow(async w => { await w.DeleteAsync(copy.Summary.Id); return true; });
    Check((await WithWorkflow(w => w.GetHistoryAsync(customerId))).Count == 1,
        "delete copy without touching source");

    Console.WriteLine("PASS: SQLite quotation workflow integration");

    string Connection(string name) => new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(folder, name), ForeignKeys = true, Pooling = false
    }.ToString();

    async Task<T> WithWorkflow<T>(Func<IQuotationWorkflow, Task<T>> action)
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IQuotationWorkflow>());
    }
}
finally
{
    Directory.Delete(folder, recursive: true);
}

static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
}

static async Task Reject(string label, Func<Task> operation, string expectedMessage)
{
    try { await operation(); }
    catch (InvalidOperationException ex) when (ex.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("PASS: " + label);
        return;
    }
    throw new InvalidOperationException("FAIL: expected rejection for " + label);
}
