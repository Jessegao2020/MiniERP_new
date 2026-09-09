using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        var prefixes = new[]
        {
            "Customer Details: ",
            "Quotation: ",
            "Contract: ",
            "Invoice: ",
            "P/I: ",
            "P/L: "
        };

        foreach (var prefix in prefixes)
        {
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return title[prefix.Length..];
        }

        return title;
    }
}

/// <summary>
/// Classic ERP/SelectLine-style single-row MDI workspace tabs. The host keeps
/// MainWindow's existing TabItem collection so business navigation and duplicate
/// prevention remain unchanged while the visual shell is replaced.
/// </summary>
public sealed class WorkspaceTabHost : UserControl
{
    private const double TabWidth = 150d;
    private const double TabHeight = 24d;

    private static readonly IBrush InactiveBackground = new SolidColorBrush(Color.Parse("#F0F0F0"));
    private static readonly IBrush HoverBackground = new SolidColorBrush(Color.Parse("#E4E4E4"));
    private static readonly IBrush ActiveBackground = Brushes.White;
    private static readonly IBrush TabBorderBrush = new SolidColorBrush(Color.Parse("#A8A8A8"));
    private static readonly IBrush ActiveBorderBrush = new SolidColorBrush(Color.Parse("#858585"));
    private static readonly IBrush CloseHoverBackground = new SolidColorBrush(Color.Parse("#D8D8D8"));

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

        // Keep the unused part of the document-tab strip visually identical to the
        // normal window/content background. SelectLine only paints the tabs themselves.
        var bar = new Border
        {
            Height = 26,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };

