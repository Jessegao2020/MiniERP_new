using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Customers;

public sealed class CustomerEditorViewModel : INotifyPropertyChanged
{
    private string _status = string.Empty;
    private CustomerContact? _selectedContact;

    public Customer Customer { get; }
    public bool IsNew { get; private set; }
    public ObservableCollection<CustomerContact> Contacts { get; } = new();

    public CustomerContact? SelectedContact
    {
        get => _selectedContact;
        set
        {
            if (ReferenceEquals(_selectedContact, value)) return;
            _selectedContact = value;
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

    public CustomerEditorViewModel(Customer? source)
    {
        IsNew = source is null;
        Customer = source is null
            ? new Customer { Name = string.Empty }
            : CloneCustomer(source);

        foreach (var contact in source?.Contacts ?? Array.Empty<CustomerContact>())
            Contacts.Add(CloneContact(contact));
    }

    public void AddContact()
    {
        var contact = new CustomerContact
        {
            Name = string.Empty,
            Title = string.Empty
        };

        Contacts.Add(contact);
        SelectedContact = contact;
        Status = "New contact added. Enter a name before saving.";
    }

    public void DeleteSelectedContact()
    {
        if (SelectedContact is null)
        {
            Status = "Please select a contact first.";
            return;
        }

        Contacts.Remove(SelectedContact);
        SelectedContact = null;
        Status = "Contact removed from this customer. Save the customer to persist the change.";
    }

    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Customer.Name))
        {
            Status = "Customer name is required.";
            return false;
        }

        var emptyContact = Contacts.FirstOrDefault(contact => string.IsNullOrWhiteSpace(contact.Name));
        if (emptyContact is not null)
        {
            SelectedContact = emptyContact;
            Status = "Every contact needs a name. Complete or remove the blank contact.";
            return false;
        }

        Customer.Name = Customer.Name.Trim();
        Customer.Contacts = Contacts.Select(CloneContact).ToList();

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();

            if (IsNew)
            {
                await service.CreateCustomerAsync(Customer);
                IsNew = false;
            }
            else
            {
                await service.UpdateCustomerAsync(Customer);
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
        if (IsNew || Customer.Id == 0)
            return true;

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            await service.DeleteCustomerAsync(Customer.Id);
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
            return false;
        }
    }

    private static Customer CloneCustomer(Customer source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        AddressLine1 = source.AddressLine1,
        AddressLine2 = source.AddressLine2,
        City = source.City,
        State = source.State,
        PostalCode = source.PostalCode,
        Country = source.Country,
        IsActive = source.IsActive,
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt
    };

    private static CustomerContact CloneContact(CustomerContact source) => new()
    {
        Id = source.Id,
        CustomerId = source.CustomerId,
        Title = source.Title,
        Name = source.Name,
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
