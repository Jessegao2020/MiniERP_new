using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Shared behavior for SelectLine-style module editors: a compact grid-view toggle
/// and snapshot-based dirty tracking. The snapshot approach deliberately observes
/// nested line-item collections as well as header fields without requiring every
/// domain entity to implement INotifyPropertyChanged.
/// </summary>
public static class EditorWorkflowSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    public static Button AddGridViewToggle(UserControl owner, EventHandler<RoutedEventArgs> clickHandler)
    {
        if (owner.Content is not Grid root)
            throw new InvalidOperationException("Editor root must be a Grid.");

        var toolbar = root.Children.OfType<StackPanel>().FirstOrDefault();
        if (toolbar is null)
            throw new InvalidOperationException("Editor toolbar was not found.");

        var saveButton = toolbar.Children.OfType<Button>().FirstOrDefault()
            ?? throw new InvalidOperationException("Editor Save button was not found.");

        var gridButton = new Button
        {
            Width = 28,
            Padding = new Thickness(3, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = new TextBlock
            {
                Text = "▦",
                FontSize = 14,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        };
        ToolTip.SetTip(gridButton, "Grid view");
        gridButton.Click += clickHandler;

        toolbar.Children.Insert(0, gridButton);
        toolbar.Children.Insert(1, new Border
        {
            Width = 1,
            Margin = new Thickness(3),
            Background = Brushes.LightGray
        });

        return saveButton;
    }

    public static string Snapshot(object? state)
        => JsonSerializer.Serialize(state, JsonOptions);
}

public sealed class EditorDirtyMonitor : IDisposable
{
    private readonly Button _saveButton;
    private readonly Func<string> _captureState;
    private readonly DispatcherTimer _timer;
    private string _cleanState = string.Empty;
    private bool _started;

    public EditorDirtyMonitor(Button saveButton, Func<string> captureState)
    {
        _saveButton = saveButton;
        _captureState = captureState;
        _saveButton.IsEnabled = false;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _timer.Tick += Timer_Tick;
    }

    public bool IsDirty { get; private set; }

    public void Start()
    {
        // A details view can be detached/re-attached when another workspace tab is
        // selected. Do not redefine the clean baseline in that case, otherwise an
        // unsaved edit would incorrectly become "clean" merely by switching tabs.
        if (_started) return;

        _cleanState = SafeCapture();
        _started = true;
        IsDirty = false;
        _saveButton.IsEnabled = false;
        _timer.Start();
    }

    public void MarkClean()
    {
        _cleanState = SafeCapture();
        IsDirty = false;
        _saveButton.IsEnabled = false;
    }

    public void Stop() => _timer.Stop();

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_started) return;

        var dirty = !string.Equals(_cleanState, SafeCapture(), StringComparison.Ordinal);
        if (dirty == IsDirty) return;

        IsDirty = dirty;
        _saveButton.IsEnabled = dirty;
    }

    private string SafeCapture()
    {
        try { return _captureState(); }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }
}
