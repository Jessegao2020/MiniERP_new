using System.Text.Json;
using MiniERP.ApplicationLayer.Services;

namespace MiniERP.Desktop.Infrastructure;

public sealed class AppSettings
{
    public decimal? CnyPerUsd { get; set; }
    // Calendar days from the document date. Older settings files default to 30.
    public int DefaultValidityDays { get; set; } = 30;
    // System follows Windows/Linux appearance, with optional Light/Dark override.
    public string ThemeMode { get; set; } = "System";
    // Optional company defaults; old settings.json files keep legacy behavior.
    public QuotationPolicy? QuotationPolicy { get; set; }
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

    public QuotationPolicy GetQuotationPolicy() => (Current.QuotationPolicy ?? new QuotationPolicy()) with
    {
        DefaultValidityDays = QuotationRules.NormalizeValidityDays(Current.DefaultValidityDays),
        CnyPerUsd = Current.CnyPerUsd is > 0m ? Current.CnyPerUsd : null
    };

    public Task SaveExchangeRateAsync(decimal cnyPerUsd)
        => SaveSettingsAsync(cnyPerUsd, Current.DefaultValidityDays);

    public async Task SaveThemeModeAsync(string themeMode)
    {
        var normalized = NormalizeThemeMode(themeMode);
        await _saveLock.WaitAsync();
        try
        {
            var updated = new AppSettings
            {
                CnyPerUsd = Current.CnyPerUsd,
                DefaultValidityDays = Current.DefaultValidityDays,
                QuotationPolicy = Current.QuotationPolicy,
                ThemeMode = normalized
            };
            await WriteSettingsAsync(updated);
        }
        finally { _saveLock.Release(); }
    }

    public static string NormalizeThemeMode(string? value)
        => value is "Light" or "Dark" ? value : "System";

    private async Task WriteSettingsAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true });

        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = _settingsPath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json);
        File.Move(temporaryPath, _settingsPath, true);
        Current = settings;
    }

    public async Task SaveSettingsAsync(decimal? cnyPerUsd, int defaultValidityDays, string? themeMode = null)
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
                DefaultValidityDays = defaultValidityDays,
                QuotationPolicy = Current.QuotationPolicy,
                ThemeMode = NormalizeThemeMode(themeMode ?? Current.ThemeMode)
            };
            await WriteSettingsAsync(updated);
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
