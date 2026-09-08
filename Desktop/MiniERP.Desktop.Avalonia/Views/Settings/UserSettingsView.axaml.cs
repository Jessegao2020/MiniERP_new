using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Settings;

namespace MiniERP.Desktop.Views.Settings;

public partial class UserSettingsView : UserControl
{
    private UserSettingsViewModel ViewModel => (UserSettingsViewModel)DataContext!;

    public UserSettingsView()
    {
        InitializeComponent();
        DataContext = new UserSettingsViewModel();
        AttachedToVisualTree += async (_, _) => await ViewModel.LoadAsync();
    }

    private void New_Click(object? sender, RoutedEventArgs e)
        => ViewModel.NewUser();

    private async void Save_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.SaveAsync();

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsExistingUser)
        {
            await ViewModel.DeleteAsync();
            return;
        }

        var confirmed = await ConfirmationDialog.ShowAsync(
            this,
            "Delete User",
            "Delete this user? Quotations assigned to this user may prevent deletion.");

        if (confirmed)
            await ViewModel.DeleteAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.LoadAsync();
}
