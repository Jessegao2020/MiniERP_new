using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.PackingLists;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.PackingLists;

public partial class PackingListListView : UserControl
{
    private PackingListListViewModel ViewModel => (PackingListListViewModel)DataContext!;
    public event Action<PackingList?>? OpenPackingListRequested;

    public PackingListListView()
    {
        InitializeComponent();
        DataContext = new PackingListListViewModel();
        AttachedToVisualTree += async (_, _) => await ViewModel.LoadAsync();
    }

    public Task ReloadAsync() => ViewModel.LoadAsync();
    private void New_Click(object? sender, RoutedEventArgs e) => OpenPackingListRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPackingList is null) { await ViewModel.DeleteSelectedAsync(); return; }
        var confirmed = await ConfirmationDialog.ShowAsync(this, "Delete Packing List", $"Delete '{ViewModel.SelectedPackingList.PackingListNumber}'? This cannot be undone.");
        if (confirmed) await ViewModel.DeleteSelectedAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) => await ViewModel.LoadAsync();
    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is string field) ViewModel.SetFilter(field, textBox.Text);
    }
    private void PackingListGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedPackingList is not null) OpenPackingListRequested?.Invoke(ViewModel.SelectedPackingList);
    }
}
