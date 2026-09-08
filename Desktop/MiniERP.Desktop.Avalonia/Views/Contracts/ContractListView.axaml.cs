using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Contracts;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Contracts;

public partial class ContractListView : UserControl
{
    private ContractListViewModel ViewModel => (ContractListViewModel)DataContext!;

    public event Action<Contract?>? OpenContractRequested;

    public ContractListView()
    {
        InitializeComponent();
        DataContext = new ContractListViewModel();
        AttachedToVisualTree += async (_, _) => await ViewModel.LoadAsync();
    }

    public Task ReloadAsync() => ViewModel.LoadAsync();

    private void New_Click(object? sender, RoutedEventArgs e) => OpenContractRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedContract is null) { await ViewModel.DeleteSelectedAsync(); return; }
        var confirmed = await ConfirmationDialog.ShowAsync(this, "Delete Contract", $"Delete '{ViewModel.SelectedContract.ContractNumber}'? This cannot be undone.");
        if (confirmed) await ViewModel.DeleteSelectedAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is string field) ViewModel.SetFilter(field, textBox.Text);
    }

    private void ContractGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedContract is not null) OpenContractRequested?.Invoke(ViewModel.SelectedContract);
    }
}
