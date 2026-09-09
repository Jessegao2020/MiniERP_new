using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Contracts;

public sealed class ContractEditorViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settings;
    private Customer? _selectedCustomer;
    private CustomerContact? _selectedCustomerContact;
    private User? _selectedUser;
    private Article? _selectedArticle;
    private ContractItemRowViewModel? _selectedItem;
    private DateTimeOffset? _contractDate;
    private DateTimeOffset? _customerPoDate;
    private string _currency;
    private decimal _exchangeRateSnapshot;
    private string _status = string.Empty;

    public Contract Contract { get; }
    public bool IsNew { get; private set; }
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<CustomerContact> CustomerContacts { get; } = new();
    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<Article> Articles { get; } = new();
    public ObservableCollection<ContractItemRowViewModel> Items { get; } = new();
    public ObservableCollection<string> Currencies { get; } = new() { "USD", "CNY" };

    public string SourceText => Contract.SourceDocumentType == DocumentSourceType.None
        ? "Created manually"
        : $"Created from {Contract.SourceDocumentType}: {Contract.SourceDocumentNumber}";

    public decimal ExchangeRateSnapshot
    {
        get => _exchangeRateSnapshot;
        private set
        {
            if (_exchangeRateSnapshot == value) return;
            _exchangeRateSnapshot = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExchangeRateDisplay));
        }
    }

    public string Currency
    {
        get => _currency;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "USD" : value.Trim().ToUpperInvariant();
            if (_currency == normalized) return;
            if (Items.Count > 0)
            {
                Status = "Remove contract items before changing currency.";
                OnPropertyChanged();
                return;
            }

            _currency = normalized;
            Contract.Currency = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalText));
        }
    }

    public string ExchangeRateDisplay => ExchangeRateSnapshot > 0
        ? $"1 USD = {ExchangeRateSnapshot:0.####} CNY"
        : "Not configured";

    public decimal TotalAmount => Items.Sum(item => item.LineTotal);
    public string TotalText => $"{Currency} {TotalAmount:N2}";

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (ReferenceEquals(_selectedCustomer, value)) return;
            _selectedCustomer = value;
            OnPropertyChanged();
            RefreshCustomerContacts();
        }
    }

    public CustomerContact? SelectedCustomerContact
    {
        get => _selectedCustomerContact;
        set { if (ReferenceEquals(_selectedCustomerContact, value)) return; _selectedCustomerContact = value; OnPropertyChanged(); }
    }

    public User? SelectedUser
    {
        get => _selectedUser;
        set { if (ReferenceEquals(_selectedUser, value)) return; _selectedUser = value; OnPropertyChanged(); }
    }

    public Article? SelectedArticle
    {
        get => _selectedArticle;
        set { if (ReferenceEquals(_selectedArticle, value)) return; _selectedArticle = value; OnPropertyChanged(); }
    }

    public ContractItemRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set { if (ReferenceEquals(_selectedItem, value)) return; _selectedItem = value; OnPropertyChanged(); }
    }

    public DateTimeOffset? ContractDate
    {
        get => _contractDate;
        set { if (_contractDate == value) return; _contractDate = value; OnPropertyChanged(); }
    }

    public DateTimeOffset? CustomerPoDate
    {
        get => _customerPoDate;
        set { if (_customerPoDate == value) return; _customerPoDate = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        private set { if (_status == value) return; _status = value; OnPropertyChanged(); }
    }

    public ContractEditorViewModel(Contract? source, AppSettingsService settings)
    {
        _settings = settings;
        IsNew = source is null || source.Id == 0;
        var currentRate = settings.Current.CnyPerUsd ?? 0m;

        Contract = source is null
            ? new Contract
            {
                ContractNumber = $"CON-{DateTime.Now:yyyyMMdd-HHmmssfff}",
                ContractDate = DateTime.Now,
                Currency = "USD",
                ExchangeRate = currentRate
            }
            : Clone(source);

        _currency = string.IsNullOrWhiteSpace(Contract.Currency) ? "USD" : Contract.Currency.ToUpperInvariant();
        ExchangeRateSnapshot = Contract.ExchangeRate > 0 ? Contract.ExchangeRate : currentRate;
        Contract.Currency = _currency;
        Contract.ExchangeRate = ExchangeRateSnapshot;
        ContractDate = new DateTimeOffset(Contract.ContractDate);
        CustomerPoDate = Contract.CustomerPoDate is null ? null : new DateTimeOffset(Contract.CustomerPoDate.Value);

        foreach (var item in Contract.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
            AddRow(new ContractItemRowViewModel(item));
    }

    public async Task LoadLookupsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var articleService = scope.ServiceProvider.GetRequiredService<IArticleService>();

            var customers = await customerService.GetAllCustomersAsync();
            var users = await userService.GetAllUsersAsync();
            var articles = await articleService.GetAllArticlesAsync();

            Customers.Clear();
            foreach (var customer in customers.Where(c => c.IsActive || c.Id == Contract.CustomerId).OrderBy(c => c.Name))
                Customers.Add(customer);

            Users.Clear();
            foreach (var user in users.OrderBy(u => u.Name)) Users.Add(user);

            Articles.Clear();
            foreach (var article in articles.OrderBy(a => a.Name)) Articles.Add(article);

            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == Contract.CustomerId);
            SelectedUser = Users.FirstOrDefault(u => u.Id == Contract.UserId);
            if (IsNew && SelectedCustomer is null && Customers.Count == 1) SelectedCustomer = Customers[0];
            if (IsNew && SelectedUser is null && Users.Count == 1) SelectedUser = Users[0];

            Status = Customers.Count == 0 ? "Create an active customer before saving a contract."
                : Users.Count == 0 ? "Create a user in Settings > User before saving a contract."
                : Articles.Count == 0 ? "Contract header is ready. Create an Article before adding line items."
                : "Ready.";
        }
        catch (Exception ex) { Status = $"Load failed: {ex.Message}"; }
    }

    public void RefreshExchangeRateFromSettings()
    {
        if (!IsNew || Items.Count > 0) return;
        var current = _settings.Current.CnyPerUsd ?? 0m;
        ExchangeRateSnapshot = current;
        Contract.ExchangeRate = current;
        Status = current > 0 ? $"Exchange rate refreshed: 1 USD = {current:0.####} CNY" : "USD exchange rate is not configured.";
    }

    public void AddSelectedArticle()
    {
        if (SelectedArticle is null) { Status = "Select an Article first."; return; }
        if (SelectedArticle.Price is null) { Status = $"Article '{SelectedArticle.Name}' does not have a CNY price."; return; }
        if (Currency == "USD" && ExchangeRateSnapshot <= 0) { Status = "Set the USD exchange rate in Settings > System first."; return; }

        var unitPrice = Currency == "USD"
            ? decimal.Round(SelectedArticle.Price.Value / ExchangeRateSnapshot, 2, MidpointRounding.AwayFromZero)
            : SelectedArticle.Price.Value;

        var row = new ContractItemRowViewModel(SelectedArticle, Currency, ExchangeRateSnapshot, unitPrice);
        AddRow(row);
        SelectedItem = row;
        Status = $"Added '{row.ArticleName}'.";
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null) { Status = "Select a contract item first."; return; }
        SelectedItem.PropertyChanged -= Item_PropertyChanged;
        Items.Remove(SelectedItem);
        SelectedItem = null;
        NotifyTotals();
        Status = "Contract item removed. Save to persist the change.";
    }

    public bool TryPrepareForExport() => TryPrepareDocument(requireItems: true);

    public async Task<bool> SaveAsync()
    {
        if (!TryPrepareDocument(requireItems: false)) return false;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContractService>();
            var sameNumber = await service.GetContractByNumberAsync(Contract.ContractNumber);
            if (sameNumber is not null && sameNumber.Id != Contract.Id) { Status = "Contract number already exists."; return false; }

            if (IsNew)
            {
                await service.CreateContractAsync(Contract);
                IsNew = false;
            }
            else
            {
                await service.UpdateContractAsync(Contract);
            }

            Status = "Saved.";
            return true;
        }
        catch (Exception ex) { Status = $"Save failed: {ex.Message}"; return false; }
    }

    public async Task<bool> DeleteAsync()
    {
        if (IsNew || Contract.Id == 0) return true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContractService>();
            await service.DeleteContractAsync(Contract.Id);
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex) { Status = $"Delete failed: {ex.Message}"; return false; }
    }

    public void SetStatusMessage(string message) => Status = message;

    private bool TryPrepareDocument(bool requireItems)
    {
        if (string.IsNullOrWhiteSpace(Contract.ContractNumber)) { Status = "Contract number is required."; return false; }
        if (SelectedCustomer is null) { Status = "Customer is required."; return false; }
        if (SelectedUser is null) { Status = "User is required."; return false; }
        if (ContractDate is null) { Status = "Contract date is required."; return false; }
        if (requireItems && Items.Count == 0) { Status = "Add at least one item before exporting PDF."; return false; }
        if (Currency == "USD" && Items.Count > 0 && ExchangeRateSnapshot <= 0) { Status = "A valid USD exchange-rate snapshot is required."; return false; }

        foreach (var item in Items)
        {
            if (!item.TryValidate(out var error)) { SelectedItem = item; Status = error; return false; }
        }

        var customerChanged = Contract.CustomerId != 0 && Contract.CustomerId != SelectedCustomer.Id;
        var userChanged = Contract.UserId != 0 && Contract.UserId != SelectedUser.Id;

        Contract.ContractNumber = Contract.ContractNumber.Trim();
        Contract.ContractDate = ContractDate.Value.DateTime;
        Contract.CustomerPoDate = CustomerPoDate?.DateTime;
        Contract.Currency = Currency;
        Contract.ExchangeRate = ExchangeRateSnapshot;

        if (customerChanged || string.IsNullOrWhiteSpace(Contract.CustomerNameSnapshot))
        {
            Contract.CustomerNameSnapshot = SelectedCustomer.Name.Trim();
            Contract.CustomerAddressSnapshot = BuildCustomerAddress(SelectedCustomer);
            Contract.CustomerContactSnapshot = SelectedCustomerContact is null ? null : BuildContactName(SelectedCustomerContact);
        }
        else if (Contract.SourceDocumentType == DocumentSourceType.None && SelectedCustomerContact is not null)
        {
            Contract.CustomerContactSnapshot = BuildContactName(SelectedCustomerContact);
        }

        if (userChanged || string.IsNullOrWhiteSpace(Contract.SalesContactNameSnapshot))
        {
            Contract.SalesContactNameSnapshot = SelectedUser.Name.Trim();
            Contract.SalesContactPhoneSnapshot = SelectedUser.Phone;
            Contract.SalesContactEmailSnapshot = SelectedUser.Email;
        }

        Contract.CustomerId = SelectedCustomer.Id;
        Contract.UserId = SelectedUser.Id;
        Contract.Customer = null;
        Contract.User = null;
        Contract.Items = Items.Select((item, index) => item.ToEntity(index)).ToList();
        return true;
    }

    private void RefreshCustomerContacts()
    {
        CustomerContacts.Clear();
        SelectedCustomerContact = null;
        if (SelectedCustomer is null) return;

        foreach (var contact in SelectedCustomer.Contacts.OrderBy(c => c.Name)) CustomerContacts.Add(contact);
        if (SelectedCustomer.Id == Contract.CustomerId && !string.IsNullOrWhiteSpace(Contract.CustomerContactSnapshot))
            SelectedCustomerContact = CustomerContacts.FirstOrDefault(c => string.Equals(BuildContactName(c), Contract.CustomerContactSnapshot, StringComparison.OrdinalIgnoreCase));
        if (IsNew && Contract.SourceDocumentType == DocumentSourceType.None && SelectedCustomerContact is null && CustomerContacts.Count == 1)
            SelectedCustomerContact = CustomerContacts[0];
    }

    private void AddRow(ContractItemRowViewModel row)
    {
        row.PropertyChanged += Item_PropertyChanged;
        Items.Add(row);
        NotifyTotals();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e) => NotifyTotals();
    private void NotifyTotals() { OnPropertyChanged(nameof(TotalAmount)); OnPropertyChanged(nameof(TotalText)); }

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

    private static string BuildContactName(CustomerContact contact)
        => string.IsNullOrWhiteSpace(contact.Title) ? contact.Name.Trim() : $"{contact.Title.Trim()} {contact.Name.Trim()}";

    private static Contract Clone(Contract source) => new()
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
        LeadTime = source.LeadTime,
        BankInformation = source.BankInformation,
        Remarks = source.Remarks,
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
        Items = source.Items.Select(i => new ContractItem
        {
            Id = i.Id,
            SortOrder = i.SortOrder,
            SourceArticleId = i.SourceArticleId,
            ArticleName = i.ArticleName,
            Description = i.Description,
            Specification = i.Specification,
            Quantity = i.Quantity,
            Unit = i.Unit,
            UnitPrice = i.UnitPrice,
            DiscountPercent = i.DiscountPercent,
            Currency = i.Currency,
            ExchangeRateSnapshot = i.ExchangeRateSnapshot
        }).ToList()
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
