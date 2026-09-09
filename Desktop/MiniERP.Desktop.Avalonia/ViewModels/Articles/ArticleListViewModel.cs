using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Articles;

public sealed class ArticleListViewModel : INotifyPropertyChanged
{
    private readonly List<Article> _allArticles = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private Article? _selectedArticle;
    private string _status = string.Empty;
    private string? _sortField;
    private bool _sortAscending = true;

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

    public string? SortField => _sortField;
    public bool SortAscending => _sortAscending;

    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IArticleService>();
            var rows = await service.GetAllArticlesAsync();

            _allArticles.Clear();
            _allArticles.AddRange(rows);
            ApplyFilters();
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    public void SetFilter(string field, string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(normalized))
            _filters.Remove(field);
        else
            _filters[field] = normalized;

        ApplyFilters();
    }

    public void SortBy(string field)
    {
        if (string.Equals(_sortField, field, StringComparison.OrdinalIgnoreCase))
            _sortAscending = !_sortAscending;
        else
        {
            _sortField = field;
            _sortAscending = true;
        }

        ApplyFilters();
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

    private void ApplyFilters()
    {
        IEnumerable<Article> filtered = _allArticles;

        foreach (var pair in _filters)
        {
            var field = pair.Key;
            var filter = pair.Value;

            filtered = filtered.Where(article => field switch
            {
                "Name" => Matches(article.Name, filter),
                "Price" => MatchesDecimal(article.Price, filter),
                "MinimumPrice" => MatchesDecimal(article.MinimumPrice, filter),
                "Description" => Matches(article.Description, filter),
                "Specification" => Matches(article.Specification, filter),
                "Discount" => Matches(article.Discount, filter),
                "Note" => Matches(article.Note, filter),
                _ => true
            });
        }

        filtered = ApplySort(filtered);

        Articles.Clear();
        foreach (var article in filtered)
            Articles.Add(article);

        SelectedArticle = null;
        Status = _filters.Count == 0
            ? $"{Articles.Count} article(s)"
            : $"{Articles.Count} of {_allArticles.Count} article(s)";
    }

    private IEnumerable<Article> ApplySort(IEnumerable<Article> source)
    {
        if (string.IsNullOrWhiteSpace(_sortField))
            return source;

        return (_sortField, _sortAscending) switch
        {
            ("Name", true) => source.OrderBy(article => article.Name, StringComparer.OrdinalIgnoreCase),
            ("Name", false) => source.OrderByDescending(article => article.Name, StringComparer.OrdinalIgnoreCase),
            ("Price", true) => source.OrderBy(article => article.Price),
            ("Price", false) => source.OrderByDescending(article => article.Price),
            ("MinimumPrice", true) => source.OrderBy(article => article.MinimumPrice),
            ("MinimumPrice", false) => source.OrderByDescending(article => article.MinimumPrice),
            ("Description", true) => source.OrderBy(article => article.Description, StringComparer.OrdinalIgnoreCase),
            ("Description", false) => source.OrderByDescending(article => article.Description, StringComparer.OrdinalIgnoreCase),
            ("Specification", true) => source.OrderBy(article => article.Specification, StringComparer.OrdinalIgnoreCase),
            ("Specification", false) => source.OrderByDescending(article => article.Specification, StringComparer.OrdinalIgnoreCase),
            ("Discount", true) => source.OrderBy(article => article.Discount, StringComparer.OrdinalIgnoreCase),
            ("Discount", false) => source.OrderByDescending(article => article.Discount, StringComparer.OrdinalIgnoreCase),
            ("Note", true) => source.OrderBy(article => article.Note, StringComparer.OrdinalIgnoreCase),
            ("Note", false) => source.OrderByDescending(article => article.Note, StringComparer.OrdinalIgnoreCase),
            _ => source
        };
    }

    private static bool Matches(string? value, string filter)
        => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesDecimal(decimal? value, string filter)
    {
        if (value is null)
            return false;

        var invariant = value.Value.ToString(CultureInfo.InvariantCulture);
        var current = value.Value.ToString(CultureInfo.CurrentCulture);

        return invariant.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || current.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
