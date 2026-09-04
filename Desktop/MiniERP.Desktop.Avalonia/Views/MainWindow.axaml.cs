using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace MiniERP.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, TabItem> _openTabs = new();
    private TabControl? _contentTabControl;

    private TabControl ContentTabControl =>
        _contentTabControl ??= this.FindControl<TabControl>("ContentTabControl")
            ?? throw new InvalidOperationException("ContentTabControl was not found.");

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Article_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("article", "Article", "Article list will be migrated here next.");

    private void Customer_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("customer", "Customer", "Customer list and details will be migrated after Article.");

    private void Quotation_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("quotation", "Quotation", "Quotation module is not migrated yet.");

    private void Order_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("order", "Order", "Order module is not migrated yet.");

    private void Invoice_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("invoice", "Invoice", "Invoice module is not migrated yet.");

    private void PackingList_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("packing-list", "P/L", "Packing List module is not migrated yet.");

    private void ProformaInvoice_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("proforma-invoice", "P/I", "Proforma Invoice module is not migrated yet.");

    private void Contract_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("contract", "Contract", "Contract module is not migrated yet.");

    private void System_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("system", "System", "System settings are not migrated yet.");

    private void User_Click(object? sender, RoutedEventArgs e)
        => OpenWorkspace("user", "User", "User settings are not migrated yet.");

    private void OpenWorkspace(string key, string title, string description)
    {
        if (_openTabs.TryGetValue(key, out var existing))
        {
            ContentTabControl.SelectedItem = existing;
            return;
        }

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
            Content = CreatePlaceholder(title, description)
        };

        closeButton.Click += (_, _) => CloseWorkspace(key, tab);

        _openTabs[key] = tab;
        ContentTabControl.Items.Add(tab);
        ContentTabControl.SelectedItem = tab;
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
        ContentTabControl.Items.Remove(tab);
        _openTabs.Remove(key);
    }
}
