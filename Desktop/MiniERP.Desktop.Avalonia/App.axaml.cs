using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
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
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        DatabasePath = AppDataPaths.GetDatabasePath();
        CatalogDatabasePath = AppDataPaths.GetCatalogDatabasePath();

        // Backup the live ERP database BEFORE applying any migrations.
        // A failed migration/import must not put historical customer or document data at risk.
        CatalogDatabaseBootstrapper.BackupLegacyDatabaseBeforeFirstSplit(DatabasePath, CatalogDatabasePath);

        var services = new ServiceCollection();
        services.AddInfrastructure($"Data Source={DatabasePath}", $"Data Source={CatalogDatabasePath}");
        services.AddApplication();
        services.AddSingleton(new AppSettingsService(AppDataPaths.GetSettingsPath()));

        Services = services.BuildServiceProvider();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();

            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            catalog.Database.Migrate();
            CatalogDatabaseBootstrapper.Initialize(DatabasePath, CatalogDatabasePath);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
