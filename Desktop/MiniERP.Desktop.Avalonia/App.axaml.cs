using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.Desktop.Infrastructure;
using MiniERP.ApplicationLayer.Services;
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
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            ConfigureDatabaseAndServices();
            ThemeManager.Apply(Services.GetRequiredService<AppSettingsService>().Current.ThemeMode);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
        }
        catch (Exception error)
        {
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
        DatabasePath = AppDataPaths.GetDatabasePath();
        CatalogDatabasePath = AppDataPaths.GetCatalogDatabasePath();

        var services = new ServiceCollection();
        services.AddInfrastructure($"Data Source={DatabasePath}", $"Data Source={CatalogDatabasePath}");
        services.AddApplication();
        services.AddSingleton(new AppSettingsService(AppDataPaths.GetSettingsPath()));
        services.AddScoped<QuotationPolicy>(sp => sp.GetRequiredService<AppSettingsService>().GetQuotationPolicy());

        Services = services.BuildServiceProvider();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        DatabaseStartupMigrator.Upgrade(db, catalog, DatabasePath, CatalogDatabasePath);
    }
}
