using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Quotations;

public sealed class QuotationEditorViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settings;
    private readonly int _defaultValidityDays;
    private Customer? _selectedCustomer;
    private CustomerContact? _selectedCustomerContact;
    private User? _selectedUser;
    private Article? _selectedArticle;
    private QuotationItemRowViewModel? _selectedItem;
    private DateTimeOffset? _quotationDate;
    private DateTimeOffset? _validUntil;
    private string _currency;
    private decimal _exchangeRateSnapshot;
    private string _status = string.Empty;
    private int _numberPreviewVersion;
    private bool _quotationNumberManuallyEdited;

    public Quotation Quotation { get; }
    public bool IsNew { get; private set; }
    // A newly created quotation gets a sequential preview, but the operator can
    // override it. A manual number must never be replaced by later async previews.
    public string QuotationNumber
    {
        get => Quotation.QuotationNumber;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(Quotation.QuotationNumber, next, StringComparison.Ordinal)) return;
            _quotationNumberManuallyEdited = true;
            ++_numberPreviewVersion;
            Quotation.QuotationNumber = next;
            OnPropertyChanged();
        }
    }
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<CustomerContact> CustomerContacts { get; } = new();
    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<Article> Articles { get; } = new();
    public ObservableCollection<QuotationItemRowViewModel> Items { get; } = new();
    public ObservableCollection<string> Currencies { get; } = new() { "USD", "CNY" };
    public IReadOnlyList<string> DeliveryTermOptions { get; } = new[] { "FCA Baoding", "CFR", "CPT" };
    public IReadOnlyList<string> LeadTimeOptions { get; } = new[] { "2 business days", "1 week", "4 weeks", "4-6 weeks" };

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
            var normalized = QuotationRules.NormalizeCurrency(value);
            if (_currency == normalized) return;

            if (!QuotationRules.IsSupportedCurrency(normalized))
            {
                Status = $"Unsupported quotation currency: {normalized}.";
                OnPropertyChanged();
                return;
            }

            if (Items.Count > 0 && !TryRepriceItems(normalized, out var error))
            {
                Status = error;
                OnPropertyChanged();
                return;
            }

            _currency = normalized;
            Quotation.Currency = normalized;
            OnPropertyChanged();
            NotifyTotals();

            Status = Items.Count == 0
                ? $"Quotation currency changed to {normalized}."
                : $"Quotation repriced in {normalized} using the current Article CNY untaxed prices.";
        }
    }

    public string ExchangeRateDisplay
        => ExchangeRateSnapshot > 0
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
        set
        {
            if (ReferenceEquals(_selectedCustomerContact, value)) return;
            _selectedCustomerContact = value;
            OnPropertyChanged();
        }
    }

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (ReferenceEquals(_selectedUser, value)) return;
            _selectedUser = value;
            OnPropertyChanged();
            if (IsNew && !_quotationNumberManuallyEdited)
                _ = RefreshQuotationNumberPreviewAsync();
        }
    }

    public Article? SelectedArticle
    {
        get => _selectedArticle;
        set
        {
            if (ReferenceEquals(_selectedArticle, value)) return;
            _selectedArticle = value;
            OnPropertyChanged();
        }
    }

    public QuotationItemRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value)) return;
            _selectedItem = value;
            OnPropertyChanged();
        }
    }

    public DateTimeOffset? QuotationDate
    {
        get => _quotationDate;
        set
        {
            if (_quotationDate == value) return;
            // Follow the configured default if the user has not customized Valid Until.
            // Once edited manually, the expiration date must not be overwritten.
            var followDefault = QuotationRules.FollowsDefaultValidity(IsNew, _quotationDate?.DateTime, _validUntil?.DateTime, _defaultValidityDays);
            _quotationDate = value;
            OnPropertyChanged();
            if (followDefault && value is not null)
                ValidUntil = new DateTimeOffset(value.Value.Date.AddDays(_defaultValidityDays));
        }
    }

    public DateTimeOffset? ValidUntil
    {
        get => _validUntil;
        set
        {
            if (_validUntil == value) return;
            _validUntil = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value) { OnPropertyChanged(); return; }
            _status = value;
            OnPropertyChanged();
        }
    }

    public QuotationEditorViewModel(Quotation? source, AppSettingsService settings)
    {
        _settings = settings;
        var policy = settings.GetQuotationPolicy();
        _defaultValidityDays = policy.DefaultValidityDays;
        IsNew = source is null;
        var currentRate = settings.Current.CnyPerUsd ?? 0m;
        Quotation = source is null
            ? QuotationRules.CreateDraft(DateTime.Now, policy)
            : QuotationRules.Clone(source);
        _currency = QuotationRules.NormalizeCurrency(Quotation.Currency);
        ExchangeRateSnapshot = QuotationRules.ResolveExchangeRate(source, currentRate);

        Quotation.Currency = _currency;
        Quotation.ExchangeRate = ExchangeRateSnapshot;
        QuotationDate = new DateTimeOffset(Quotation.QuotationDate);
        ValidUntil = Quotation.ValidUntil is null ? null : new DateTimeOffset(Quotation.ValidUntil.Value);

        foreach (var item in Quotation.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
            AddRow(new QuotationItemRowViewModel(item));
    }

    private async Task RefreshQuotationNumberPreviewAsync()
    {
        if (_quotationNumberManuallyEdited) return;
        var version = ++_numberPreviewVersion;
        var user = SelectedUser;
        if (user is null)
        {
            Quotation.QuotationNumber = string.Empty;
            OnPropertyChanged(nameof(QuotationNumber));
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IQuotationWorkflow>();
            var number = await workflow.PreviewNumberAsync(user.Id, DateTime.Now);

            if (!IsNew || _quotationNumberManuallyEdited || version != _numberPreviewVersion || !ReferenceEquals(user, SelectedUser))
                return;

            Quotation.QuotationNumber = number;
            OnPropertyChanged(nameof(QuotationNumber));
        }
        catch (Exception ex)
        {
            if (!IsNew || _quotationNumberManuallyEdited || version != _numberPreviewVersion)
                return;

            Quotation.QuotationNumber = string.Empty;
            OnPropertyChanged(nameof(QuotationNumber));
            Status = $"Quotation number unavailable: {ex.Message}";
        }
    }

    public void RefreshExchangeRateFromSettings()
    {
        if (!IsNew)
            return;

        if (!QuotationRules.CanRefreshExchangeRate(IsNew, Items.Count))
        {
            Status = "Exchange rate changed in Settings, but this quotation already has items. Remove the items before adopting the new rate.";
            return;
        }

        var currentRate = _settings.Current.CnyPerUsd ?? 0m;
        ExchangeRateSnapshot = currentRate;
        Quotation.ExchangeRate = currentRate;
        Status = currentRate > 0m
            ? $"Exchange rate refreshed: 1 USD = {currentRate:0.####} CNY."
            : "USD exchange rate is not configured.";
    }

    public async Task LoadLookupsAsync()
    {
        try
        {
            if (IsNew && Items.Count == 0)
                RefreshExchangeRateFromSettings();

            using var scope = App.Services.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var articleService = scope.ServiceProvider.GetRequiredService<IArticleService>();

            var customers = await customerService.GetAllCustomersAsync();
            var users = await userService.GetAllUsersAsync();
            var articles = await articleService.GetAllArticlesAsync();

            Customers.Clear();
            foreach (var customer in customers.Where(customer => customer.IsActive).OrderBy(customer => customer.Name))
                Customers.Add(customer);

            Users.Clear();
            foreach (var user in users.OrderBy(user => user.Name))
                Users.Add(user);

            Articles.Clear();
            foreach (var article in articles.OrderBy(article => article.Name))
                Articles.Add(article);

            SelectedCustomer = Customers.FirstOrDefault(customer => customer.Id == Quotation.CustomerId);
            SelectedUser = Users.FirstOrDefault(user => user.Id == Quotation.UserId);

            if (IsNew && SelectedCustomer is null && Customers.Count == 1)
                SelectedCustomer = Customers[0];

            if (IsNew && SelectedUser is null && Users.Count == 1)
                SelectedUser = Users[0];

            if (Customers.Count == 0)
                Status = "Create an active customer before saving a quotation.";
            else if (Users.Count == 0)
                Status = "Create a user in Settings > User before saving a quotation.";
            else if (Articles.Count == 0)
                Status = "Quotation header is ready. Create an Article before adding line items.";
            else if (Currency == "USD" && ExchangeRateSnapshot <= 0)
                Status = "Set the USD exchange rate in Settings > System before adding USD items.";
            else if (string.IsNullOrWhiteSpace(Status) || Status.StartsWith("Exchange rate refreshed", StringComparison.Ordinal))
                Status = IsNew ? "New quotation." : "Ready.";
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    public void AddSelectedArticle()
    {
        if (SelectedArticle is null)
        {
            Status = "Select an Article first.";
            return;
        }

        if (!QuotationRules.TryCreateItem(SelectedArticle, Currency, ExchangeRateSnapshot, out var item, out var error))
        {
            Status = error;
            return;
        }

        var row = new QuotationItemRowViewModel(item!);
        AddRow(row);
        SelectedItem = row;
        Status = $"Added '{row.ArticleName}' using the current {Currency} price snapshot.";
    }

    private bool TryRepriceItems(string targetCurrency, out string error)
    {
        var items = Items.Select((row, index) => row.ToEntity(index)).ToList();
        if (!QuotationRules.TryPlanRepricing(items, Articles, targetCurrency, ExchangeRateSnapshot, out var prices, out error))
            return false;

        for (var index = 0; index < Items.Count; index++)
            Items[index].Reprice(targetCurrency, ExchangeRateSnapshot, prices[index]);

        return true;
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null)
        {
            Status = "Select a quotation item first.";
            return;
        }

        SelectedItem.PropertyChanged -= Item_PropertyChanged;
        Items.Remove(SelectedItem);
        SelectedItem = null;
        NotifyTotals();
        Status = "Quotation item removed. Save to persist the change.";
    }

    public void MoveSelectedItemUp()
    {
        if (SelectedItem is null)
        {
            Status = "Select a quotation item first.";
            return;
        }

        var index = Items.IndexOf(SelectedItem);
        if (index <= 0)
        {
            Status = "The selected item is already first.";
            return;
        }

        Items.Move(index, index - 1);
        Status = "Quotation item moved up. Save to persist the new order.";
    }

    public void MoveSelectedItemDown()
    {
        if (SelectedItem is null)
        {
            Status = "Select a quotation item first.";
            return;
        }

        var index = Items.IndexOf(SelectedItem);
        if (index < 0 || index >= Items.Count - 1)
        {
            Status = "The selected item is already last.";
            return;
        }

        Items.Move(index, index + 1);
        Status = "Quotation item moved down. Save to persist the new order.";
    }

    public bool TryPrepareForExport()
        => TryPrepareDocument(requireItems: true);

    public async Task<bool> SaveAsync()
    {
        if (!TryPrepareDocument(requireItems: false))
            return false;

        try
        {
            using var scope = App.Services.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IQuotationWorkflow>();

            if (IsNew) ++_numberPreviewVersion;
            var error = await workflow.SavePreparedAsync(Quotation, IsNew,
                _quotationNumberManuallyEdited, SelectedCustomerContact?.Id);
            if (error is not null)
            {
                Status = error;
                return false;
            }
            if (IsNew)
            {
                IsNew = false;
                OnPropertyChanged(nameof(QuotationNumber));
            }

            // Persisted line identities must survive another edit/save in the same window.
            var savedItems = Quotation.Items.OrderBy(item => item.SortOrder).ToList();
            for (var index = 0; index < Items.Count; index++)
                Items[index].AcceptSavedIdentity(savedItems[index].Id);
            Status = "Saved.";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteAsync()
    {
        if (IsNew || Quotation.Id == 0)
            return true;

        try
        {
            using var scope = App.Services.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IQuotationWorkflow>();
            await workflow.DeleteAsync(Quotation.Id);
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
            return false;
        }
    }

    public void SetStatusMessage(string message) => Status = message;

    private bool TryPrepareDocument(bool requireItems)
    {
        var items = Items.Select((item, index) => item.ToEntity(index)).ToList();
        if (QuotationRules.TryPrepare(Quotation, IsNew, _quotationNumberManuallyEdited, requireItems,
            SelectedCustomer, SelectedUser, SelectedCustomerContact, QuotationDate?.DateTime, ValidUntil?.DateTime,
            Currency, ExchangeRateSnapshot, items, out var error, out var invalidItemIndex))
            return true;

        if (invalidItemIndex >= 0)
            SelectedItem = Items[invalidItemIndex];
        Status = error;
        return false;
    }

    private void RefreshCustomerContacts()
    {
        CustomerContacts.Clear();
        SelectedCustomerContact = null;

        if (SelectedCustomer is null)
            return;

        foreach (var contact in SelectedCustomer.Contacts.OrderBy(contact => contact.Name))
            CustomerContacts.Add(contact);

        if (SelectedCustomer.Id == Quotation.CustomerId && !string.IsNullOrWhiteSpace(Quotation.CustomerContactSnapshot))
        {
            SelectedCustomerContact = CustomerContacts.FirstOrDefault(contact =>
                string.Equals(QuotationRules.BuildContactName(contact), Quotation.CustomerContactSnapshot, StringComparison.OrdinalIgnoreCase));
        }

        if (IsNew && SelectedCustomerContact is null && CustomerContacts.Count == 1)
            SelectedCustomerContact = CustomerContacts[0];
    }

    private void AddRow(QuotationItemRowViewModel row)
    {
        row.PropertyChanged += Item_PropertyChanged;
        Items.Add(row);
        NotifyTotals();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e) => NotifyTotals();

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
