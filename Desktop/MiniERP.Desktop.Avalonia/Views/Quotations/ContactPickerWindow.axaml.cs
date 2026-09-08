using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class ContactPickerWindow : Window
{
    private readonly List<CustomerContact> _allContacts;
    private readonly ObservableCollection<CustomerContact> _visibleContacts = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private readonly int? _currentContactId;

    public ContactPickerWindow(IEnumerable<CustomerContact> contacts, int? currentContactId = null)
    {
        InitializeComponent();

        _allContacts = contacts
            .OrderBy(contact => contact.Name)
            .ToList();
        _currentContactId = currentContactId;

        ContactGrid.ItemsSource = _visibleContacts;
        ApplyFilters();
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string field)
            return;

        var value = textBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            _filters.Remove(field);
        else
            _filters[field] = value;

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<CustomerContact> filtered = _allContacts;

        foreach (var filter in _filters)
        {
            filtered = filtered.Where(contact => filter.Key switch
            {
                "Title" => Matches(contact.Title, filter.Value),
                "Name" => Matches(contact.Name, filter.Value),
                _ => true
            });
        }

        _visibleContacts.Clear();
        foreach (var contact in filtered)
            _visibleContacts.Add(contact);

        if (_currentContactId is not null && ContactGrid.SelectedItem is null)
        {
            var current = _visibleContacts.FirstOrDefault(contact => contact.Id == _currentContactId.Value);
            if (current is not null)
                ContactGrid.SelectedItem = current;
        }

        StatusText.Text = _filters.Count == 0
            ? $"{_visibleContacts.Count} contact(s)"
            : $"{_visibleContacts.Count} of {_allContacts.Count} contact(s)";
    }

    private static bool Matches(string? value, string filter)
        => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void ContactGrid_DoubleTapped(object? sender, TappedEventArgs e)
        => CloseSelected();

    private void Ok_Click(object? sender, RoutedEventArgs e)
        => CloseSelected();

    private void Cancel_Click(object? sender, RoutedEventArgs e)
        => Close(null);

    private void CloseSelected()
    {
        if (ContactGrid.SelectedItem is CustomerContact contact)
            Close(contact);
    }
}
