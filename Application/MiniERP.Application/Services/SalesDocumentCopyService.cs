using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public sealed class SalesDocumentCopyService : ISalesDocumentCopyService
{
    private readonly IQuotationService _quotations;
    private readonly IInvoiceService _invoices;
    private readonly IContractService _contracts;

    public SalesDocumentCopyService(
        IQuotationService quotations,
        IInvoiceService invoices,
        IContractService contracts)
    {
        _quotations = quotations;
        _invoices = invoices;
        _contracts = contracts;
    }

    public async Task<Invoice> CreateInvoiceFromQuotationAsync(int quotationId, InvoiceType targetType)
    {
        var source = await _quotations.GetQuotationByIdAsync(quotationId)
            ?? throw new InvalidOperationException($"Quotation {quotationId} was not found.");

        return new Invoice
        {
            Type = targetType,
            InvoiceNumber = NewInvoiceNumber(targetType),
            InvoiceDate = DateTime.Now,
            CustomerId = source.CustomerId,
            UserId = source.UserId,
            Currency = source.Currency,
            ExchangeRate = source.ExchangeRate,
            DeliveryTerm = source.DeliveryTerm,
            PaymentTerm = source.PaymentTerm,
            Remarks = source.Remarks,
            CustomerNameSnapshot = source.CustomerNameSnapshot,
            CustomerAddressSnapshot = source.CustomerAddressSnapshot,
            CustomerContactSnapshot = source.CustomerContactSnapshot,
            SalesContactNameSnapshot = source.SalesContactNameSnapshot,
            SalesContactPhoneSnapshot = source.SalesContactPhoneSnapshot,
            SalesContactEmailSnapshot = source.SalesContactEmailSnapshot,
            SourceDocumentType = DocumentSourceType.Quotation,
            SourceDocumentId = source.Id,
            SourceDocumentNumber = source.QuotationNumber,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select((item, index) => new InvoiceItem
                {
                    SortOrder = index,
                    SourceArticleId = item.SourceArticleId,
                    ArticleName = item.ArticleName,
                    Description = item.Description,
                    Specification = item.Specification,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    Currency = item.Currency,
                    ExchangeRateSnapshot = item.ExchangeRateSnapshot
                })
                .ToList()
        };
    }

    public async Task<Invoice> CreateInvoiceFromInvoiceAsync(int invoiceId, InvoiceType targetType)
    {
        var source = await _invoices.GetInvoiceByIdAsync(invoiceId)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} was not found.");

        return new Invoice
        {
            Type = targetType,
            InvoiceNumber = NewInvoiceNumber(targetType),
            InvoiceDate = DateTime.Now,
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
            SourceDocumentType = InvoiceSourceType(source),
            SourceDocumentId = source.Id,
            SourceDocumentNumber = source.InvoiceNumber,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select((item, index) => CloneInvoiceItem(item, index))
                .ToList()
        };
    }

    public async Task<Invoice> CreateInvoiceFromContractAsync(int contractId, InvoiceType targetType)
    {
        var source = await _contracts.GetContractByIdAsync(contractId)
            ?? throw new InvalidOperationException($"Contract {contractId} was not found.");

        return new Invoice
        {
            Type = targetType,
            InvoiceNumber = NewInvoiceNumber(targetType),
            InvoiceDate = DateTime.Now,
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
            SourceDocumentType = DocumentSourceType.Contract,
            SourceDocumentId = source.Id,
            SourceDocumentNumber = source.ContractNumber,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select((item, index) => new InvoiceItem
                {
                    SortOrder = index,
                    SourceArticleId = item.SourceArticleId,
                    ArticleName = item.ArticleName,
                    Description = item.Description,
                    Specification = item.Specification,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    Currency = item.Currency,
                    ExchangeRateSnapshot = item.ExchangeRateSnapshot
                })
                .ToList()
        };
    }

    public async Task<PackingList> CreatePackingListFromQuotationAsync(int quotationId)
    {
        var source = await _quotations.GetQuotationByIdAsync(quotationId)
            ?? throw new InvalidOperationException($"Quotation {quotationId} was not found.");

        return new PackingList
        {
            PackingListNumber = NewPackingListNumber(),
            PackingDate = DateTime.Now,
            CustomerId = source.CustomerId,
            UserId = source.UserId,
            DeliveryTerm = source.DeliveryTerm,
            Remarks = source.Remarks,
            CustomerNameSnapshot = source.CustomerNameSnapshot,
            CustomerAddressSnapshot = source.CustomerAddressSnapshot,
            CustomerContactSnapshot = source.CustomerContactSnapshot,
            SalesContactNameSnapshot = source.SalesContactNameSnapshot,
            SalesContactPhoneSnapshot = source.SalesContactPhoneSnapshot,
            SalesContactEmailSnapshot = source.SalesContactEmailSnapshot,
            SourceDocumentType = DocumentSourceType.Quotation,
            SourceDocumentId = source.Id,
            SourceDocumentNumber = source.QuotationNumber,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select((item, index) => new PackingListItem
                {
                    SortOrder = index,
                    SourceArticleId = item.SourceArticleId,
                    ArticleName = item.ArticleName,
                    Description = item.Description,
                    Specification = item.Specification,
                    Quantity = item.Quantity,
                    Unit = item.Unit
                })
                .ToList()
        };
    }

    public async Task<PackingList> CreatePackingListFromInvoiceAsync(int invoiceId)
    {
        var source = await _invoices.GetInvoiceByIdAsync(invoiceId)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} was not found.");

        return new PackingList
        {
            PackingListNumber = NewPackingListNumber(),
            PackingDate = DateTime.Now,
            CustomerId = source.CustomerId,
            UserId = source.UserId,
            DeliveryTerm = source.DeliveryTerm,
            Remarks = source.Remarks,
            CustomerPoNumber = source.CustomerPoNumber,
            CustomerPoDate = source.CustomerPoDate,
            CustomerNameSnapshot = source.CustomerNameSnapshot,
            CustomerAddressSnapshot = source.CustomerAddressSnapshot,
            CustomerContactSnapshot = source.CustomerContactSnapshot,
            SalesContactNameSnapshot = source.SalesContactNameSnapshot,
            SalesContactPhoneSnapshot = source.SalesContactPhoneSnapshot,
            SalesContactEmailSnapshot = source.SalesContactEmailSnapshot,
            SourceDocumentType = InvoiceSourceType(source),
            SourceDocumentId = source.Id,
            SourceDocumentNumber = source.InvoiceNumber,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select((item, index) => new PackingListItem
                {
                    SortOrder = index,
                    SourceArticleId = item.SourceArticleId,
                    ArticleName = item.ArticleName,
                    Description = item.Description,
                    Specification = item.Specification,
                    Quantity = item.Quantity,
                    Unit = item.Unit
                })
                .ToList()
        };
    }

    public async Task<PackingList> CreatePackingListFromContractAsync(int contractId)
    {
        var source = await _contracts.GetContractByIdAsync(contractId)
            ?? throw new InvalidOperationException($"Contract {contractId} was not found.");

        return new PackingList
        {
            PackingListNumber = NewPackingListNumber(),
            PackingDate = DateTime.Now,
            CustomerId = source.CustomerId,
            UserId = source.UserId,
            DeliveryTerm = source.DeliveryTerm,
            Remarks = source.Remarks,
            CustomerPoNumber = source.CustomerPoNumber,
            CustomerPoDate = source.CustomerPoDate,
            CustomerNameSnapshot = source.CustomerNameSnapshot,
            CustomerAddressSnapshot = source.CustomerAddressSnapshot,
            CustomerContactSnapshot = source.CustomerContactSnapshot,
            SalesContactNameSnapshot = source.SalesContactNameSnapshot,
            SalesContactPhoneSnapshot = source.SalesContactPhoneSnapshot,
            SalesContactEmailSnapshot = source.SalesContactEmailSnapshot,
            SourceDocumentType = DocumentSourceType.Contract,
            SourceDocumentId = source.Id,
            SourceDocumentNumber = source.ContractNumber,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select((item, index) => new PackingListItem
                {
                    SortOrder = index,
                    SourceArticleId = item.SourceArticleId,
                    ArticleName = item.ArticleName,
                    Description = item.Description,
                    Specification = item.Specification,
                    Quantity = item.Quantity,
                    Unit = item.Unit
                })
                .ToList()
        };
    }

    public async Task<Contract> CreateContractFromQuotationAsync(int quotationId)
    {
        var source = await _quotations.GetQuotationByIdAsync(quotationId)
            ?? throw new InvalidOperationException($"Quotation {quotationId} was not found.");

        return new Contract
        {
            ContractNumber = NewContractNumber(),
            ContractDate = DateTime.Now,
            CustomerId = source.CustomerId,
            UserId = source.UserId,
            Currency = source.Currency,
            ExchangeRate = source.ExchangeRate,
            DeliveryTerm = source.DeliveryTerm,
            PaymentTerm = source.PaymentTerm,
            LeadTime = source.LeadTime,
            Remarks = source.Remarks,
            CustomerNameSnapshot = source.CustomerNameSnapshot,
            CustomerAddressSnapshot = source.CustomerAddressSnapshot,
            CustomerContactSnapshot = source.CustomerContactSnapshot,
            SalesContactNameSnapshot = source.SalesContactNameSnapshot,
            SalesContactPhoneSnapshot = source.SalesContactPhoneSnapshot,
            SalesContactEmailSnapshot = source.SalesContactEmailSnapshot,
            SourceDocumentType = DocumentSourceType.Quotation,
            SourceDocumentId = source.Id,
            SourceDocumentNumber = source.QuotationNumber,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select((item, index) => new ContractItem
                {
                    SortOrder = index,
                    SourceArticleId = item.SourceArticleId,
                    ArticleName = item.ArticleName,
                    Description = item.Description,
                    Specification = item.Specification,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    Currency = item.Currency,
                    ExchangeRateSnapshot = item.ExchangeRateSnapshot
                })
                .ToList()
        };
    }

    public async Task<Contract> CreateContractFromInvoiceAsync(int invoiceId)
    {
        var source = await _invoices.GetInvoiceByIdAsync(invoiceId)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} was not found.");

        return new Contract
        {
            ContractNumber = NewContractNumber(),
            ContractDate = DateTime.Now,
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
            SourceDocumentType = InvoiceSourceType(source),
            SourceDocumentId = source.Id,
            SourceDocumentNumber = source.InvoiceNumber,
            Items = source.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select((item, index) => new ContractItem
                {
                    SortOrder = index,
                    SourceArticleId = item.SourceArticleId,
                    ArticleName = item.ArticleName,
                    Description = item.Description,
                    Specification = item.Specification,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    Currency = item.Currency,
                    ExchangeRateSnapshot = item.ExchangeRateSnapshot
                })
                .ToList()
        };
    }

    private static InvoiceItem CloneInvoiceItem(InvoiceItem item, int sortOrder)
        => new()
        {
            SortOrder = sortOrder,
            SourceArticleId = item.SourceArticleId,
            ArticleName = item.ArticleName,
            Description = item.Description,
            Specification = item.Specification,
            Quantity = item.Quantity,
            Unit = item.Unit,
            UnitPrice = item.UnitPrice,
            DiscountPercent = item.DiscountPercent,
            Currency = item.Currency,
            ExchangeRateSnapshot = item.ExchangeRateSnapshot
        };

    private static DocumentSourceType InvoiceSourceType(Invoice invoice)
        => invoice.Type == InvoiceType.Proforma
            ? DocumentSourceType.ProformaInvoice
            : DocumentSourceType.CommercialInvoice;

    private static string NewInvoiceNumber(InvoiceType type)
        => $"{(type == InvoiceType.Proforma ? "PI" : "INV")}-{DateTime.Now:yyyyMMdd-HHmmssfff}";

    private static string NewPackingListNumber()
        => $"PL-{DateTime.Now:yyyyMMdd-HHmmssfff}";

    private static string NewContractNumber()
        => $"CON-{DateTime.Now:yyyyMMdd-HHmmssfff}";
}
