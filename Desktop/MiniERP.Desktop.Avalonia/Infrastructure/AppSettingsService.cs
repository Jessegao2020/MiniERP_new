using System.Text.Json;

namespace MiniERP.Desktop.Infrastructure;

public sealed class AppSettings
{
    public decimal? CnyPerUsd { get; set; }
}

public sealed class AppSettingsService
{
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public AppSettings Current { get; private set; }

    public AppSettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
        Current = Load(settingsPath);
    }

    public async Task SaveExchangeRateAsync(decimal cnyPerUsd)
    {
        if (cnyPerUsd <= 0)
            throw new ArgumentOutOfRangeException(nameof(cnyPerUsd), "Exchange rate must be greater than zero.");

        await _saveLock.WaitAsync();
        try
        {
            Current.CnyPerUsd = cnyPerUsd;

            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = _settingsPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, true);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
