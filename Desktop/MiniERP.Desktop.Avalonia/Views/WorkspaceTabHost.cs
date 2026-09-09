using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace MiniERP.Desktop.Views;

internal sealed class WorkspaceTabDescriptor
{
    public WorkspaceTabDescriptor(string key, string fullTitle, Action close)
    {
        Key = key;
        FullTitle = fullTitle;
        DisplayTitle = CompactTitle(fullTitle);
        Close = close;
    }

    public string Key { get; }
    public string FullTitle { get; }
    public string DisplayTitle { get; }
    public Action Close { get; }

    private static string CompactTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        var separators = new[]
        {
            "Customer Details: ",
            "Quotation: ",
            "Contract: ",
            "Invoice: ",
            "P/I: ",
            "P/L: "
        };

        foreach (var prefix in separators)
        {
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return title[prefix.Length..];
        }

        return title;
    }
}

/// <summary>
/// SAP/SelectLine-style single-row workspace host. It deliberately accepts the
/// existing TabItem collection used by MainWindow so the business navigation,
/// duplicate prevention and editor close events do not need to be rewritten.
/// </summary>
public sealed class WorkspaceTabHost : UserControl
{
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#0078D4"));
    private static readonly IBrush ActiveBackground = new SolidColorBrush(Color.Parse("#F4F8FC"));
    private static readonly IBrush BarBackground = new SolidColorBrush(Color.Parse("#F6F6F6"));
    private static readonly IBrush SeparatorBrush = new SolidColorBrush(Color.Parse("#D5D5D5"));

    private readonly StackPanel _tabPanel;
    private readonly ScrollViewer _scrollViewer;
    private readonly ContentControl _contentHost;
    private readonly Button _leftButton;
    private readonly Button _rightButton;
    private readonly Button _windowsButton;
    private readonly Dictionary<TabItem, Border> _visuals = new();

    private IEnumerable? _itemsSource;
    private INotifyCollectionChanged? _observableSource;
    private TabItem? _selectedItem;

