using Avalonia;
using Avalonia.Styling;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// System follows the OS appearance; Light and Dark override it for MiniERP.
/// Theme dictionaries use DynamicResource and react without restarting the app.
/// </summary>
public static class ThemeManager
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";

    public static void Apply(string? mode)
    {
        if (Application.Current is not { } app)
            return;

        app.RequestedThemeVariant = AppSettingsService.NormalizeThemeMode(mode) switch
        {
            Light => ThemeVariant.Light,
            Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
