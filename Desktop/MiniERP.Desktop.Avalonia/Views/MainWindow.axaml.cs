using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniERP.Desktop.ViewModels;

namespace MiniERP.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainWindowViewModel(App.Services);
        Opened += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }
}
