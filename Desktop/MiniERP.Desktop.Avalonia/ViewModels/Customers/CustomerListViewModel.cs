using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Customers;

public sealed class CustomerListViewModel : INotifyPropertyChanged
{
    private readonly List<Customer> _allCustomers = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private Customer? _selectedCustomer;
    private string _status = string.Empty;

    public ObservableCollection<Customer> Customers { get; } = new();

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

    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var rows = await service.GetAllCustomersAsync();

            _allCustomers.Clear();
            _allCustomers.AddRange(rows);
            ApplyFilters();
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    public void SetFilter(string field, string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(normalized))
            _filters.Remove(field);
        else
            _filters[field] = normalized;

        ApplyFilters();
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedCustomer is null)
        {
            Status = "Please select a customer first.";
            return;
        }

        try
        {
            var id = SelectedCustomer.Id;

            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            await service.DeleteCustomerAsync(id);

            await LoadAsync();
            Status = $"Customer {id} deleted.";
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<Customer> filtered = _allCustomers;

        foreach (var pair in _filters)
        {
            var field = pair.Key;
            var filter = pair.Value;

            filtered = filtered.Where(customer => field switch
            {
                "Name" => Matches(customer.Name, filter),
                "AddressLine1" => Matches(customer.AddressLine1, filter),
                "City" => Matches(customer.City, filter),
                "State" => Matches(customer.State, filter),
                "PostalCode" => Matches(customer.PostalCode, filter),
                "Country" => Matches(customer.Country, filter),
                _ => true
            });
        }

        Customers.Clear();
        foreach (var customer in filtered)
            Customers.Add(customer);

        SelectedCustomer = null;
        Status = _filters.Count == 0
            ? $"{Customers.Count} customer(s)"
            : $"{Customers.Count} of {_allCustomers.Count} customer(s)";
    }

    private static bool Matches(string? value, string filter)
        => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
