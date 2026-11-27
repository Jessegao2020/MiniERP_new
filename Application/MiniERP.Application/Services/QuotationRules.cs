using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

/// <summary>Headless quotation business rules. UI state and text parsing belong to clients.</summary>
public static class QuotationRules
{
    public static int NormalizeValidityDays(int days) => days is >= 1 and <= 3650 ? days : 30;

    public static string NormalizeCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

    public static bool IsSupportedCurrency(string currency) => currency is "USD" or "CNY";

    public static Quotation CreateDraft(DateTime now, int validityDays, decimal exchangeRate)
        => CreateDraft(now, new QuotationPolicy
        {
            DefaultValidityDays = NormalizeValidityDays(validityDays),
            CnyPerUsd = exchangeRate > 0m ? exchangeRate : null
        });

    public static Quotation CreateDraft(DateTime now, QuotationPolicy policy)
    {
        policy.Validate();
        return new Quotation
        {
            QuotationNumber = string.Empty,
            DeliveryTerm = policy.DeliveryTerm,
            PaymentTerm = policy.PaymentTerm,
            LeadTime = policy.LeadTime,
            QuotationDate = now,
            ValidUntil = now.Date.AddDays(policy.DefaultValidityDays),
            Currency = policy.DefaultCurrency,
            ExchangeRate = policy.CnyPerUsd ?? 0m
        };
    }

    public static decimal ResolveExchangeRate(Quotation? source, decimal currentRate)
    {
        var legacyPlaceholder = source is not null && source.Items.Count == 0
            && source.ExchangeRate == 1m && currentRate > 0m && currentRate != 1m;
        return source is null || source.ExchangeRate <= 0m || legacyPlaceholder
            ? currentRate : source.ExchangeRate;
    }

    public static bool FollowsDefaultValidity(bool isNew, DateTime? date, DateTime? validUntil, int days)
        => isNew && date is not null && validUntil is not null
            && validUntil.Value.Date == date.Value.Date.AddDays(NormalizeValidityDays(days));

    public static bool CanRefreshExchangeRate(bool isNew, int itemCount) => isNew && itemCount == 0;

    public static bool TryGetArticleUnitPrice(Article article, string currency, decimal exchangeRate,
        out decimal unitPrice, out string error)
    {
        unitPrice = 0m;
        if (article.Price is null)
        {
            error = $"Article '{article.Name}' does not have a CNY price.";
            return false;
        }
        if (!IsSupportedCurrency(currency))
        {
            error = $"Unsupported quotation currency: {currency}.";
            return false;
        }
        if (currency == "USD" && exchangeRate <= 0m)
        {
            error = "Set the USD exchange rate in Settings > System before adding USD items.";
            return false;
        }
        unitPrice = ConvertCatalogPrice(article.Price.Value, currency, exchangeRate);
        error = string.Empty;
        return true;
    }

    private static decimal ConvertCatalogPrice(decimal cnyPrice, string currency, decimal exchangeRate)
        => currency == "USD"
            ? decimal.Round(cnyPrice / exchangeRate, 2, MidpointRounding.AwayFromZero)
            : cnyPrice;

    public static QuotationItem CreateItemSnapshot(Article article, string currency, decimal exchangeRate, decimal unitPrice)
        => new()
        {
            SourceArticleId = article.Id,
            ArticleName = FirstNonEmpty(article.Name_EN, article.Name) ?? $"Article {article.Id}",
            Description = article.Description_EN,
            Specification = FirstNonEmpty(article.Specs_EN, article.Specification),
            Quantity = 1m, Unit = "PCS", UnitPrice = unitPrice, DiscountPercent = 0m,
            Currency = currency, ExchangeRateSnapshot = exchangeRate
        };

