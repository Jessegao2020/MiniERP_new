using Avalonia;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Headless bundle check: never starts UI or reads private databases.
        if (args.Length == 1 && args[0] == "--verify-bundle")
        {
            if (!string.IsNullOrEmpty(typeof(Program).Assembly.Location))
                throw new InvalidOperationException("Application is not running from a single-file bundle.");
            return;
        }

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
            .WithInterFont()
            .LogToTrace();
}
