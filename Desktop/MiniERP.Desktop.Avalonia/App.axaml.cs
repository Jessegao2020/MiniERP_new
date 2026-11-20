using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.Views;
using MiniERP.Infrastructure;
using MiniERP.Infrastructure.Data;

namespace MiniERP.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static string DatabasePath { get; private set; } = string.Empty;
    public static string CatalogDatabasePath { get; private set; } = string.Empty;

    public override void Initialize()
    {
        StartupTiming.Mark("Avalonia App.Initialize entered");
        StartupTiming.Measure("Load App.axaml and application styles",
            () => AvaloniaXamlLoader.Load(this));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        StartupTiming.Mark("OnFrameworkInitializationCompleted entered");
        try
        {
            StartupTiming.Measure("Configure databases and services", ConfigureDatabaseAndServices);
            StartupTiming.Measure("Apply user theme", () =>
                ThemeManager.Apply(Services.GetRequiredService<AppSettingsService>().Current.ThemeMode));

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = StartupTiming.Measure("Construct MainWindow", () => new MainWindow());
                window.Opened += (_, _) =>
                {
                    // Window.Opened is a reliable UI milestone, but not a guarantee
                    // that the compositor has already presented the first pixel.
                    StartupTiming.Mark("MainWindow.Opened event");
                    Dispatcher.UIThread.Post(() =>
                    {
                        StartupTiming.Mark("UI background callback after window opened");
                        StartupTiming.Complete("Main window opened");
                    }, DispatcherPriority.Background);
                };
                desktop.MainWindow = window;
            }
        }
        catch (Exception error)
        {
            StartupTiming.Mark("Startup exception; see startup-error.log");
            StartupTiming.Complete("Failed during framework initialization");
            var logPath = StartupFailureReporter.Write(error);
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                throw;

            // A Windows WinExe has no console. Display the actual cause instead
            // of silently exiting on a failed database migration.
            desktop.MainWindow = new Window
            {
                Title = "MiniERP - Startup error",
                Width = 750,
                Height = 350,
                MinWidth = 550,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Margin = new Thickness(24),
                        TextWrapping = TextWrapping.Wrap,
                        Text = "MiniERP could not open its database. Your data has not been " +
                               "intentionally removed. Please keep your ERP and catalog files.\n\n" +
                               error.Message + "\n\nDiagnostic log:\n" + logPath + "\n\n" +
                               "Send this error message and the log to the developer."
                    }
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureDatabaseAndServices()
    {
        StartupTiming.Measure("Resolve database paths", () =>
        {
            DatabasePath = AppDataPaths.GetDatabasePath();
            CatalogDatabasePath = AppDataPaths.GetCatalogDatabasePath();
        });

        var services = new ServiceCollection();
        StartupTiming.Measure("Register DI services and load settings", () =>
        {
            services.AddInfrastructure($"Data Source={DatabasePath}", $"Data Source={CatalogDatabasePath}");
            services.AddApplication();
            services.AddSingleton(new AppSettingsService(AppDataPaths.GetSettingsPath()));
        });

        Services = StartupTiming.Measure("Build DI service provider",
            () => services.BuildServiceProvider());
        using var scope = Services.CreateScope();
        var db = StartupTiming.Measure("Resolve ERP DbContext",
            () => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var catalog = StartupTiming.Measure("Resolve catalog DbContext",
            () => scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
        StartupTiming.Measure("Database upgrade and verification (total)", () =>
            DatabaseStartupMigrator.Upgrade(db, catalog, DatabasePath, CatalogDatabasePath,
                (step, elapsed) => StartupTiming.RecordDuration("Database / " + step, elapsed)));
    }
}
