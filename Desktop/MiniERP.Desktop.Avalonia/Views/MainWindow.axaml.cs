using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MiniERP.Desktop.Views.Articles;
using MiniERP.Desktop.Views.Settings;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, TabItem> _openTabs = new();
    private readonly ObservableCollection<TabItem> _workspaceTabs = new();

    public MainWindow()
    {
        InitializeComponent();
        ContentTabControl.ItemsSource = _workspaceTabs;
    }

    private void Article_Click(object? sender, RoutedEventArgs e)
        => OpenArticleList();

    private void Customer_Click(object? sender, RoutedEventArgs e)
        => OpenPlaceholder("customer", "Customer", "Customer list and details will be migrated after Article.");

    private void Quotation_Click(object? sender, RoutedEventArgs e)
        => OpenPlaceholder("quotation", "Quotation", "Quotation module is not migrated yet.");

    private void Order_Click(object? sender, RoutedEventArgs e)
        => OpenPlaceholder("order", "Order", "Order module is not migrated yet.");

    private void Invoice_Click(object? sender, RoutedEventArgs e)
        => OpenPlaceholder("invoice", "Invoice", "Invoice module is not migrated yet.");

    private void PackingList_Click(object? sender, RoutedEventArgs e)
        => OpenPlaceholder("packing-list", "P/L", "Packing List module is not migrated yet.");

    private void ProformaInvoice_Click(object? sender, RoutedEventArgs e)
        => OpenPlaceholder("proforma-invoice", "P/I", "Proforma Invoice module is not migrated yet.");

    private void Contract_Click(object? sender, RoutedEventArgs e)
        => OpenPlaceholder("contract", "Contract", "Contract module is not migrated yet.");

    private void System_Click(object? sender, RoutedEventArgs e)
        => OpenSystemSettings();

    private void User_Click(object? sender, RoutedEventArgs e)
        => OpenPlaceholder("user", "User", "User settings are not migrated yet.");

    private void OpenArticleList()
    {
        const string key = "article";

        if (SelectExisting(key))
            return;

        var view = new ArticleListView();
        view.OpenArticleRequested += OpenArticleEditor;
        AddWorkspace(key, "Article", view);
    }

    private void OpenArticleEditor(Article? article)
    {
        var key = article is null ? "article:new" : $"article:{article.Id}";
        var title = article is null ? "New Article" : "Article Details";

        if (SelectExisting(key))
            return;

        var editor = new ArticleEditorView(article);
        var tab = AddWorkspace(key, title, editor);

        editor.Saved += async (_, _) => await RefreshArticleListAsync();
        editor.Deleted += async (_, _) => await RefreshArticleListAsync();
        editor.RequestClose += (_, _) => CloseWorkspace(key, tab);
    }

    private void OpenSystemSettings()
    {
        const string key = "system";

        if (SelectExisting(key))
            return;

        var view = new SystemSettingsView();
        view.Saved += (_, _) => RefreshOpenArticleExchangeRates();
        AddWorkspace(key, "System", view);
    }

    private void RefreshOpenArticleExchangeRates()
    {
        foreach (var tab in _workspaceTabs)
        {
            if (tab.Content is ArticleEditorView editor)
                editor.RefreshExchangeRate();
        }
    }

    private async Task RefreshArticleListAsync()
    {
        if (_openTabs.TryGetValue("article", out var tab) && tab.Content is ArticleListView list)
            await list.ReloadAsync();
    }

    private void OpenPlaceholder(string key, string title, string description)
    {
        if (SelectExisting(key))
            return;

        AddWorkspace(key, title, CreatePlaceholder(title, description));
    }

    private bool SelectExisting(string key)
    {
        if (!_openTabs.TryGetValue(key, out var existing))
            return false;

        ContentTabControl.SelectedItem = existing;
        return true;
    }

    private TabItem AddWorkspace(string key, string title, Control content)
    {
        var closeButton = new Button
        {
            Content = "×",
            Margin = new Thickness(5, 0, 0, 0),
            Padding = new Thickness(3, 0),
            MinWidth = 18,
            MinHeight = 18,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 11,
            FontWeight = FontWeight.Bold
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        header.Children.Add(new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(closeButton);

        var tab = new TabItem
        {
            Header = header,
            Content = content
        };

        closeButton.Click += (_, _) => CloseWorkspace(key, tab);

        _openTabs[key] = tab;
        _workspaceTabs.Add(tab);
        ContentTabControl.SelectedItem = tab;

        return tab;
    }

    private static Control CreatePlaceholder(string title, string description)
    {
        var stack = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 24,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        stack.Children.Add(new TextBlock
        {
            Text = description,
            Opacity = 0.55,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return stack;
    }

    private void CloseWorkspace(string key, TabItem tab)
    {
        _workspaceTabs.Remove(tab);
        _openTabs.Remove(key);
    }
}