    public static bool TryCreateItem(Article article, string currency, decimal exchangeRate,
        out QuotationItem? item, out string error)
    {
        item = null;
        if (!TryGetArticleUnitPrice(article, currency, exchangeRate, out var price, out error))
            return false;
        item = CreateItemSnapshot(article, currency, exchangeRate, price);
        return true;
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback)
        => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

    // Plan all prices before updating any line. A missing source or price cannot
    // leave a document partly repriced. Historical snapshots are never changed implicitly.
    public static bool TryPlanRepricing(IReadOnlyList<QuotationItem> items, IReadOnlyCollection<Article> articles,
        string targetCurrency, decimal exchangeRate, out IReadOnlyList<decimal> prices, out string error)
    {
        prices = Array.Empty<decimal>();
        if (!IsSupportedCurrency(targetCurrency))
        {
            error = $"Unsupported quotation currency: {targetCurrency}.";
            return false;
        }
        if (targetCurrency == "USD" && exchangeRate <= 0m)
        {
            error = "Set the USD exchange rate in Settings > System before switching this quotation to USD.";
            return false;
        }
        if (articles.Count == 0)
        {
            error = "Article prices are not loaded yet. Wait for the quotation to finish loading, then change currency again.";
            return false;
        }
        var prepared = new List<decimal>(items.Count);
        foreach (var item in items)
        {
            if (item.SourceArticleId is null)
            {
                error = $"Position '{item.ArticleName}' is not linked to an Article, so its {targetCurrency} price cannot be refreshed automatically.";
                return false;
            }
            var article = articles.FirstOrDefault(a => a.Id == item.SourceArticleId.Value);
            if (article is null)
            {
                error = $"The source Article for position '{item.ArticleName}' no longer exists, so its {targetCurrency} price cannot be refreshed.";
                return false;
            }
            if (article.Price is null)
            {
                error = $"Article '{article.Name}' does not have a CNY untaxed price.";
                return false;
            }
            prepared.Add(ConvertCatalogPrice(article.Price.Value, targetCurrency, exchangeRate));
        }
        prices = prepared;
        error = string.Empty;
        return true;
    }

    public static bool TryValidateItem(QuotationItem item, out string error)
        => TryValidatePosition(item.ArticleName, item.Quantity, item.Unit, item.UnitPrice, item.DiscountPercent, out error);

    // Nullable numeric values let UI clients report parse failures without embedding business rules.
    public static bool TryValidatePosition(string articleName, decimal? quantity, string unit,
        decimal? unitPrice, decimal? discount, out string error)
    {
        if (string.IsNullOrWhiteSpace(articleName))
            error = "Every quotation item needs an article name.";
        else if (quantity is null || quantity <= 0)
            error = $"Quantity for '{articleName}' must be greater than zero.";
        else if (string.IsNullOrWhiteSpace(unit))
            error = $"Unit for '{articleName}' is required.";
        else if (unitPrice is null || unitPrice < 0)
            error = $"Unit price for '{articleName}' cannot be negative.";
        else if (discount is null || discount < 0 || discount > 100)
            error = $"Discount for '{articleName}' must be between 0 and 100%.";
        else
        {
            error = string.Empty;
            return true;
        }
        return false;
    }

