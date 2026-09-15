using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Creates a new independent quotation from an existing quotation snapshot.
/// Database identities and audit history are deliberately not carried over.
/// </summary>
public static class QuotationCopySupport
{
    public static Quotation CreateCopy(Quotation source)
    {
        var now = DateTime.Now;

        return new Quotation
        {
            QuotationNumber = $"QT-{now:yyyyMMdd-HHmmssfff}",
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
            CreatedAt = now,
            LastModifiedAt = now,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(item => CopyItem(item, now))
                .ToList()
        };
    }

    private static QuotationItem CopyItem(QuotationItem source, DateTime now)
        => new()
        {
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
            CreatedAt = now,
            LastModifiedAt = now
        };
}
