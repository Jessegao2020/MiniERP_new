namespace MiniERP.Desktop.Infrastructure;

internal static class AppDataPaths
{
    public static string GetDatabasePath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(basePath))
            basePath = AppContext.BaseDirectory;

        var dataDirectory = Path.Combine(basePath, "MiniERP");
        Directory.CreateDirectory(dataDirectory);

        return Path.Combine(dataDirectory, "erp.db");
    }
}