    // Prepare a document from typed input. Selection and error display remain the UI's responsibility.
    public static bool TryPrepare(Quotation quotation, bool isNew, bool manuallyEditedNumber, bool requireItems,
        Customer? customer, User? user, CustomerContact? contact, DateTime? date, DateTime? validUntil,
        string currency, decimal exchangeRate, IReadOnlyList<QuotationItem> items,
        out string error, out int invalidItemIndex)
    {
        invalidItemIndex = -1;
        if ((!isNew || manuallyEditedNumber) && string.IsNullOrWhiteSpace(quotation.QuotationNumber))
            error = "Quotation number is required.";
        else if (customer is null)
            error = "Customer is required.";
        else if (user is null)
            error = "User is required. Create one in Settings > User if necessary.";
        else if (contact is not null && !customer.Contacts.Any(c => c.Id == contact.Id))
            error = "Contact does not belong to the selected customer.";
        else if (date is null)
            error = "Quotation date is required.";
        else if (requireItems && items.Count == 0)
            error = "Add at least one quotation item before exporting PDF.";
        else if (!IsSupportedCurrency(currency))
            error = $"Unsupported quotation currency: {currency}.";
        else if (currency == "USD" && items.Count > 0 && exchangeRate <= 0)
            error = "A valid USD exchange-rate snapshot is required.";
        else
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (!TryValidateItem(items[index], out error))
                {
                    invalidItemIndex = index;
                    return false;
                }
            }
            var customerChanged = quotation.CustomerId != 0 && quotation.CustomerId != customer.Id;
            var userChanged = quotation.UserId != 0 && quotation.UserId != user.Id;
            quotation.QuotationNumber = quotation.QuotationNumber.Trim();
            quotation.QuotationDate = date.Value;
            quotation.ValidUntil = validUntil;
            quotation.Currency = currency;
            quotation.ExchangeRate = exchangeRate;
            if (isNew || customerChanged || string.IsNullOrWhiteSpace(quotation.CustomerNameSnapshot))
            {
                quotation.CustomerNameSnapshot = customer.Name.Trim();
                quotation.CustomerAddressSnapshot = BuildCustomerAddress(customer);
                quotation.CustomerContactSnapshot = contact is null ? null : BuildContactName(contact);
            }
            else if (contact is not null)
                quotation.CustomerContactSnapshot = BuildContactName(contact);

