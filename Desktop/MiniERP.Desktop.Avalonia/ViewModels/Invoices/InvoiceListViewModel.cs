using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Invoices;

public sealed class InvoiceListViewModel : INotifyPropertyChanged
{
    private readonly InvoiceType _type;
    private readonly List<Invoice> _allInvoices = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private Invoice? _selectedInvoice;
    private string _status = string.Empty;

    public ObservableCollection<Invoice> Invoices { get; } = new();
    public string DocumentTitle => _type == InvoiceType.Proforma ? "Proforma Invoice" : "Commercial Invoice";

    public Invoice? SelectedInvoice
    {
        get => _selectedInvoice;
        set { if (ReferenceEquals(_selectedInvoice, value)) return; _selectedInvoice = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        private set { if (_status == value) return; _status = value; OnPropertyChanged(); }
    }

    public InvoiceListViewModel(InvoiceType type) => _type = type;

    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var rows = await service.GetInvoicesByTypeAsync(_type);
            _allInvoices.Clear();
            _allInvoices.AddRange(rows);
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
        if (SelectedInvoice is null) { Status = $"Please select a {DocumentTitle.ToLowerInvariant()} first."; return; }
        try
        {
            var id = SelectedInvoice.Id;
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            await service.DeleteInvoiceAsync(id);
            await LoadAsync();
            Status = $"{DocumentTitle} deleted.";
        }
        catch (Exception ex) { Status = $"Delete failed: {ex.Message}"; }
    }

    private void ApplyFilters()
    {
        IEnumerable<Invoice> filtered = _allInvoices;
        foreach (var pair in _filters)
        {
            filtered = filtered.Where(i => pair.Key switch
            {
                "Number" => Matches(i.InvoiceNumber, pair.Value),
                "Customer" => Matches(i.CustomerNameSnapshot ?? i.Customer?.Name, pair.Value),
                "User" => Matches(i.SalesContactNameSnapshot ?? i.User?.Name, pair.Value),
                "Date" => Matches(i.InvoiceDate.ToString("yyyy-MM-dd"), pair.Value),
                "PO" => Matches(i.CustomerPoNumber, pair.Value),
                "Currency" => Matches(i.Currency, pair.Value),
                "Source" => Matches(i.SourceDocumentNumber, pair.Value),
                _ => true
            });
        }

        Invoices.Clear();
        foreach (var invoice in filtered) Invoices.Add(invoice);
        SelectedInvoice = null;
        Status = _filters.Count == 0 ? $"{Invoices.Count} document(s)" : $"{Invoices.Count} of {_allInvoices.Count} document(s)";
    }

    private static bool Matches(string? value, string filter)
        => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
