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
    public IReadOnlyList<string> DeliveryTermOptions { get; } = new[] { "FCA Baoding", "CFR", "CPT" };
    public IReadOnlyList<string> LeadTimeOptions { get; } = new[] { "2 business days", "1 week", "4 weeks", "4-6 weeks" };

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

    public string Currency => _currency;

    public decimal CurrencyConversionRate => CurrencyConversionService.ResolveRate(
        ExchangeRateSnapshot, _settings.Current.CnyPerUsd ?? 0m);

    public IReadOnlyList<CurrencyConversionLine> GetCurrencyConversionLines()
        => Items.Select((row, index) => CurrencyConversionService.From(row.ToEntity(index))).ToList();

    public void ApplyCurrencyConversion(CurrencyConversionPlan plan)
    {
        if (plan.SourceCurrency != Currency || plan.Lines.Count != Items.Count)
            throw new InvalidOperationException("Currency conversion preview is no longer current.");
        if (!GetCurrencyConversionLines().SequenceEqual(plan.Lines.Select(line => line.SourceLine)))
            throw new InvalidOperationException("Currency conversion preview is no longer current.");
        // Only mutate once every line has been checked.
        for (var index = 0; index < Items.Count; index++)
            Items[index].Reprice(plan.TargetCurrency, plan.Rate, plan.Lines[index].NewUnitPrice);
        _currency = plan.TargetCurrency;
        ExchangeRateSnapshot = plan.Rate;
        Contract.Currency = _currency;
        Contract.ExchangeRate = plan.Rate;
        OnPropertyChanged(nameof(Currency));
        NotifyTotals();
        Status = $"Currency changed to {_currency}. Save to persist the change.";
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
        private set { if (_status == value) { OnPropertyChanged(); return; } _status = value; OnPropertyChanged(); }
    }

    public ContractEditorViewModel(Contract? source, AppSettingsService settings)
    {
        _settings = settings;
        IsNew = source is null || source.Id == 0;
        var policy = settings.GetContractPolicy();
        var currentRate = policy.CnyPerUsd ?? 0m;
        Contract = source is null ? ContractRules.CreateDraft(DateTime.Now, policy) : ContractRules.Clone(source);
        if (IsNew) ContractRules.ApplyDraftDefaults(Contract, policy);
        _currency = QuotationRules.NormalizeCurrency(Contract.Currency);
        ExchangeRateSnapshot = ContractRules.ResolveExchangeRate(Contract, currentRate);
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
        if (!ContractRules.TryGetArticleUnitPrice(SelectedArticle, Currency, ExchangeRateSnapshot, out var price, out var error))
        { Status = error; return; }
        var row = new ContractItemRowViewModel(ContractRules.CreateItemSnapshot(SelectedArticle, Currency, ExchangeRateSnapshot, price));
        AddRow(row);
        SelectedItem = row;
        Status = $"Added '{row.ArticleName}'.";
    }

    public void AddPosition(ContractItemRowViewModel row)
    {
        AddRow(row);
        SelectedItem = row;
        Status = $"Position '{row.ArticleName}' added. Save the document to persist it.";
    }

    public void RemovePosition(ContractItemRowViewModel row)
    {
        row.PropertyChanged -= Item_PropertyChanged;
        Items.Remove(row);
        if (ReferenceEquals(SelectedItem, row))
            SelectedItem = null;
        NotifyTotals();
        Status = "Contract item removed. Save to persist the change.";
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null) { Status = "Select a contract item first."; return; }
        RemovePosition(SelectedItem);
    }

    public bool TryPrepareForExport() => TryPrepareDocument(requireItems: true);

    public async Task<bool> SaveAsync()
    {
        if (!TryPrepareDocument(requireItems: false)) return false;
        try
        {
            using var scope = App.Services.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IContractWorkflow>();
            var error = await workflow.SavePreparedAsync(Contract, IsNew, SelectedCustomerContact?.Id);
            if (error is not null) { Status = error; return false; }
            IsNew = false;
            var savedItems = Contract.Items.OrderBy(item => item.SortOrder).ToList();
            for (var index = 0; index < Items.Count; index++)
                Items[index].AcceptSavedIdentity(savedItems[index].Id);

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
            var workflow = scope.ServiceProvider.GetRequiredService<IContractWorkflow>();
            await workflow.DeleteAsync(Contract.Id);
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex) { Status = $"Delete failed: {ex.Message}"; return false; }
    }

    public void SetStatusMessage(string message) => Status = message;

    private bool TryPrepareDocument(bool requireItems)
    {
        if (ContractRules.TryPrepare(Contract, requireItems, SelectedCustomer, SelectedUser,
            SelectedCustomerContact, ContractDate?.DateTime, CustomerPoDate?.DateTime,
            Currency, ExchangeRateSnapshot, Items.Select((item, index) => item.ToEntity(index)).ToList(),
            out var error, out var invalidIndex))
            return true;
        if (invalidIndex >= 0) SelectedItem = Items[invalidIndex];
        Status = error;
        return false;
    }

    private void RefreshCustomerContacts()
    {
        CustomerContacts.Clear();
        SelectedCustomerContact = null;
        if (SelectedCustomer is null) return;

        foreach (var contact in SelectedCustomer.Contacts.OrderBy(c => c.Name)) CustomerContacts.Add(contact);
        if (SelectedCustomer.Id == Contract.CustomerId && !string.IsNullOrWhiteSpace(Contract.CustomerContactSnapshot))
            SelectedCustomerContact = CustomerContacts.FirstOrDefault(c => string.Equals(ContractRules.BuildContactName(c), Contract.CustomerContactSnapshot, StringComparison.OrdinalIgnoreCase));
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
