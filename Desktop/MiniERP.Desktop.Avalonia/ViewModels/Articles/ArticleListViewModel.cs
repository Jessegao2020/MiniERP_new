using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Articles;

public sealed class ArticleListViewModel : INotifyPropertyChanged
{
    private Article? _selectedArticle;
    private string _searchText = string.Empty;
    private string _status = string.Empty;

    public ObservableCollection<Article> Articles { get; } = new();

    public Article? SelectedArticle
    {
        get => _selectedArticle;
        set
        {
            if (ReferenceEquals(_selectedArticle, value)) return;
            _selectedArticle = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
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

    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IArticleService>();

            var rows = string.IsNullOrWhiteSpace(SearchText)
                ? await service.GetAllArticlesAsync()
                : await service.SearchArticlesAsync(SearchText);

            Articles.Clear();
            foreach (var article in rows)
                Articles.Add(article);

            SelectedArticle = null;
            Status = $"{Articles.Count} article(s)";
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedArticle is null)
        {
            Status = "Please select an article first.";
            return;
        }

        try
        {
            var id = SelectedArticle.Id;

            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IArticleService>();
            await service.DeleteArticleAsync(id);

            await LoadAsync();
            Status = $"Article {id} deleted.";
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
