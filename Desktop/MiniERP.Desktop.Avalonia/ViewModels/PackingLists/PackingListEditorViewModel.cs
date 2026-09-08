using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.PackingLists;

public sealed class PackingListEditorViewModel : INotifyPropertyChanged
{
    private Customer? _selectedCustomer;
    private CustomerContact? _selectedCustomerContact;
    private User? _selectedUser;
    private Article? _selectedArticle;
    private PackingListItemRowViewModel? _selectedItem;
    private PackingPackageRowViewModel? _selectedPackage;
    private DateTimeOffset? _packingDate;
    private DateTimeOffset? _customerPoDate;
    private string _status = string.Empty;

    public PackingList PackingList { get; }
    public bool IsNew { get; private set; }
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<CustomerContact> CustomerContacts { get; } = new();
    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<Article> Articles { get; } = new();
    public ObservableCollection<PackingListItemRowViewModel> Items { get; } = new();
    public ObservableCollection<PackingPackageRowViewModel> Packages { get; } = new();

    public string SourceText => PackingList.SourceDocumentType == DocumentSourceType.None
        ? "Created manually"
        : $"Created from {PackingList.SourceDocumentType}: {PackingList.SourceDocumentNumber}";

    public decimal TotalQuantity => Items.Sum(i => i.Quantity);
    public int TotalCartons => Packages.Sum(p => p.PackageCount);
    public decimal TotalNetWeight => Packages.Sum(p => p.TotalNetWeight);
    public decimal TotalGrossWeight => Packages.Sum(p => p.TotalGrossWeight);
    public decimal TotalCbm => Packages.Sum(p => p.TotalCbm);
    public string TotalsText => $"Qty {TotalQuantity:0.####}  |  Cartons {TotalCartons}  |  N.W. {TotalNetWeight:0.###} kg  |  G.W. {TotalGrossWeight:0.###} kg  |  {TotalCbm:0.####} CBM";

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set { if (ReferenceEquals(_selectedCustomer, value)) return; _selectedCustomer = value; OnPropertyChanged(); RefreshCustomerContacts(); }
    }
    public CustomerContact? SelectedCustomerContact { get => _selectedCustomerContact; set { if (ReferenceEquals(_selectedCustomerContact, value)) return; _selectedCustomerContact = value; OnPropertyChanged(); } }
    public User? SelectedUser { get => _selectedUser; set { if (ReferenceEquals(_selectedUser, value)) return; _selectedUser = value; OnPropertyChanged(); } }
    public Article? SelectedArticle { get => _selectedArticle; set { if (ReferenceEquals(_selectedArticle, value)) return; _selectedArticle = value; OnPropertyChanged(); } }
    public PackingListItemRowViewModel? SelectedItem { get => _selectedItem; set { if (ReferenceEquals(_selectedItem, value)) return; _selectedItem = value; OnPropertyChanged(); } }
    public PackingPackageRowViewModel? SelectedPackage { get => _selectedPackage; set { if (ReferenceEquals(_selectedPackage, value)) return; _selectedPackage = value; OnPropertyChanged(); } }
    public DateTimeOffset? PackingDate { get => _packingDate; set { if (_packingDate == value) return; _packingDate = value; OnPropertyChanged(); } }
    public DateTimeOffset? CustomerPoDate { get => _customerPoDate; set { if (_customerPoDate == value) return; _customerPoDate = value; OnPropertyChanged(); } }

    public string Status
    {
        get => _status;
        private set { if (_status == value) return; _status = value; OnPropertyChanged(); }
    }

    public PackingListEditorViewModel(PackingList? source)
    {
        IsNew = source is null || source.Id == 0;
        PackingList = source is null
            ? new PackingList { PackingListNumber = $"PL-{DateTime.Now:yyyyMMdd-HHmmssfff}", PackingDate = DateTime.Now }
            : Clone(source);

        PackingDate = new DateTimeOffset(PackingList.PackingDate);
        CustomerPoDate = PackingList.CustomerPoDate is null ? null : new DateTimeOffset(PackingList.CustomerPoDate.Value);
        foreach (var item in PackingList.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)) AddItemRow(new PackingListItemRowViewModel(item));
        foreach (var package in PackingList.Packages.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)) AddPackageRow(new PackingPackageRowViewModel(package));
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

            Customers.Clear(); foreach (var c in customers.Where(c => c.IsActive).OrderBy(c => c.Name)) Customers.Add(c);
            Users.Clear(); foreach (var u in users.OrderBy(u => u.Name)) Users.Add(u);
            Articles.Clear(); foreach (var a in articles.OrderBy(a => a.Name)) Articles.Add(a);

            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == PackingList.CustomerId);
            SelectedUser = Users.FirstOrDefault(u => u.Id == PackingList.UserId);
            if (IsNew && SelectedCustomer is null && Customers.Count == 1) SelectedCustomer = Customers[0];
            if (IsNew && SelectedUser is null && Users.Count == 1) SelectedUser = Users[0];
            Status = "Ready.";
        }
        catch (Exception ex) { Status = $"Load failed: {ex.Message}"; }
    }

    public void AddSelectedArticle()
    {
        if (SelectedArticle is null) { Status = "Select an Article first."; return; }
        var row = new PackingListItemRowViewModel(SelectedArticle);
        AddItemRow(row);
        SelectedItem = row;
        Status = $"Added '{row.ArticleName}'.";
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null) { Status = "Select a packing-list item first."; return; }
        SelectedItem.PropertyChanged -= Row_PropertyChanged;
        Items.Remove(SelectedItem);
        SelectedItem = null;
        NotifyTotals();
    }

    public void AddPackage()
    {
        var row = new PackingPackageRowViewModel();
        AddPackageRow(row);
        SelectedPackage = row;
        Status = "Package row added.";
    }

    public void RemoveSelectedPackage()
    {
        if (SelectedPackage is null) { Status = "Select a package row first."; return; }
        SelectedPackage.PropertyChanged -= Row_PropertyChanged;
        Packages.Remove(SelectedPackage);
        SelectedPackage = null;
        NotifyTotals();
    }

    public bool TryPrepareForExport() => TryPrepareDocument(requireItems: true, requirePackages: true);

    public async Task<bool> SaveAsync()
    {
        if (!TryPrepareDocument(requireItems: false, requirePackages: false)) return false;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPackingListService>();
            var sameNumber = await service.GetPackingListByNumberAsync(PackingList.PackingListNumber);
            if (sameNumber is not null && sameNumber.Id != PackingList.Id) { Status = "Packing List number already exists."; return false; }
            if (IsNew) { await service.CreatePackingListAsync(PackingList); IsNew = false; }
            else await service.UpdatePackingListAsync(PackingList);
            Status = "Saved.";
            return true;
        }
        catch (Exception ex) { Status = $"Save failed: {ex.Message}"; return false; }
    }

    public async Task<bool> DeleteAsync()
    {
        if (IsNew || PackingList.Id == 0) return true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPackingListService>();
            await service.DeletePackingListAsync(PackingList.Id);
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex) { Status = $"Delete failed: {ex.Message}"; return false; }
    }

    public void SetStatusMessage(string message) => Status = message;

    private bool TryPrepareDocument(bool requireItems, bool requirePackages)
    {
        if (string.IsNullOrWhiteSpace(PackingList.PackingListNumber)) { Status = "Packing List number is required."; return false; }
        if (SelectedCustomer is null) { Status = "Customer is required."; return false; }
        if (SelectedUser is null) { Status = "User is required."; return false; }
        if (PackingDate is null) { Status = "Packing date is required."; return false; }
        if (requireItems && Items.Count == 0) { Status = "Add at least one item before exporting PDF."; return false; }
        if (requirePackages && Packages.Count == 0) { Status = "Add at least one package/carton row before exporting PDF."; return false; }

        foreach (var item in Items)
            if (!item.TryValidate(out var error)) { SelectedItem = item; Status = error; return false; }
        foreach (var package in Packages)
            if (!package.TryValidate(out var error)) { SelectedPackage = package; Status = error; return false; }

        var customerChanged = PackingList.CustomerId != 0 && PackingList.CustomerId != SelectedCustomer.Id;
        var userChanged = PackingList.UserId != 0 && PackingList.UserId != SelectedUser.Id;

        PackingList.PackingListNumber = PackingList.PackingListNumber.Trim();
        PackingList.PackingDate = PackingDate.Value.DateTime;
        PackingList.CustomerPoDate = CustomerPoDate?.DateTime;

        if (IsNew || customerChanged || string.IsNullOrWhiteSpace(PackingList.CustomerNameSnapshot))
        {
            PackingList.CustomerNameSnapshot = SelectedCustomer.Name.Trim();
            PackingList.CustomerAddressSnapshot = BuildCustomerAddress(SelectedCustomer);
            PackingList.CustomerContactSnapshot = SelectedCustomerContact is null ? null : BuildContactName(SelectedCustomerContact);
        }
        else if (SelectedCustomerContact is not null)
            PackingList.CustomerContactSnapshot = BuildContactName(SelectedCustomerContact);

        if (IsNew || userChanged || string.IsNullOrWhiteSpace(PackingList.SalesContactNameSnapshot))
        {
            PackingList.SalesContactNameSnapshot = SelectedUser.Name.Trim();
            PackingList.SalesContactPhoneSnapshot = SelectedUser.Phone;
            PackingList.SalesContactEmailSnapshot = SelectedUser.Email;
        }

        PackingList.CustomerId = SelectedCustomer.Id;
        PackingList.UserId = SelectedUser.Id;
        PackingList.Customer = null;
        PackingList.User = null;
        PackingList.Items = Items.Select((item, index) => item.ToEntity(index)).ToList();
        PackingList.Packages = Packages.Select((package, index) => package.ToEntity(index)).ToList();
        return true;
    }

    private void RefreshCustomerContacts()
    {
        CustomerContacts.Clear(); SelectedCustomerContact = null;
        if (SelectedCustomer is null) return;
        foreach (var c in SelectedCustomer.Contacts.OrderBy(c => c.Name)) CustomerContacts.Add(c);
        if (SelectedCustomer.Id == PackingList.CustomerId && !string.IsNullOrWhiteSpace(PackingList.CustomerContactSnapshot))
            SelectedCustomerContact = CustomerContacts.FirstOrDefault(c => string.Equals(BuildContactName(c), PackingList.CustomerContactSnapshot, StringComparison.OrdinalIgnoreCase));
        if (IsNew && SelectedCustomerContact is null && CustomerContacts.Count == 1) SelectedCustomerContact = CustomerContacts[0];
    }

    private void AddItemRow(PackingListItemRowViewModel row) { row.PropertyChanged += Row_PropertyChanged; Items.Add(row); NotifyTotals(); }
    private void AddPackageRow(PackingPackageRowViewModel row) { row.PropertyChanged += Row_PropertyChanged; Packages.Add(row); NotifyTotals(); }
    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e) => NotifyTotals();
    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalQuantity)); OnPropertyChanged(nameof(TotalCartons)); OnPropertyChanged(nameof(TotalNetWeight));
        OnPropertyChanged(nameof(TotalGrossWeight)); OnPropertyChanged(nameof(TotalCbm)); OnPropertyChanged(nameof(TotalsText));
    }

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
    private static string BuildContactName(CustomerContact contact) => string.IsNullOrWhiteSpace(contact.Title) ? contact.Name.Trim() : $"{contact.Title.Trim()} {contact.Name.Trim()}";

    private static PackingList Clone(PackingList source) => new()
    {
        Id = source.Id,
        PackingListNumber = source.PackingListNumber,
        PackingDate = source.PackingDate,
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
        SourceDocumentType = source.SourceDocumentType,
        SourceDocumentId = source.SourceDocumentId,
        SourceDocumentNumber = source.SourceDocumentNumber,
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt,
        Items = source.Items.Select(i => new PackingListItem
        {
            Id = i.Id, SortOrder = i.SortOrder, SourceArticleId = i.SourceArticleId, ArticleName = i.ArticleName,
            Description = i.Description, Specification = i.Specification, Quantity = i.Quantity, Unit = i.Unit
        }).ToList(),
        Packages = source.Packages.Select(p => new PackingPackage
        {
            Id = p.Id, SortOrder = p.SortOrder, CartonNumber = p.CartonNumber, PackageCount = p.PackageCount,
            Contents = p.Contents, LengthCm = p.LengthCm, WidthCm = p.WidthCm, HeightCm = p.HeightCm,
            NetWeightKg = p.NetWeightKg, GrossWeightKg = p.GrossWeightKg
        }).ToList()
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
