using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Desktop.ViewModels.Customers;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Customers;

public partial class CustomerListView : UserControl
{
    private CustomerListViewModel ViewModel => (CustomerListViewModel)DataContext!;

    public event Action<Customer?>? OpenCustomerRequested;

    public CustomerListView()
    {
        InitializeComponent();
        DataContext = new CustomerListViewModel();
        AttachedToVisualTree += async (_, _) => await ViewModel.LoadAsync();
    }

    public Task ReloadAsync() => ViewModel.LoadAsync();

    private void New_Click(object? sender, RoutedEventArgs e)
        => OpenCustomerRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.DeleteSelectedAsync();

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.LoadAsync();

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string field)
            return;

        ViewModel.SetFilter(field, textBox.Text);
    }

    private void CustomerGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedCustomer is not null)
            OpenCustomerRequested?.Invoke(ViewModel.SelectedCustomer);
    }
}
