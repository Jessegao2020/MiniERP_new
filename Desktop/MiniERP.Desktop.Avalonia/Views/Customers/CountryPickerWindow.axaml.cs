using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MiniERP.Desktop.Views.Customers;

public sealed record CountryOption(string Code, string Name);

public partial class CountryPickerWindow : Window
{
    private static readonly CountryOption[] AllCountries =
    {
        new("CN", "China"),
        new("GB", "United Kingdom"),
        new("DE", "Germany"),
        new("FR", "France"),
        new("US", "United States"),
        new("JP", "Japan"),
        new("KR", "Korea, Republic of")
    };

    private readonly ObservableCollection<CountryOption> _countries = new();

    public CountryPickerWindow(string? currentCode = null)
    {
        InitializeComponent();
        CountryGrid.ItemsSource = _countries;
        ApplyFilter();

        if (!string.IsNullOrWhiteSpace(currentCode))
        {
            var current = AllCountries.FirstOrDefault(country =>
                country.Code.Equals(currentCode.Trim(), StringComparison.OrdinalIgnoreCase));

            if (current is not null)
                CountryGrid.SelectedItem = current;
        }
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
        => ApplyFilter();

    private void ApplyFilter()
    {
        var filter = FilterTextBox.Text?.Trim() ?? string.Empty;

        _countries.Clear();
        foreach (var country in AllCountries.Where(country =>
                     string.IsNullOrEmpty(filter) ||
                     country.Code.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                     country.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            _countries.Add(country);
        }
    }

    private void CountryGrid_DoubleTapped(object? sender, TappedEventArgs e)
        => CloseSelected();

    private void Ok_Click(object? sender, RoutedEventArgs e)
        => CloseSelected();

    private void Cancel_Click(object? sender, RoutedEventArgs e)
        => Close(null);

    private void CloseSelected()
    {
        if (CountryGrid.SelectedItem is CountryOption selected)
            Close(selected.Code);
    }
}
