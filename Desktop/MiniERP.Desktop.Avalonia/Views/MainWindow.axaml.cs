using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MiniERP.Desktop.ViewModels;

namespace MiniERP.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        // Explicit XAML loading keeps this bootstrap compatible with older .NET 8 SDKs
        // whose Roslyn version cannot load Avalonia 12's source generator.
        AvaloniaXamlLoader.Load(this);

        DataContext = new MainWindowViewModel(App.Services);
        Opened += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }
}
