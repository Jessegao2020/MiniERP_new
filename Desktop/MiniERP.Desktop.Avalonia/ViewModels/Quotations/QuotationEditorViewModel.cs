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
    private Customer? _selectedCustomer;
    private CustomerContact? _selectedCustomerContact;
    private User? _selectedUser;
    private Article? _selectedArticle;
    private QuotationItemRowViewModel? _selectedItem;
    private DateTimeOffset? _quotationDate;
    private DateTimeOffset? _validUntil;
    private string _currency;
    private decimal _exchangeRateSnapshot;
    private string _tier1Label;
    private string _tier2Label;
    private string _tier3Label;
    private string _status = string.Empty;

    public Quotation Quotation { get; }
    public bool IsNew { get; private set; }

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<CustomerContact> CustomerContacts { get; } = new();
    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<Article> Articles { get; } = new();
    public ObservableCollection<QuotationItemRowViewModel> Items { get; } = new();
    public ObservableCollection<string> Currencies { get; } = new() { "USD", "CNY" };

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
                Status = "Remove quotation items before changing currency.";
                OnPropertyChanged();
                return;
            }

            _currency = normalized;
            Quotation.Currency = normalized;
            OnPropertyChanged();
            NotifyTotals();
        }
    }

    public string Tier1Label
    {
        get => _tier1Label;
        set
        {
            var normalized = value ?? string.Empty;
            if (_tier1Label == normalized) return;
            _tier1Label = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Tier1TotalText));
            OnPropertyChanged(nameof(TotalText));
        }
    }

    public string Tier2Label
    {
        get => _tier2Label;
        set
        {
            var normalized = value ?? string.Empty;
            if (_tier2Label == normalized) return;
            _tier2Label = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Tier2TotalText));
        }
    }

    public string Tier3Label
    {
        get => _tier3Label;
        set
        {
            var normalized = value ?? string.Empty;
            if (_tier3Label == normalized) return;
            _tier3Label = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Tier3TotalText));
        }
    }

    public string ExchangeRateDisplay
        => ExchangeRateSnapshot > 0
            ? $"1 USD = {ExchangeRateSnapshot:0.####} CNY"
            : "Not configured";

    public decimal Tier1Total => Items.Sum(item => item.LineTotal);
    public decimal Tier2Total => Items.Sum(item => item.LineTotal2);
    public decimal Tier3Total => Items.Sum(item => item.LineTotal3);

    public string Tier1TotalText => $"{FallbackTierLabel(Tier1Label, "Tier 1")}: {Currency} {Tier1Total:N2}";
    public string Tier2TotalText => $"{FallbackTierLabel(Tier2Label, "Tier 2")}: {Currency} {Tier2Total:N2}";
    public string Tier3TotalText => $"{FallbackTierLabel(Tier3Label, "Tier 3")}: {Currency} {Tier3Total:N2}";

    // Compatibility aliases used by older bindings.
    public decimal TotalAmount => Tier1Total;
    public string TotalText => Tier1TotalText;

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
            _quotationDate = value;
            OnPropertyChanged();
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
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public QuotationEditorViewModel(Quotation? source, AppSettingsService settings)
    {
        _settings = settings;
        IsNew = source is null;
        var currentRate = settings.Current.CnyPerUsd ?? 0m;

        Quotation = source is null
            ? new Quotation
            {
                QuotationNumber = $"QT-{DateTime.Now:yyyyMMdd-HHmmssfff}",
                QuotationDate = DateTime.Now,
                ValidUntil = DateTime.Today.AddDays(30),
                Currency = "USD",
                ExchangeRate = currentRate,
                Tier1Label = "100 Sets",
                Tier2Label = "500 Sets",
                Tier3Label = "1000 Sets"
            }
            : Clone(source);

        _currency = string.IsNullOrWhiteSpace(Quotation.Currency) ? "USD" : Quotation.Currency.ToUpperInvariant();
        _tier1Label = string.IsNullOrWhiteSpace(Quotation.Tier1Label) ? "100 Sets" : Quotation.Tier1Label;
        _tier2Label = string.IsNullOrWhiteSpace(Quotation.Tier2Label) ? "500 Sets" : Quotation.Tier2Label;
        _tier3Label = string.IsNullOrWhiteSpace(Quotation.Tier3Label) ? "1000 Sets" : Quotation.Tier3Label;

        // The first quotation-items migration used 1.0 as a compatibility default for
        // quotation headers that existed before exchange-rate snapshots were introduced.
        // Such a header has no real historical snapshot, so use the current configured rate.
        var isLegacyPlaceholderRate = source is not null
            && source.Items.Count == 0
            && source.ExchangeRate == 1m
            && currentRate > 0m
            && currentRate != 1m;

        ExchangeRateSnapshot = source is null || source.ExchangeRate <= 0m || isLegacyPlaceholderRate
            ? currentRate
            : source.ExchangeRate;

        Quotation.Currency = _currency;
        Quotation.ExchangeRate = ExchangeRateSnapshot;
        Quotation.Tier1Label = _tier1Label;
        Quotation.Tier2Label = _tier2Label;
        Quotation.Tier3Label = _tier3Label;

        QuotationDate = new DateTimeOffset(Quotation.QuotationDate);
        ValidUntil = Quotation.ValidUntil is null
            ? null
            : new DateTimeOffset(Quotation.ValidUntil.Value);

        foreach (var item in Quotation.Items.OrderBy(item => item.Id))
            AddRow(new QuotationItemRowViewModel(item));
    }

    public void RefreshExchangeRateFromSettings()
    {
        if (!IsNew)
            return;

        if (Items.Count > 0)
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

        if (SelectedArticle.Price is null)
        {
            Status = $"Article '{SelectedArticle.Name}' does not have a CNY price.";
            return;
        }

        if (Currency == "USD" && ExchangeRateSnapshot <= 0)
        {
            Status = "Set the USD exchange rate in Settings > System before adding USD items.";
            return;
        }

        var unitPrice = Currency == "USD"
            ? decimal.Round(SelectedArticle.Price.Value / ExchangeRateSnapshot, 2, MidpointRounding.AwayFromZero)
            : SelectedArticle.Price.Value;

        var row = new QuotationItemRowViewModel(
            SelectedArticle,
            Currency,
            ExchangeRateSnapshot,
            unitPrice,
            Tier1Label,
            Tier2Label,
            Tier3Label);

        AddRow(row);
        SelectedItem = row;
        Status = $"Added '{row.ArticleName}' with three quantity-tier price snapshots.";
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

    public bool TryPrepareForOutput(bool requireItems = false)
    {
        if (string.IsNullOrWhiteSpace(Quotation.QuotationNumber))
        {
            Status = "Quotation number is required.";
            return false;
        }

        if (SelectedCustomer is null)
        {
            Status = "Customer is required.";
            return false;
        }

        if (SelectedUser is null)
        {
            Status = "User is required. Create one in Settings > User if necessary.";
            return false;
        }

        if (QuotationDate is null)
        {
            Status = "Quotation date is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Tier1Label) ||
            string.IsNullOrWhiteSpace(Tier2Label) ||
            string.IsNullOrWhiteSpace(Tier3Label))
        {
            Status = "All three quantity-tier labels are required.";
            return false;
        }

        if (requireItems && Items.Count == 0)
        {
            Status = "Add at least one quotation item before exporting.";
            return false;
        }

        if (Currency == "USD" && Items.Count > 0 && ExchangeRateSnapshot <= 0)
        {
            Status = "A valid USD exchange-rate snapshot is required.";
            return false;
        }

        foreach (var item in Items)
        {
            if (!item.TryValidate(out var error))
            {
                SelectedItem = item;
                Status = error;
                return false;
            }
        }

        var customerChanged = Quotation.CustomerId != 0 && Quotation.CustomerId != SelectedCustomer.Id;
        var userChanged = Quotation.UserId != 0 && Quotation.UserId != SelectedUser.Id;

        Quotation.QuotationNumber = Quotation.QuotationNumber.Trim();
        Quotation.QuotationDate = QuotationDate.Value.DateTime;
        Quotation.ValidUntil = ValidUntil?.DateTime;
        Quotation.Currency = Currency;
        Quotation.ExchangeRate = ExchangeRateSnapshot;
        Quotation.Tier1Label = Tier1Label.Trim();
        Quotation.Tier2Label = Tier2Label.Trim();
        Quotation.Tier3Label = Tier3Label.Trim();

        if (IsNew || customerChanged || string.IsNullOrWhiteSpace(Quotation.CustomerNameSnapshot))
        {
            Quotation.CustomerNameSnapshot = SelectedCustomer.Name.Trim();
            Quotation.CustomerAddressSnapshot = BuildCustomerAddress(SelectedCustomer);
            Quotation.CustomerContactSnapshot = SelectedCustomerContact is null
                ? null
                : BuildContactName(SelectedCustomerContact);
        }
        else if (SelectedCustomerContact is not null)
        {
            // Selecting another contact is an explicit edit of the quotation.
            Quotation.CustomerContactSnapshot = BuildContactName(SelectedCustomerContact);
        }

        if (IsNew || userChanged || string.IsNullOrWhiteSpace(Quotation.SalesContactNameSnapshot))
        {
            Quotation.SalesContactNameSnapshot = SelectedUser.Name.Trim();
            Quotation.SalesContactPhoneSnapshot = SelectedUser.Phone;
            Quotation.SalesContactEmailSnapshot = SelectedUser.Email;
        }

        Quotation.CustomerId = SelectedCustomer.Id;
        Quotation.UserId = SelectedUser.Id;
        Quotation.Customer = null;
        Quotation.User = null;
        Quotation.Items = Items.Select(item => item.ToEntity()).ToList();

        return true;
    }

    public async Task<bool> SaveAsync()
    {
        if (!TryPrepareForOutput())
            return false;

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();

            var sameNumber = await service.GetQuotationByNumberAsync(Quotation.QuotationNumber);
            if (sameNumber is not null && sameNumber.Id != Quotation.Id)
            {
                Status = "Quotation number already exists.";
                return false;
            }

            if (IsNew)
            {
                await service.CreateQuotationAsync(Quotation);
                IsNew = false;
            }
            else
            {
                await service.UpdateQuotationAsync(Quotation);
            }

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
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            await service.DeleteQuotationAsync(Quotation.Id);
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
            return false;
        }
    }

    public void SetStatusMessage(string message)
        => Status = message;

    private void RefreshCustomerContacts()
    {
        CustomerContacts.Clear();
        SelectedCustomerContact = null;

        if (SelectedCustomer is null)
            return;

        foreach (var contact in SelectedCustomer.Contacts.OrderBy(contact => contact.Name))
            CustomerContacts.Add(contact);

        var sameCustomerAsSnapshot = SelectedCustomer.Id == Quotation.CustomerId;
        if (sameCustomerAsSnapshot && !string.IsNullOrWhiteSpace(Quotation.CustomerContactSnapshot))
        {
            SelectedCustomerContact = CustomerContacts.FirstOrDefault(contact =>
                string.Equals(
                    BuildContactName(contact),
                    Quotation.CustomerContactSnapshot,
                    StringComparison.OrdinalIgnoreCase));
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

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => NotifyTotals();

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(Tier1Total));
        OnPropertyChanged(nameof(Tier2Total));
        OnPropertyChanged(nameof(Tier3Total));
        OnPropertyChanged(nameof(Tier1TotalText));
        OnPropertyChanged(nameof(Tier2TotalText));
        OnPropertyChanged(nameof(Tier3TotalText));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalText));
    }

    private static string BuildContactName(CustomerContact contact)
    {
        var title = contact.Title?.Trim();
        var name = contact.Name.Trim();

        return string.IsNullOrWhiteSpace(title)
            ? name
            : $"{title} {name}".Trim();
    }

    private static string? BuildCustomerAddress(Customer customer)
    {
        var lines = new List<string>();

        AddIfNotEmpty(lines, customer.AddressLine1);
        AddIfNotEmpty(lines, customer.AddressLine2);

        var cityLineParts = new[]
        {
            customer.PostalCode?.Trim(),
            customer.City?.Trim(),
            customer.State?.Trim()
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        var cityLine = string.Join(" ", cityLineParts);
        AddIfNotEmpty(lines, cityLine);
        AddIfNotEmpty(lines, customer.Country);

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static void AddIfNotEmpty(ICollection<string> target, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target.Add(value.Trim());
    }

    private static string FallbackTierLabel(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static Quotation Clone(Quotation source) => new()
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
        Tier1Label = source.Tier1Label,
        Tier2Label = source.Tier2Label,
        Tier3Label = source.Tier3Label,
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
        SourceArticleId = source.SourceArticleId,
        ArticleName = source.ArticleName,
        Description = source.Description,
        Specification = source.Specification,
        Unit = source.Unit,
        Quantity = source.Quantity,
        UnitPrice = source.UnitPrice,
        Quantity2 = source.Quantity2,
        UnitPrice2 = source.UnitPrice2,
        Quantity3 = source.Quantity3,
        UnitPrice3 = source.UnitPrice3,
        DiscountPercent = source.DiscountPercent,
        Currency = source.Currency,
        ExchangeRateSnapshot = source.ExchangeRateSnapshot,
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
