using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.PackingLists;

public sealed class PackingListListViewModel : INotifyPropertyChanged
{
    private readonly List<PackingList> _allPackingLists = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private PackingList? _selectedPackingList;
    private string _status = string.Empty;

    public ObservableCollection<PackingList> PackingLists { get; } = new();
    public PackingList? SelectedPackingList
    {
        get => _selectedPackingList;
        set { if (ReferenceEquals(_selectedPackingList, value)) return; _selectedPackingList = value; OnPropertyChanged(); }
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
            var service = scope.ServiceProvider.GetRequiredService<IPackingListService>();
            var rows = await service.GetAllPackingListsAsync();
            _allPackingLists.Clear();
            _allPackingLists.AddRange(rows);
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
        if (SelectedPackingList is null) { Status = "Please select a packing list first."; return; }
        try
        {
            var id = SelectedPackingList.Id;
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPackingListService>();
            await service.DeletePackingListAsync(id);
            await LoadAsync();
            Status = "Packing List deleted.";
        }
        catch (Exception ex) { Status = $"Delete failed: {ex.Message}"; }
    }

    private void ApplyFilters()
    {
        IEnumerable<PackingList> filtered = _allPackingLists;
        foreach (var pair in _filters)
        {
            filtered = filtered.Where(p => pair.Key switch
            {
                "Number" => Matches(p.PackingListNumber, pair.Value),
                "Customer" => Matches(p.CustomerNameSnapshot ?? p.Customer?.Name, pair.Value),
                "User" => Matches(p.SalesContactNameSnapshot ?? p.User?.Name, pair.Value),
                "Date" => Matches(p.PackingDate.ToString("yyyy-MM-dd"), pair.Value),
                "PO" => Matches(p.CustomerPoNumber, pair.Value),
                "Source" => Matches(p.SourceDocumentNumber, pair.Value),
                _ => true
            });
        }

        PackingLists.Clear();
        foreach (var row in filtered) PackingLists.Add(row);
        SelectedPackingList = null;
        Status = _filters.Count == 0 ? $"{PackingLists.Count} packing list(s)" : $"{PackingLists.Count} of {_allPackingLists.Count} packing list(s)";
    }

    private static bool Matches(string? value, string filter) => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
