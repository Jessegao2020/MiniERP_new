using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Contracts;

public sealed class ContractListViewModel : INotifyPropertyChanged
{
    private readonly List<Contract> _allContracts = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private Contract? _selectedContract;
    private string _status = string.Empty;

    public ObservableCollection<Contract> Contracts { get; } = new();

    public Contract? SelectedContract
    {
        get => _selectedContract;
        set { if (ReferenceEquals(_selectedContract, value)) return; _selectedContract = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        private set { if (_status == value) return; _status = value; OnPropertyChanged(); }
    }

    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContractService>();
            var rows = await service.GetAllContractsAsync();
            _allContracts.Clear();
            _allContracts.AddRange(rows);
            ApplyFilters();
        }
        catch (Exception ex) { Status = $"Load failed: {ex.Message}"; }
    }

    public void SetFilter(string field, string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized)) _filters.Remove(field); else _filters[field] = normalized;
        ApplyFilters();
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedContract is null) { Status = "Please select a contract first."; return; }
        try
        {
            var id = SelectedContract.Id;
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContractService>();
            await service.DeleteContractAsync(id);
            await LoadAsync();
            Status = "Contract deleted.";
        }
        catch (Exception ex) { Status = $"Delete failed: {ex.Message}"; }
    }

    private void ApplyFilters()
    {
        IEnumerable<Contract> filtered = _allContracts;
        foreach (var pair in _filters)
        {
            filtered = filtered.Where(c => pair.Key switch
            {
                "Number" => Matches(c.ContractNumber, pair.Value),
                "Customer" => Matches(c.CustomerNameSnapshot ?? c.Customer?.Name, pair.Value),
                "User" => Matches(c.SalesContactNameSnapshot ?? c.User?.Name, pair.Value),
                "Date" => Matches(c.ContractDate.ToString("yyyy-MM-dd"), pair.Value),
                "PO" => Matches(c.CustomerPoNumber, pair.Value),
                "Currency" => Matches(c.Currency, pair.Value),
                "Source" => Matches(c.SourceDocumentNumber, pair.Value),
                _ => true
            });
        }

        Contracts.Clear();
        foreach (var contract in filtered) Contracts.Add(contract);
        SelectedContract = null;
        Status = _filters.Count == 0 ? $"{Contracts.Count} contract(s)" : $"{Contracts.Count} of {_allContracts.Count} contract(s)";
    }

    private static bool Matches(string? value, string filter)
        => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
