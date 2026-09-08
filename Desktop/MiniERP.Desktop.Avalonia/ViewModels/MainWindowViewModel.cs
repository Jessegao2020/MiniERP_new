using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;

namespace MiniERP.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IServiceProvider _services;

    private int _articleCount;
    private int _customerCount;
    private string _status = "Ready";

    public int ArticleCount
    {
        get => _articleCount;
        private set
        {
            if (_articleCount == value) return;
            _articleCount = value;
            OnPropertyChanged();
        }
    }

    public int CustomerCount
    {
        get => _customerCount;
        private set
        {
            if (_customerCount == value) return;
            _customerCount = value;
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

    public string DatabasePath => App.DatabasePath;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
    }

    public async Task LoadAsync()
    {
        try
        {
            Status = "Loading...";

            using var scope = _services.CreateScope();

            var articleService = scope.ServiceProvider.GetRequiredService<IArticleService>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

            var articles = await articleService.GetAllArticlesAsync();
            var customers = await customerService.GetAllCustomersAsync();

            ArticleCount = articles.Count();
            CustomerCount = customers.Count();
            Status = "Linux/Avalonia bootstrap is running";
        }
        catch (Exception ex)
        {
            Status = $"Startup failed: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
