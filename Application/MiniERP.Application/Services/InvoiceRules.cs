using MiniERP.Domain;
using System.Globalization;

namespace MiniERP.ApplicationLayer.Services;

/// <summary>Invoice and PI rules independent of Avalonia, storage and transport.</summary>
public static class InvoiceRules
{
    public static string NewNumber(InvoiceType type, DateTime now, InvoicePolicy policy)
    {
        policy.Validate();
        if (type is not (InvoiceType.Proforma or InvoiceType.Commercial))
            throw new InvalidOperationException("Unsupported invoice type.");
        return (type == InvoiceType.Proforma ? policy.ProformaPrefix : policy.CommercialPrefix)
            + "-" + now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
    }

    public static Invoice CreateDraft(InvoiceType type, DateTime now, InvoicePolicy policy)
        => new()
        {
            Type = type, InvoiceNumber = NewNumber(type, now, policy), InvoiceDate = now,
            Currency = policy.DefaultCurrency, ExchangeRate = policy.CnyPerUsd ?? 0m,
            DeliveryTerm = policy.DeliveryTerm, PaymentTerm = policy.PaymentTerm
        };

    public static void ApplyDraftDefaults(Invoice invoice, InvoicePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(invoice.DeliveryTerm)) invoice.DeliveryTerm = policy.DeliveryTerm;
        if (string.IsNullOrWhiteSpace(invoice.PaymentTerm)) invoice.PaymentTerm = policy.PaymentTerm;
    }

    public static bool TryChangeCurrency(string currency, int itemCount, out string error)
    {
        error = !QuotationRules.IsSupportedCurrency(currency) ? "Unsupported invoice currency."
            : itemCount > 0 ? "Remove invoice items before changing currency." : string.Empty;
        return error.Length == 0;
    }

    public static bool TryGetArticleUnitPrice(Article article, string currency, decimal rate, out decimal price, out string error)
        => QuotationRules.TryGetArticleUnitPrice(article, currency, rate, out price, out error);

    public static InvoiceItem CreateItemSnapshot(Article article, string currency, decimal rate, decimal price)
    {
        var item = QuotationRules.CreateItemSnapshot(article, currency, rate, price);
        return new InvoiceItem
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
            error = "Every invoice item needs an article name.";
            return false;
        }
        return QuotationRules.TryValidatePosition(name, quantity, unit, price, discount, out error);
    }

    public static bool TryValidateItem(InvoiceItem item, out string error)
        => TryValidatePosition(item.ArticleName, item.Quantity, item.Unit, item.UnitPrice, item.DiscountPercent, out error);

    public static bool TryPrepare(Invoice invoice, bool requireItems, Customer? customer, User? user,
        CustomerContact? contact, DateTime? date, DateTime? due, DateTime? poDate,
        string currency, decimal rate, IReadOnlyList<InvoiceItem> items, out string error, out int invalidItemIndex)
    {
        invalidItemIndex = -1;
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber)) error = "Document number is required.";
        else if (customer is null) error = "Customer is required.";
        else if (user is null) error = "User is required.";
        else if (contact is not null && !customer.Contacts.Any(c => c.Id == contact.Id)) error = "Contact does not belong to the selected customer.";
        else if (date is null) error = "Date is required.";
        else if (requireItems && items.Count == 0) error = "Add at least one item before exporting PDF.";
        else if (!QuotationRules.IsSupportedCurrency(currency)) error = "Unsupported invoice currency.";
        else if (currency == "USD" && items.Count > 0 && rate <= 0) error = "A valid USD exchange-rate snapshot is required.";
        else
        {
            for (var index = 0; index < items.Count; index++)
                if (!TryValidateItem(items[index], out error)) { invalidItemIndex = index; return false; }

            var customerChanged = invoice.CustomerId != 0 && invoice.CustomerId != customer.Id;
            var userChanged = invoice.UserId != 0 && invoice.UserId != user.Id;
            invoice.InvoiceNumber = invoice.InvoiceNumber.Trim();
            invoice.InvoiceDate = date.Value;
            invoice.DueDate = due;
            invoice.CustomerPoDate = poDate;
            invoice.Currency = currency;
            invoice.ExchangeRate = rate;
            // Converted documents keep historical source snapshots unless a selection changes.
            if (customerChanged || string.IsNullOrWhiteSpace(invoice.CustomerNameSnapshot))
            {
                invoice.CustomerNameSnapshot = customer.Name.Trim();
                invoice.CustomerAddressSnapshot = BuildCustomerAddress(customer);
                invoice.CustomerContactSnapshot = contact is null ? null : BuildContactName(contact);
            }
            else if (contact is not null)
                invoice.CustomerContactSnapshot = BuildContactName(contact);
            if (userChanged || string.IsNullOrWhiteSpace(invoice.SalesContactNameSnapshot))
            {
                invoice.SalesContactNameSnapshot = user.Name.Trim();
                invoice.SalesContactPhoneSnapshot = user.Phone;
                invoice.SalesContactEmailSnapshot = user.Email;
            }
            invoice.CustomerId = customer.Id;
            invoice.UserId = user.Id;
            invoice.Customer = null;
            invoice.User = null;
            invoice.Items = items.Select((item, index) =>
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

    public static InvoiceItem CloneItem(InvoiceItem item) => new()
    {
        Id = item.Id, InvoiceId = item.InvoiceId, SortOrder = item.SortOrder,
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

    public static Invoice Clone(Invoice source) => new()
    {
        Id = source.Id,
        Type = source.Type,
        InvoiceNumber = source.InvoiceNumber,
        InvoiceDate = source.InvoiceDate,
        DueDate = source.DueDate,
        CustomerId = source.CustomerId,
        UserId = source.UserId,
        Currency = source.Currency,
        ExchangeRate = source.ExchangeRate,
        DeliveryTerm = source.DeliveryTerm,
        PaymentTerm = source.PaymentTerm,
        BankInformation = source.BankInformation,
        Remarks = source.Remarks,
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
