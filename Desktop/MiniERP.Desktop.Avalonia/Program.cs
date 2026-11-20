using Avalonia;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTiming.Mark("Managed entry point");
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            StartupTiming.Mark("Unhandled startup exception");
            StartupTiming.Complete("Failed before opening a main window");
            StartupFailureReporter.Write(error);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
