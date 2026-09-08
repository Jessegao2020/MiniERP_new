using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Quotations;

public sealed class QuotationEditorViewModel : INotifyPropertyChanged
{
    private Customer? _selectedCustomer;
    private User? _selectedUser;
    private DateTimeOffset? _quotationDate;
    private DateTimeOffset? _validUntil;
    private string _status = string.Empty;

    public Quotation Quotation { get; }
    public bool IsNew { get; private set; }
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<User> Users { get; } = new();

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (ReferenceEquals(_selectedCustomer, value)) return;
            _selectedCustomer = value;
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

    public QuotationEditorViewModel(Quotation? source)
    {
        IsNew = source is null;
        Quotation = source is null
            ? new Quotation
            {
                QuotationNumber = $"QT-{DateTime.Now:yyyyMMdd-HHmmssfff}",
                QuotationDate = DateTime.Now,
                ValidUntil = DateTime.Today.AddDays(30)
            }
            : Clone(source);

        QuotationDate = new DateTimeOffset(Quotation.QuotationDate);
        ValidUntil = Quotation.ValidUntil is null
            ? null
            : new DateTimeOffset(Quotation.ValidUntil.Value);
    }

    public async Task LoadLookupsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            var customers = await customerService.GetAllCustomersAsync();
            var users = await userService.GetAllUsersAsync();

            Customers.Clear();
            foreach (var customer in customers.Where(customer => customer.IsActive).OrderBy(customer => customer.Name))
                Customers.Add(customer);

            Users.Clear();
            foreach (var user in users.OrderBy(user => user.Name))
                Users.Add(user);

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
            else
                Status = IsNew ? "New quotation." : "Ready.";
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    public async Task<bool> SaveAsync()
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

        Quotation.QuotationNumber = Quotation.QuotationNumber.Trim();
        Quotation.CustomerId = SelectedCustomer.Id;
        Quotation.UserId = SelectedUser.Id;
        Quotation.Customer = null;
        Quotation.User = null;
        Quotation.QuotationDate = QuotationDate.Value.DateTime;
        Quotation.ValidUntil = ValidUntil?.DateTime;

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
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
