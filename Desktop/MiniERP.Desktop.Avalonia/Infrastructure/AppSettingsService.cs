using System.Text.Json;

namespace MiniERP.Desktop.Infrastructure;

public sealed class AppSettings
{
    public decimal? CnyPerUsd { get; set; }
    // Calendar days from the document date. Older settings files default to 30.
    public int DefaultValidityDays { get; set; } = 30;
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

    public Task SaveExchangeRateAsync(decimal cnyPerUsd)
        => SaveSettingsAsync(cnyPerUsd, Current.DefaultValidityDays);

    public async Task SaveSettingsAsync(decimal? cnyPerUsd, int defaultValidityDays)
    {
        if (cnyPerUsd is <= 0)
            throw new ArgumentOutOfRangeException(nameof(cnyPerUsd), "Exchange rate must be greater than zero.");
        if (defaultValidityDays is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(defaultValidityDays),
                "Default validity must be between 1 and 3650 calendar days.");

        await _saveLock.WaitAsync();
        try
        {
            var updated = new AppSettings
            {
                CnyPerUsd = cnyPerUsd,
                DefaultValidityDays = defaultValidityDays
            };

            var json = JsonSerializer.Serialize(updated, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = _settingsPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, true);
            Current = updated;
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
