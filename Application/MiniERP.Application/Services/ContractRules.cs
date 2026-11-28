using MiniERP.Domain;
using System.Globalization;

namespace MiniERP.ApplicationLayer.Services;

/// <summary>Contract rules independent of Avalonia, storage and transport.</summary>
public static class ContractRules
{
    public static string NewNumber(DateTime now, ContractPolicy policy)
    {
        policy.Validate();
        return policy.NumberPrefix + "-" + now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
    }

    public static Contract CreateDraft(DateTime now, ContractPolicy policy)
        => new()
        {
            ContractNumber = NewNumber(now, policy), ContractDate = now,
            Currency = policy.DefaultCurrency, ExchangeRate = policy.CnyPerUsd ?? 0m,
            DeliveryTerm = policy.DeliveryTerm, PaymentTerm = policy.PaymentTerm, LeadTime = policy.LeadTime
        };

    // Zero is a valid unused rate for a CNY document. Never replace a rate
    // snapshot on a populated document just because settings have changed.
    public static decimal ResolveExchangeRate(Contract contract, decimal currentRate)
        => contract.Items.Count > 0 || contract.ExchangeRate > 0m
            ? contract.ExchangeRate : currentRate;

    public static void ApplyDraftDefaults(Contract contract, ContractPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(contract.DeliveryTerm)) contract.DeliveryTerm = policy.DeliveryTerm;
        if (string.IsNullOrWhiteSpace(contract.PaymentTerm)) contract.PaymentTerm = policy.PaymentTerm;
        if (string.IsNullOrWhiteSpace(contract.LeadTime)) contract.LeadTime = policy.LeadTime;
    }

    public static bool TryGetArticleUnitPrice(Article article, string currency, decimal rate, out decimal price, out string error)
        => QuotationRules.TryGetArticleUnitPrice(article, currency, rate, out price, out error);

    public static ContractItem CreateItemSnapshot(Article article, string currency, decimal rate, decimal price)
    {
        var item = QuotationRules.CreateItemSnapshot(article, currency, rate, price);
        return new ContractItem
        {
            SourceArticleId = item.SourceArticleId, ArticleName = item.ArticleName,
            Description = item.Description, Specification = item.Specification,
            Quantity = item.Quantity, Unit = item.Unit, UnitPrice = item.UnitPrice,
            DiscountPercent = item.DiscountPercent, Currency = item.Currency,
            ExchangeRateSnapshot = item.ExchangeRateSnapshot
        };
    }

    public static bool TryValidatePosition(string name, decimal? quantity, string unit, decimal? price,
        decimal? discount, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Every contract item needs an article name.";
            return false;
        }
        return QuotationRules.TryValidatePosition(name, quantity, unit, price, discount, out error);
    }

    public static bool TryValidateItem(ContractItem item, out string error)
        => TryValidatePosition(item.ArticleName, item.Quantity, item.Unit, item.UnitPrice, item.DiscountPercent, out error);

    public static bool TryPrepare(Contract contract, bool requireItems, Customer? customer, User? user,
        CustomerContact? contact, DateTime? date, DateTime? poDate,
        string currency, decimal rate, IReadOnlyList<ContractItem> items, out string error, out int invalidItemIndex)
    {
        invalidItemIndex = -1;
        if (string.IsNullOrWhiteSpace(contract.ContractNumber)) error = "Document number is required.";
        else if (customer is null) error = "Customer is required.";
        else if (user is null) error = "User is required.";
        else if (contact is not null && !customer.Contacts.Any(c => c.Id == contact.Id)) error = "Contact does not belong to the selected customer.";
        else if (date is null) error = "Date is required.";
        else if (requireItems && items.Count == 0) error = "Add at least one item before exporting PDF.";
        else if (!QuotationRules.IsSupportedCurrency(currency)) error = "Unsupported contract currency.";
        else if (currency == "USD" && items.Count > 0 && rate <= 0) error = "A valid USD exchange-rate snapshot is required.";
        else
        {
            for (var index = 0; index < items.Count; index++)
                if (!TryValidateItem(items[index], out error)) { invalidItemIndex = index; return false; }

            var customerChanged = contract.CustomerId != 0 && contract.CustomerId != customer.Id;
            var userChanged = contract.UserId != 0 && contract.UserId != user.Id;
            contract.ContractNumber = contract.ContractNumber.Trim();
            contract.ContractDate = date.Value;
            contract.CustomerPoDate = poDate;
            contract.Currency = currency;
            contract.ExchangeRate = rate;
            // Converted documents keep historical source snapshots unless a selection changes.
            if (customerChanged || string.IsNullOrWhiteSpace(contract.CustomerNameSnapshot))
            {
                contract.CustomerNameSnapshot = customer.Name.Trim();
                contract.CustomerAddressSnapshot = BuildCustomerAddress(customer);
                contract.CustomerContactSnapshot = contact is null ? null : BuildContactName(contact);
            }
            else if (contract.SourceDocumentType == DocumentSourceType.None && contact is not null)
                contract.CustomerContactSnapshot = BuildContactName(contact);
            if (userChanged || string.IsNullOrWhiteSpace(contract.SalesContactNameSnapshot))
            {
                contract.SalesContactNameSnapshot = user.Name.Trim();
                contract.SalesContactPhoneSnapshot = user.Phone;
                contract.SalesContactEmailSnapshot = user.Email;
            }
            contract.CustomerId = customer.Id;
            contract.UserId = user.Id;
            contract.Customer = null;
            contract.User = null;
            contract.Items = items.Select((item, index) =>
            {
                var copy = CloneItem(item);
                copy.ArticleName = copy.ArticleName.Trim();
                copy.Unit = copy.Unit.Trim().ToUpperInvariant();
                copy.SortOrder = index;
                return copy;
            }).ToList();
            error = string.Empty;
            return true;
        }
        return false;
    }

    public static ContractItem CloneItem(ContractItem item) => new()
    {
        Id = item.Id, ContractId = item.ContractId, SortOrder = item.SortOrder,
        SourceArticleId = item.SourceArticleId, ArticleName = item.ArticleName,
        Description = item.Description, Specification = item.Specification,
        Quantity = item.Quantity, Unit = item.Unit, UnitPrice = item.UnitPrice,
        DiscountPercent = item.DiscountPercent, Currency = item.Currency,
        ExchangeRateSnapshot = item.ExchangeRateSnapshot, CreatedBy = item.CreatedBy,
        CreatedAt = item.CreatedAt, LastModifiedBy = item.LastModifiedBy, LastModifiedAt = item.LastModifiedAt
    };

    private static string BuildCustomerAddress(Customer customer)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.AddressLine1)) lines.Add(customer.AddressLine1.Trim());
        if (!string.IsNullOrWhiteSpace(customer.AddressLine2)) lines.Add(customer.AddressLine2.Trim());
        var cityLine = string.Join(" ", new[] { customer.PostalCode, customer.City, customer.State }.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
        if (!string.IsNullOrWhiteSpace(cityLine)) lines.Add(cityLine);
        if (!string.IsNullOrWhiteSpace(customer.Country)) lines.Add(customer.Country.Trim());
        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildContactName(CustomerContact contact)
        => string.IsNullOrWhiteSpace(contact.Title) ? contact.Name.Trim() : $"{contact.Title.Trim()} {contact.Name.Trim()}";

    public static Contract Clone(Contract source) => new()
    {
        Id = source.Id,
        ContractNumber = source.ContractNumber,
        ContractDate = source.ContractDate,
        CustomerId = source.CustomerId,
        UserId = source.UserId,
        Currency = source.Currency,
        ExchangeRate = source.ExchangeRate,
        DeliveryTerm = source.DeliveryTerm,
        PaymentTerm = source.PaymentTerm,
        BankInformation = source.BankInformation,
        Remarks = source.Remarks,
        LeadTime = source.LeadTime,
        TermsAndConditions = source.TermsAndConditions,
        CustomerPoNumber = source.CustomerPoNumber,
        CustomerPoDate = source.CustomerPoDate,
        CustomerNameSnapshot = source.CustomerNameSnapshot,
        CustomerAddressSnapshot = source.CustomerAddressSnapshot,
        CustomerContactSnapshot = source.CustomerContactSnapshot,
        SalesContactNameSnapshot = source.SalesContactNameSnapshot,
        SalesContactPhoneSnapshot = source.SalesContactPhoneSnapshot,
        SalesContactEmailSnapshot = source.SalesContactEmailSnapshot,
        SourceDocumentType = source.SourceDocumentType,
        SourceDocumentId = source.SourceDocumentId,
        SourceDocumentNumber = source.SourceDocumentNumber,
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt,
        Items = source.Items.Select(CloneItem).ToList()
    };
}
