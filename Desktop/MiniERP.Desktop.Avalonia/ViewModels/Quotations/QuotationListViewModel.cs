using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Quotations;

public sealed class QuotationListViewModel : INotifyPropertyChanged
{
    private readonly List<Quotation> _allQuotations = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private Quotation? _selectedQuotation;
    private string _status = string.Empty;

    public ObservableCollection<Quotation> Quotations { get; } = new();

    public Quotation? SelectedQuotation
    {
        get => _selectedQuotation;
        set
        {
            if (ReferenceEquals(_selectedQuotation, value)) return;
            _selectedQuotation = value;
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
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            var rows = await service.GetAllQuotationsAsync();

            _allQuotations.Clear();
            _allQuotations.AddRange(rows);
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
        if (SelectedQuotation is null)
        {
            Status = "Please select a quotation first.";
            return;
        }

        try
        {
            var id = SelectedQuotation.Id;
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            await service.DeleteQuotationAsync(id);
            await LoadAsync();
            Status = $"Quotation {id} deleted.";
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<Quotation> filtered = _allQuotations;

        foreach (var pair in _filters)
        {
            var field = pair.Key;
            var filter = pair.Value;

            filtered = filtered.Where(q => field switch
            {
                "Number" => Matches(q.QuotationNumber, filter),
                "Customer" => Matches(q.Customer?.Name, filter),
                "User" => Matches(q.User?.Name, filter),
                "Date" => Matches(q.QuotationDate.ToString("yyyy-MM-dd"), filter),
                "ValidUntil" => Matches(q.ValidUntil?.ToString("yyyy-MM-dd"), filter),
                "Currency" => Matches(q.Currency, filter),
                "DeliveryTerm" => Matches(q.DeliveryTerm, filter),
                "LeadTime" => Matches(q.LeadTime, filter),
                "PaymentTerm" => Matches(q.PaymentTerm, filter),
                _ => true
            });
        }

        Quotations.Clear();
        foreach (var quotation in filtered)
            Quotations.Add(quotation);

        SelectedQuotation = null;
        Status = _filters.Count == 0
            ? $"{Quotations.Count} quotation(s)"
            : $"{Quotations.Count} of {_allQuotations.Count} quotation(s)";
    }

    private static bool Matches(string? value, string filter)
        => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