    public WorkspaceTabHost()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));

        var bar = new Border
        {
            Height = 38,
            Background = BarBackground,
            BorderBrush = SeparatorBrush,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var barGrid = new Grid();
        barGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(30)));
        barGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(30)));
        barGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        barGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(36)));

        _leftButton = CreateNavButton("‹", "Scroll workspaces left");
        _leftButton.Click += (_, _) => ScrollBy(-220);
        Grid.SetColumn(_leftButton, 0);
        barGrid.Children.Add(_leftButton);

        _rightButton = CreateNavButton("›", "Scroll workspaces right");
        _rightButton.Click += (_, _) => ScrollBy(220);
        Grid.SetColumn(_rightButton, 1);
        barGrid.Children.Add(_rightButton);

        _tabPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _tabPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(_scrollViewer, 2);
        barGrid.Children.Add(_scrollViewer);

        _windowsButton = CreateNavButton("▼", "Open windows");
        _windowsButton.FontSize = 11;
        _windowsButton.Click += (_, _) => ShowOpenWindowsMenu();
        Grid.SetColumn(_windowsButton, 3);
        barGrid.Children.Add(_windowsButton);

        bar.Child = barGrid;
        root.Children.Add(bar);

        _contentHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(_contentHost, 1);
        root.Children.Add(_contentHost);

        Content = root;
        UpdateNavigationState();
    }

    public IEnumerable? ItemsSource
    {
        get => _itemsSource;
        set
        {
            if (ReferenceEquals(_itemsSource, value)) return;
            if (_observableSource is not null)
                _observableSource.CollectionChanged -= ItemsSource_CollectionChanged;

            _itemsSource = value;
            _observableSource = value as INotifyCollectionChanged;
            if (_observableSource is not null)
                _observableSource.CollectionChanged += ItemsSource_CollectionChanged;

            RebuildTabs();
        }
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (value is null)
            {
                SelectTab(null);
                return;
            }

            if (value is TabItem tab)
                SelectTab(tab);
        }
    }

    private static Button CreateNavButton(string text, string toolTip)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            MinWidth = 28,
            MinHeight = 34,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 19,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(button, toolTip);
        return button;
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var previousSelected = _selectedItem;
        var previousIndex = previousSelected is null ? -1 : Tabs().IndexOf(previousSelected);
        RebuildTabs();

        if (previousSelected is not null && Tabs().Contains(previousSelected))
        {
            SelectTab(previousSelected);
            return;
        }

        var tabs = Tabs();
        if (tabs.Count == 0)
        {
            SelectTab(null);
            return;
        }

        var nextIndex = previousIndex < 0 ? tabs.Count - 1 : Math.Min(previousIndex, tabs.Count - 1);
        SelectTab(tabs[nextIndex]);
    }

    private void RebuildTabs()
    {
        _tabPanel.Children.Clear();
        _visuals.Clear();

        foreach (var tab in Tabs())
        {
            var visual = CreateTabVisual(tab);
            _visuals[tab] = visual;
            _tabPanel.Children.Add(visual);
        }

        UpdateSelectedAppearance();
        UpdateNavigationState();
    }

    private Border CreateTabVisual(TabItem tab)
    {
        var descriptor = Descriptor(tab);
        var titleText = new TextBlock
        {
            Text = descriptor?.DisplayTitle ?? tab.Header?.ToString() ?? string.Empty,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        var selectButton = new Button
        {
            Content = titleText,
            Padding = new Thickness(10, 6, 5, 6),
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MinHeight = 35,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(selectButton, descriptor?.FullTitle ?? titleText.Text);
        selectButton.Click += (_, _) => SelectTab(tab);

        var closeButton = new Button
        {
            Content = "×",
            Padding = new Thickness(3, 0),
            Margin = new Thickness(0, 0, 3, 0),
            MinWidth = 20,
            MinHeight = 30,
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(closeButton, "Close");
        closeButton.Click += (_, _) => descriptor?.Close();

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        panel.Children.Add(selectButton);
        panel.Children.Add(closeButton);

        var border = new Border
        {
            Child = panel,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 2),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        border.ContextMenu = BuildTabContextMenu(tab);
        return border;
    }

    private ContextMenu BuildTabContextMenu(TabItem tab)
    {
        var menu = new ContextMenu();

        var close = new MenuItem { Header = "Close" };
        close.Click += (_, _) => Descriptor(tab)?.Close();
        menu.Items.Add(close);

        var closeOthers = new MenuItem { Header = "Close Others" };
        closeOthers.Click += (_, _) =>
        {
            foreach (var other in Tabs().Where(candidate => !ReferenceEquals(candidate, tab)).ToList())
                Descriptor(other)?.Close();
            SelectTab(tab);
        };
        menu.Items.Add(closeOthers);

        var closeAll = new MenuItem { Header = "Close All" };
        closeAll.Click += (_, _) =>
        {
            foreach (var item in Tabs().ToList())
                Descriptor(item)?.Close();
        };
        menu.Items.Add(closeAll);

        return menu;
    }

    private void SelectTab(TabItem? tab)
    {
        _selectedItem = tab;
        _contentHost.Content = tab?.Content;
        UpdateSelectedAppearance();

        if (tab is not null && _visuals.TryGetValue(tab, out var visual))
            visual.BringIntoView();
    }

    private void UpdateSelectedAppearance()
    {
        foreach (var pair in _visuals)
        {
            var active = ReferenceEquals(pair.Key, _selectedItem);
            pair.Value.BorderBrush = active ? ActiveBrush : Brushes.Transparent;
            pair.Value.Background = active ? ActiveBackground : Brushes.Transparent;
        }
    }

    private void ShowOpenWindowsMenu()
    {
        var menu = new ContextMenu();
        var tabs = Tabs();

        if (tabs.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "No open windows", IsEnabled = false });
        }
        else
        {
            foreach (var tab in tabs)
            {
                var current = tab;
                var descriptor = Descriptor(current);
                var title = descriptor?.FullTitle ?? current.Header?.ToString() ?? "Window";
                var item = new MenuItem
                {
                    Header = ReferenceEquals(current, _selectedItem) ? $"✓  {title}" : title
                };
                item.Click += (_, _) => SelectTab(current);
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());
            var closeAll = new MenuItem { Header = "Close All" };
            closeAll.Click += (_, _) =>
            {
                foreach (var tab in Tabs().ToList())
                    Descriptor(tab)?.Close();
            };
            menu.Items.Add(closeAll);
        }

        _windowsButton.ContextMenu = menu;
        menu.Open(_windowsButton);
    }

    private void ScrollBy(double amount)
    {
        var max = Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width);
        var next = Math.Clamp(_scrollViewer.Offset.X + amount, 0, max);
        _scrollViewer.Offset = new Vector(next, _scrollViewer.Offset.Y);
    }

    private void UpdateNavigationState()
    {
        var count = Tabs().Count;
        _leftButton.IsEnabled = count > 1;
        _rightButton.IsEnabled = count > 1;
        _windowsButton.IsEnabled = count > 0;
    }

    private List<TabItem> Tabs()
        => _itemsSource?.Cast<object>().OfType<TabItem>().ToList() ?? new List<TabItem>();

    private static WorkspaceTabDescriptor? Descriptor(TabItem tab)
        => tab.Header as WorkspaceTabDescriptor;
}