            if (isNew || userChanged || string.IsNullOrWhiteSpace(quotation.SalesContactNameSnapshot))
            {
                quotation.SalesContactNameSnapshot = user.Name.Trim();
                quotation.SalesContactPhoneSnapshot = user.Phone;
                quotation.SalesContactEmailSnapshot = user.Email;
            }
            quotation.CustomerId = customer.Id;
            quotation.UserId = user.Id;
            quotation.Customer = null;
            quotation.User = null;
            quotation.Items = items.Select((item, index) =>
            {
                var snapshot = CloneItem(item);
                snapshot.ArticleName = snapshot.ArticleName.Trim();
                snapshot.Unit = snapshot.Unit.Trim().ToUpperInvariant();
                snapshot.SortOrder = index;
                return snapshot;
            }).ToList();
            error = string.Empty;
            return true;
        }
        return false;
    }

    public static bool TryValidatePrepared(Quotation quotation, bool isNew, bool manualNumber, out string error)
    {
        if ((!isNew || manualNumber) && string.IsNullOrWhiteSpace(quotation.QuotationNumber))
            error = "Quotation number is required.";
        else if (quotation.CustomerId <= 0)
            error = "Customer is required.";
        else if (quotation.UserId <= 0)
            error = "User is required. Create one in Settings > User if necessary.";
        else if (quotation.QuotationDate == default)
            error = "Quotation date is required.";
        else if (!IsSupportedCurrency(quotation.Currency))
            error = $"Unsupported quotation currency: {quotation.Currency}.";
        else if (quotation.Currency == "USD" && quotation.Items.Count > 0 && quotation.ExchangeRate <= 0m)
            error = "A valid USD exchange-rate snapshot is required.";
        else
        {
            foreach (var item in quotation.Items)
                if (!TryValidateItem(item, out error)) return false;
            error = string.Empty;
            return true;
        }
        return false;
    }

    // Copying from an editor deliberately refreshes selected customer and sales
    // contact snapshots, unlike reopening/saving historical documents.
    public static Quotation BuildCopySnapshot(Quotation source, Customer customer, User user, CustomerContact? contact,
        DateTime date, DateTime? validUntil, string currency, decimal exchangeRate,
        IReadOnlyList<QuotationItem> items, string? countryDisplayName)
        => new()
        {
            QuotationNumber = source.QuotationNumber,
            CustomerId = customer.Id, UserId = user.Id,
            DeliveryTerm = source.DeliveryTerm, LeadTime = source.LeadTime, PaymentTerm = source.PaymentTerm,
            Remarks = source.Remarks, QuotationDate = date, ValidUntil = validUntil,
            Currency = currency, ExchangeRate = exchangeRate,
            CustomerNameSnapshot = customer.Name.Trim(),
            CustomerAddressSnapshot = BuildCustomerAddress(customer, countryDisplayName),
            CustomerContactSnapshot = contact is null
                ? customer.Id == source.CustomerId ? source.CustomerContactSnapshot : null
                : BuildContactName(contact),
            SalesContactNameSnapshot = user.Name.Trim(), SalesContactPhoneSnapshot = user.Phone,
            SalesContactEmailSnapshot = user.Email,
            Items = items.Select(CloneItem).ToList()
        };

    public static string BuildContactName(CustomerContact contact)
    {
        var title = contact.Title?.Trim();
        var name = contact.Name.Trim();
        return string.IsNullOrWhiteSpace(title) ? name : $"{title} {name}".Trim();
    }

    public static string? BuildCustomerAddress(Customer customer)
        => BuildCustomerAddress(customer, CountryName(customer.Country));

    public static string? BuildCustomerAddress(Customer customer, string? countryName)
    {
        var lines = new List<string>();
        AddIfNotEmpty(lines, customer.AddressLine1);
        AddIfNotEmpty(lines, customer.AddressLine2);

        var cityParts = new[] { customer.PostalCode?.Trim(), customer.City?.Trim(), customer.State?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        AddIfNotEmpty(lines, string.Join(" ", cityParts));
        AddIfNotEmpty(lines, countryName);
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private static string? CountryName(string? code)
        => code?.Trim().ToUpperInvariant() switch
        {
            "CN" => "China",
            "GB" => "United Kingdom",
            "DE" => "Germany",
            "FR" => "France",
            "US" => "United States",
            "JP" => "Japan",
            "KR" => "South Korea",
            _ => code?.Trim()
        };

    private static void AddIfNotEmpty(ICollection<string> target, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) target.Add(value.Trim());
    }

    public static Quotation Clone(Quotation source) => new()
    {
        Id = source.Id,
        QuotationNumber = source.QuotationNumber,
        CustomerId = source.CustomerId,
        UserId = source.UserId,
        DeliveryTerm = source.DeliveryTerm,
        LeadTime = source.LeadTime,
        PaymentTerm = source.PaymentTerm,
        Remarks = source.Remarks,
        QuotationDate = source.QuotationDate,
        ValidUntil = source.ValidUntil,
        Currency = source.Currency,
        ExchangeRate = source.ExchangeRate,
        CustomerNameSnapshot = source.CustomerNameSnapshot,
        CustomerAddressSnapshot = source.CustomerAddressSnapshot,
        CustomerContactSnapshot = source.CustomerContactSnapshot,
        SalesContactNameSnapshot = source.SalesContactNameSnapshot,
        SalesContactPhoneSnapshot = source.SalesContactPhoneSnapshot,
        SalesContactEmailSnapshot = source.SalesContactEmailSnapshot,
        Items = source.Items.Select(CloneItem).ToList(),
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt
    };

    private static QuotationItem CloneItem(QuotationItem source) => new()
    {
        Id = source.Id,
        QuotationId = source.QuotationId,
        SortOrder = source.SortOrder,
        SourceArticleId = source.SourceArticleId,
        ArticleName = source.ArticleName,
        Description = source.Description,
        Specification = source.Specification,
        Quantity = source.Quantity,
        Unit = source.Unit,
        UnitPrice = source.UnitPrice,
        DiscountPercent = source.DiscountPercent,
        Currency = source.Currency,
        ExchangeRateSnapshot = source.ExchangeRateSnapshot,
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt
    };


}
