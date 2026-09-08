using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class ArticlePickerWindow : Window
{
    private readonly List<Article> _allArticles;
    private readonly ObservableCollection<Article> _visibleArticles = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private readonly int? _currentArticleId;

    public ArticlePickerWindow(IEnumerable<Article> articles, int? currentArticleId = null)
    {
        InitializeComponent();

        _allArticles = articles
            .OrderBy(article => article.Name)
            .ToList();
        _currentArticleId = currentArticleId;

        ArticleGrid.ItemsSource = _visibleArticles;
        ApplyFilters();
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string field)
            return;

        var value = textBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            _filters.Remove(field);
        else
            _filters[field] = value;

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<Article> filtered = _allArticles;

        foreach (var filter in _filters)
        {
            filtered = filtered.Where(article => filter.Key switch
            {
                "Name" => Matches(article.Name, filter.Value),
                "Name_EN" => Matches(article.Name_EN, filter.Value),
                "Price" => Matches(FormatPrice(article.Price), filter.Value),
                "Description_EN" => Matches(article.Description_EN, filter.Value),
                "Category" => Matches(article.Category, filter.Value),
                _ => true
            });
        }

        _visibleArticles.Clear();
        foreach (var article in filtered)
            _visibleArticles.Add(article);

        if (_currentArticleId is not null && ArticleGrid.SelectedItem is null)
        {
            var current = _visibleArticles.FirstOrDefault(article => article.Id == _currentArticleId.Value);
            if (current is not null)
                ArticleGrid.SelectedItem = current;
        }

        StatusText.Text = _filters.Count == 0
            ? $"{_visibleArticles.Count} article(s)"
            : $"{_visibleArticles.Count} of {_allArticles.Count} article(s)";
    }

    private static string FormatPrice(decimal? price)
        => price?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool Matches(string? value, string filter)
        => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void ArticleGrid_DoubleTapped(object? sender, TappedEventArgs e)
        => CloseSelected();

    private void Ok_Click(object? sender, RoutedEventArgs e)
        => CloseSelected();

    private void Cancel_Click(object? sender, RoutedEventArgs e)
        => Close(null);

    private void CloseSelected()
    {
        if (ArticleGrid.SelectedItem is Article article)
            Close(article);
    }
}
