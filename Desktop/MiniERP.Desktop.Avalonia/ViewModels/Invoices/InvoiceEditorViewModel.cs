using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Invoices;

public sealed class InvoiceEditorViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settings;
    private Customer? _selectedCustomer;
    private CustomerContact? _selectedCustomerContact;
    private User? _selectedUser;
    private Article? _selectedArticle;
    private InvoiceItemRowViewModel? _selectedItem;
    private DateTimeOffset? _invoiceDate;
    private DateTimeOffset? _dueDate;
    private DateTimeOffset? _customerPoDate;
    private string _currency;
    private decimal _exchangeRateSnapshot;
    private string _status = string.Empty;

    public Invoice Invoice { get; }
    public bool IsNew { get; private set; }
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<CustomerContact> CustomerContacts { get; } = new();
    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<Article> Articles { get; } = new();
    public ObservableCollection<InvoiceItemRowViewModel> Items { get; } = new();
    public ObservableCollection<string> Currencies { get; } = new() { "USD", "CNY" };
    public IReadOnlyList<string> DeliveryTermOptions { get; } = new[] { "FCA Baoding", "CFR", "CPT" };
    public IReadOnlyList<string> LeadTimeOptions { get; } = new[] { "2 business days", "1 week", "4 weeks", "4-6 weeks" };

    public string DocumentTitle => Invoice.Type == InvoiceType.Proforma ? "Proforma Invoice" : "Commercial Invoice";
    public string SourceText => Invoice.SourceDocumentType == DocumentSourceType.None
        ? "Created manually"
        : $"Created from {Invoice.SourceDocumentType}: {Invoice.SourceDocumentNumber}";

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
        for (var index = 0; index < Items.Count; index++)
        {
            var line = Items[index];
            var preview = plan.Lines[index];
            if (line.ArticleName != preview.Name || line.UnitPrice != preview.OldUnitPrice ||
                line.Quantity != preview.Quantity || line.DiscountPercent != preview.DiscountPercent)
                throw new InvalidOperationException("Currency conversion preview is no longer current.");
        }
        // Only mutate once every line has been checked.
        for (var index = 0; index < Items.Count; index++)
            Items[index].Reprice(plan.TargetCurrency, plan.Rate, plan.Lines[index].NewUnitPrice);
        _currency = plan.TargetCurrency;
        ExchangeRateSnapshot = plan.Rate;
        Invoice.Currency = _currency;
        Invoice.ExchangeRate = plan.Rate;
        OnPropertyChanged(nameof(Currency));
        NotifyTotals();
        Status = $"Currency changed to {_currency}. Save to persist the change.";
    }

    public string ExchangeRateDisplay => ExchangeRateSnapshot > 0 ? $"1 USD = {ExchangeRateSnapshot:0.####} CNY" : "Not configured";
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

    public InvoiceItemRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set { if (ReferenceEquals(_selectedItem, value)) return; _selectedItem = value; OnPropertyChanged(); }
    }

    public DateTimeOffset? InvoiceDate { get => _invoiceDate; set { if (_invoiceDate == value) return; _invoiceDate = value; OnPropertyChanged(); } }
    public DateTimeOffset? DueDate { get => _dueDate; set { if (_dueDate == value) return; _dueDate = value; OnPropertyChanged(); } }
    public DateTimeOffset? CustomerPoDate { get => _customerPoDate; set { if (_customerPoDate == value) return; _customerPoDate = value; OnPropertyChanged(); } }

    public string Status
    {
        get => _status;
        private set { if (_status == value) { OnPropertyChanged(); return; } _status = value; OnPropertyChanged(); }
    }

    public InvoiceEditorViewModel(Invoice? source, InvoiceType type, AppSettingsService settings)
    {
        _settings = settings;
        IsNew = source is null || source.Id == 0;
        var policy = settings.GetInvoicePolicy();
        var currentRate = policy.CnyPerUsd ?? 0m;
        Invoice = source is null
            ? InvoiceRules.CreateDraft(type, DateTime.Now, policy)
            : InvoiceRules.Clone(source);
        if (IsNew) InvoiceRules.ApplyDraftDefaults(Invoice, policy);

        Invoice.Type = type;
        _currency = string.IsNullOrWhiteSpace(Invoice.Currency) ? "USD" : Invoice.Currency.ToUpperInvariant();
        ExchangeRateSnapshot = InvoiceRules.ResolveExchangeRate(Invoice, currentRate);
        Invoice.Currency = _currency;
        Invoice.ExchangeRate = ExchangeRateSnapshot;
        InvoiceDate = new DateTimeOffset(Invoice.InvoiceDate);
        DueDate = Invoice.DueDate is null ? null : new DateTimeOffset(Invoice.DueDate.Value);
        CustomerPoDate = Invoice.CustomerPoDate is null ? null : new DateTimeOffset(Invoice.CustomerPoDate.Value);

        foreach (var item in Invoice.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
            AddRow(new InvoiceItemRowViewModel(item));
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
            foreach (var customer in customers
                         .Where(c => c.IsActive || c.Id == Invoice.CustomerId)
                         .OrderBy(c => c.Name))
                Customers.Add(customer);

            Users.Clear();
            foreach (var user in users.OrderBy(u => u.Name)) Users.Add(user);
            Articles.Clear();
            foreach (var article in articles.OrderBy(a => a.Name)) Articles.Add(article);

            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == Invoice.CustomerId);
            SelectedUser = Users.FirstOrDefault(u => u.Id == Invoice.UserId);
            if (IsNew && Invoice.SourceDocumentType == DocumentSourceType.None && SelectedCustomer is null && Customers.Count == 1) SelectedCustomer = Customers[0];
            if (IsNew && Invoice.SourceDocumentType == DocumentSourceType.None && SelectedUser is null && Users.Count == 1) SelectedUser = Users[0];

            Status = Customers.Count == 0 ? "Create an active customer before saving."
                : Users.Count == 0 ? "Create a user in Settings > User before saving."
                : "Ready.";
        }
        catch (Exception ex) { Status = $"Load failed: {ex.Message}"; }
    }

    public void AddSelectedArticle()
    {
        if (SelectedArticle is null) { Status = "Select an Article first."; return; }
        if (!InvoiceRules.TryGetArticleUnitPrice(SelectedArticle, Currency, ExchangeRateSnapshot, out var price, out var error))
        {
            Status = error;
            return;
        }
        var row = new InvoiceItemRowViewModel(InvoiceRules.CreateItemSnapshot(SelectedArticle, Currency, ExchangeRateSnapshot, price));
        AddRow(row);
        SelectedItem = row;
        Status = $"Added '{row.ArticleName}'.";
    }

    public void AddPosition(InvoiceItemRowViewModel row)
    {
        AddRow(row);
        SelectedItem = row;
        Status = $"Position '{row.ArticleName}' added. Save the document to persist it.";
    }

    public void RemovePosition(InvoiceItemRowViewModel row)
    {
        row.PropertyChanged -= Item_PropertyChanged;
        Items.Remove(row);
        if (ReferenceEquals(SelectedItem, row))
            SelectedItem = null;
        NotifyTotals();
        Status = "Invoice item removed. Save to persist the change.";
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null) { Status = "Select an invoice item first."; return; }
        RemovePosition(SelectedItem);
    }

    public bool TryPrepareForExport() => TryPrepareDocument(requireItems: true);

    public async Task<bool> SaveAsync()
    {
        if (!TryPrepareDocument(requireItems: false)) return false;
        try
        {
            using var scope = App.Services.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IInvoiceWorkflow>();
            var error = await workflow.SavePreparedAsync(Invoice, IsNew, SelectedCustomerContact?.Id);
            if (error is not null) { Status = error; return false; }
            IsNew = false;
            var savedItems = Invoice.Items.OrderBy(item => item.SortOrder).ToList();
            for (var index = 0; index < Items.Count; index++)
                Items[index].AcceptSavedIdentity(savedItems[index].Id);
            Status = "Saved.";
            return true;
        }
        catch (Exception ex) { Status = $"Save failed: {ex.Message}"; return false; }
    }

    public async Task<bool> DeleteAsync()
    {
        if (IsNew || Invoice.Id == 0) return true;
        try
        {
            using var scope = App.Services.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IInvoiceWorkflow>();
            await workflow.DeleteAsync(Invoice.Id);
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex) { Status = $"Delete failed: {ex.Message}"; return false; }
    }

    public void SetStatusMessage(string message) => Status = message;

    private bool TryPrepareDocument(bool requireItems)
    {
        if (InvoiceRules.TryPrepare(Invoice, requireItems, SelectedCustomer, SelectedUser,
            SelectedCustomerContact, InvoiceDate?.DateTime, DueDate?.DateTime, CustomerPoDate?.DateTime,
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
        if (SelectedCustomer.Id == Invoice.CustomerId && !string.IsNullOrWhiteSpace(Invoice.CustomerContactSnapshot))
            SelectedCustomerContact = CustomerContacts.FirstOrDefault(c => string.Equals(InvoiceRules.BuildContactName(c), Invoice.CustomerContactSnapshot, StringComparison.OrdinalIgnoreCase));
        if (IsNew && Invoice.SourceDocumentType == DocumentSourceType.None && SelectedCustomerContact is null && CustomerContacts.Count == 1)
            SelectedCustomerContact = CustomerContacts[0];
    }

    private void AddRow(InvoiceItemRowViewModel row)
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
