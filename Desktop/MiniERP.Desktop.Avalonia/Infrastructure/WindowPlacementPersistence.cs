using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Saves the main window's last normal bounds and maximized state separately from
/// business settings. Positions are physical screen pixels; sizes are Avalonia DIPs.
/// </summary>
internal sealed class WindowPlacementPersistence
{
    private readonly Window _window;
    private readonly string _path;
    private SavedWindowPlacement? _normalPlacement;
    private readonly bool _restoreMaximized;
    private bool _opened;
    private bool _wasMaximizedBeforeMinimizing;

    private WindowPlacementPersistence(Window window)
    {
        _window = window;
        _path = Path.Combine(AppDataPaths.GetDataDirectory(), "window-placement.json");

        var saved = Read();
        if (saved is not null && IsValid(saved))
        {
            _normalPlacement = FitToScreens(saved);
            _restoreMaximized = saved.Maximized;
            _wasMaximizedBeforeMinimizing = saved.Maximized;
            ApplyNormalPlacement(_normalPlacement);
        }

        _window.Opened += (_, _) => OnOpened();
        _window.Resized += (_, _) => CaptureNormalPlacement();
        _window.PositionChanged += (_, _) => CaptureNormalPlacement();
        _window.PropertyChanged += (_, e) =>
        {
            if (e.Property != Window.WindowStateProperty)
                return;

            if (_window.WindowState == WindowState.Maximized)
                _wasMaximizedBeforeMinimizing = true;
            else if (_window.WindowState == WindowState.Normal)
                _wasMaximizedBeforeMinimizing = false;

            CaptureNormalPlacement();
        };
        _window.Closing += (_, _) => Save();
    }

    public static void Attach(Window window) => _ = new WindowPlacementPersistence(window);

    private void OnOpened()
    {
        // Screens may only be available once the platform window has been opened.
        if (_normalPlacement is not null)
        {
            var adjusted = FitToScreens(_normalPlacement);
            if (adjusted != _normalPlacement)
            {
                _normalPlacement = adjusted;
                ApplyNormalPlacement(adjusted);
            }
        }

        _opened = true;
        if (_restoreMaximized)
            _window.WindowState = WindowState.Maximized;
        else
            CaptureNormalPlacement();
    }

    private void ApplyNormalPlacement(SavedWindowPlacement saved)
    {
        _window.Width = saved.Width;
        _window.Height = saved.Height;
        _window.WindowStartupLocation = WindowStartupLocation.Manual;
        _window.Position = new PixelPoint(saved.X, saved.Y);
    }

    private void CaptureNormalPlacement()
    {
        // Never overwrite the restored bounds with maximized/minimized dimensions.
        if (!_opened || _window.WindowState != WindowState.Normal)
            return;

        var width = _window.Bounds.Width;
        var height = _window.Bounds.Height;
        if (!double.IsFinite(width) || !double.IsFinite(height)
            || width < 300 || height < 200)
            return;

        _normalPlacement = new SavedWindowPlacement
        {
            X = _window.Position.X,
            Y = _window.Position.Y,
            Width = width,
            Height = height
        };
    }

    private void Save()
    {
        CaptureNormalPlacement();
        if (_normalPlacement is null)
            return;

        var isMaximized = _window.WindowState == WindowState.Maximized
            || (_window.WindowState == WindowState.Minimized && _wasMaximizedBeforeMinimizing);
        var saved = _normalPlacement with { Maximized = isMaximized };

        try
        {
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(saved, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            // A settings write failure must not prevent the ERP window from closing.
            Debug.WriteLine("Could not save MiniERP window placement: " + ex);
        }
    }

    private SavedWindowPlacement? Read()
    {
        try
        {
            if (!File.Exists(_path))
                return null;

            return JsonSerializer.Deserialize<SavedWindowPlacement>(File.ReadAllText(_path));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Could not load MiniERP window placement: " + ex);
            return null;
        }
    }

    private static bool IsValid(SavedWindowPlacement state)
        => double.IsFinite(state.Width) && double.IsFinite(state.Height)
           && state.Width is >= 300 and <= 16384
           && state.Height is >= 200 and <= 16384
           && Math.Abs((long)state.X) <= 200000
           && Math.Abs((long)state.Y) <= 200000;

    private SavedWindowPlacement FitToScreens(SavedWindowPlacement saved)
    {
        var screens = _window.Screens.All;
        if (screens.Count == 0)
            return saved;

        var selected = _window.Screens.Primary ?? screens[0];
        foreach (var screen in screens)
        {
            var area = screen.WorkingArea;
            var scaling = screen.Scaling > 0 ? screen.Scaling : 1;
            var right = saved.X + saved.Width * scaling;
            var bottom = saved.Y + saved.Height * scaling;
            var overlapWidth = Math.Min(right, area.Right) - Math.Max(saved.X, area.X);
            var overlapHeight = Math.Min(bottom, area.Bottom) - Math.Max(saved.Y, area.Y);
            if (overlapWidth >= 100 && overlapHeight >= 60)
            {
                selected = screen;
                break;
            }
        }

        var usable = selected.WorkingArea;
        if (usable.Width <= 0 || usable.Height <= 0)
            return saved;

        var scale = selected.Scaling > 0 ? selected.Scaling : 1;
        var width = Math.Min(Math.Max(saved.Width, _window.MinWidth),
            Math.Max(_window.MinWidth, usable.Width / scale));
        var height = Math.Min(Math.Max(saved.Height, _window.MinHeight),
            Math.Max(_window.MinHeight, usable.Height / scale));

        var maxX = Math.Max(usable.X, usable.Right - (int)Math.Ceiling(width * scale));
        var maxY = Math.Max(usable.Y, usable.Bottom - (int)Math.Ceiling(height * scale));

        return saved with
        {
            X = Math.Clamp(saved.X, usable.X, maxX),
            Y = Math.Clamp(saved.Y, usable.Y, maxY),
            Width = width,
            Height = height
        };
    }
}

internal sealed record SavedWindowPlacement
{
    public int X { get; init; }
    public int Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool Maximized { get; init; }
}