        var barGrid = new Grid();
        barGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
        barGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
        barGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        barGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(26)));

        _leftButton = CreateNavButton("‹", "Scroll workspaces left");
        _leftButton.Click += (_, _) => ScrollBy(-TabWidth * 2);
        Grid.SetColumn(_leftButton, 0);
        barGrid.Children.Add(_leftButton);

        _rightButton = CreateNavButton("›", "Scroll workspaces right");
        _rightButton.Click += (_, _) => ScrollBy(TabWidth * 2);
        Grid.SetColumn(_rightButton, 1);
        barGrid.Children.Add(_rightButton);

        _tabPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            Margin = new Thickness(1, 1, 0, 0),
            VerticalAlignment = VerticalAlignment.Bottom
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _tabPanel,
            Background = Brushes.Transparent,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalContentAlignment = VerticalAlignment.Bottom
        };
        Grid.SetColumn(_scrollViewer, 2);
        barGrid.Children.Add(_scrollViewer);

        _windowsButton = CreateNavButton("▼", "Open windows");
        _windowsButton.FontSize = 9;
        _windowsButton.Click += (_, _) => ShowOpenWindowsMenu();
        Grid.SetColumn(_windowsButton, 3);
        barGrid.Children.Add(_windowsButton);

        bar.Child = barGrid;
        root.Children.Add(bar);

        _contentHost = new ContentControl
        {
            Background = Brushes.White,
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
            MinWidth = 22,
            MinHeight = 24,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            Opacity = 0.72,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(button, toolTip);
        return button;
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var previousSelected = _selectedItem;
        var previousTabs = Tabs();
        var previousIndex = previousSelected is null ? -1 : previousTabs.IndexOf(previousSelected);
        RebuildTabs();

        var tabs = Tabs();
        if (previousSelected is not null && tabs.Contains(previousSelected))
        {
            SelectTab(previousSelected);
            return;
        }

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
            Text = descriptor.DisplayTitle,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            FontSize = 12
        };

        var titleHost = new Border
        {
            Child = titleText,
            Padding = new Thickness(8, 0, 2, 0),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        ToolTip.SetTip(titleHost, descriptor.FullTitle);

        // The close affordance is intentionally not a Button. Fluent Button hover
        // styling paints a rectangular region; SelectLine-like tabs use a small,
        // subtle circular hover target instead.
        var closeGlyph = new TextBlock
        {
            Text = "×",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var closeHost = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Child = closeGlyph,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(closeHost, "Close");
        closeHost.PointerEntered += (_, _) => closeHost.Background = CloseHoverBackground;
        closeHost.PointerExited += (_, _) => closeHost.Background = Brushes.Transparent;
        closeHost.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(closeHost).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
                return;

            e.Handled = true;
            descriptor.Close();
        };

        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        panel.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
        Grid.SetColumn(titleHost, 0);
        Grid.SetColumn(closeHost, 1);
        panel.Children.Add(titleHost);
        panel.Children.Add(closeHost);

        var border = new Border
        {
            Width = TabWidth,
            Height = TabHeight,
            Child = panel,
            BorderBrush = TabBorderBrush,
            BorderThickness = new Thickness(1),
            Background = InactiveBackground,
            Margin = new Thickness(0, 0, -1, 0),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        ToolTip.SetTip(border, descriptor.FullTitle);
        border.ContextMenu = BuildTabContextMenu(tab);

        // Hover belongs to the whole tab rectangle, not only to the title text area.
        border.PointerEntered += (_, _) =>
        {
            if (!ReferenceEquals(tab, _selectedItem))
                border.Background = HoverBackground;
        };
        border.PointerExited += (_, _) =>
        {
            border.Background = ReferenceEquals(tab, _selectedItem)
                ? ActiveBackground
                : InactiveBackground;
        };
        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
                return;

            SelectTab(tab);
        };

        return border;
    }

    private ContextMenu BuildTabContextMenu(TabItem tab)
    {
        var menu = new ContextMenu();

        var close = new MenuItem { Header = "Close" };
        close.Click += (_, _) => Descriptor(tab).Close();
        menu.Items.Add(close);

        var closeOthers = new MenuItem { Header = "Close Others" };
        closeOthers.Click += (_, _) =>
        {
            foreach (var other in Tabs().Where(candidate => !ReferenceEquals(candidate, tab)).ToList())
                Descriptor(other).Close();
            SelectTab(tab);
        };
        menu.Items.Add(closeOthers);

        var closeAll = new MenuItem { Header = "Close All" };
        closeAll.Click += (_, _) =>
        {
            foreach (var item in Tabs().ToList())
                Descriptor(item).Close();
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
            pair.Value.Background = active ? ActiveBackground : InactiveBackground;
            pair.Value.BorderBrush = active ? ActiveBorderBrush : TabBorderBrush;

            // Selected top tab merges subtly into the white document surface below.
            pair.Value.BorderThickness = active
                ? new Thickness(1, 1, 1, 0)
                : new Thickness(1);
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
                var item = new MenuItem
                {
                    Header = ReferenceEquals(current, _selectedItem)
                        ? $"✓  {descriptor.FullTitle}"
                        : descriptor.FullTitle
                };
                item.Click += (_, _) => SelectTab(current);
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());
            var closeAll = new MenuItem { Header = "Close All" };
            closeAll.Click += (_, _) =>
            {
                foreach (var tab in Tabs().ToList())
                    Descriptor(tab).Close();
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

        // When there are no documents open, leave an unobtrusive strip in the same
        // background color rather than showing a wide grey toolbar.
        _leftButton.IsVisible = count > 0;
        _rightButton.IsVisible = count > 0;
        _windowsButton.IsVisible = count > 0;

        _leftButton.IsEnabled = count > 1;
        _rightButton.IsEnabled = count > 1;
        _windowsButton.IsEnabled = count > 0;
    }

    private List<TabItem> Tabs()
        => _itemsSource?.Cast<object>().OfType<TabItem>().ToList() ?? new List<TabItem>();

    private static WorkspaceTabDescriptor Descriptor(TabItem tab)
    {
        if (tab.Header is WorkspaceTabDescriptor descriptor)
            return descriptor;

        if (tab.Header is StackPanel headerPanel)
        {
            var title = headerPanel.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? "Window";
            var originalClose = headerPanel.Children.OfType<Button>().LastOrDefault();
            return new WorkspaceTabDescriptor(
                string.Empty,
                title,
                () => originalClose?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        }

        var fallbackTitle = tab.Header?.ToString() ?? "Window";
        return new WorkspaceTabDescriptor(string.Empty, fallbackTitle, () => { });
    }
}
