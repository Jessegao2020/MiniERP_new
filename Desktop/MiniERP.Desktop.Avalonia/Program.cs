using Avalonia;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            StartupFailureReporter.Write(error);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { WmClass = "MiniERP" })
            .WithInterFont()
            .LogToTrace();
}
