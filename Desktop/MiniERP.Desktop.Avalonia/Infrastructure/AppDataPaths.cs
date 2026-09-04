namespace MiniERP.Desktop.Infrastructure;

internal static class AppDataPaths
{
    public static string GetDataDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(basePath))
            basePath = AppContext.BaseDirectory;

        var dataDirectory = Path.Combine(basePath, "MiniERP");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    public static string GetDatabasePath()
        => Path.Combine(GetDataDirectory(), "erp.db");

    public static string GetSettingsPath()
        => Path.Combine(GetDataDirectory(), "settings.json");
}
