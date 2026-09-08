using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.Desktop.Controls;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Settings;

namespace MiniERP.Desktop.Views.Settings;

public partial class SystemSettingsView : UserControl
{
    private readonly DecimalTextBoxFilter _rateFilter;
    private SystemSettingsViewModel ViewModel => (SystemSettingsViewModel)DataContext!;

    public event EventHandler? Saved;

    public SystemSettingsView()
    {
        InitializeComponent();

        var settings = App.Services.GetRequiredService<AppSettingsService>();
        DataContext = new SystemSettingsViewModel(settings);
        _rateFilter = new DecimalTextBoxFilter(ExchangeRateTextBox);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.SaveAsync())
            return;

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
