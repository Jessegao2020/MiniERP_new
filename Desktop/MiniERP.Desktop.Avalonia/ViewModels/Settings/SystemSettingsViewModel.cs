using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop.ViewModels.Settings;

public sealed class SystemSettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settings;
    private string _exchangeRateText = string.Empty;
    private string _status = string.Empty;

    public string ExchangeRateText
    {
        get => _exchangeRateText;
        set
        {
            if (_exchangeRateText == value) return;
            _exchangeRateText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewText));
        }
    }

    public string PreviewText
    {
        get
        {
            if (!decimal.TryParse(ExchangeRateText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || rate <= 0)
                return "Enter how many CNY equal 1 USD.";

            return $"Example: 100 CNY = {(100m / rate):0.00} USD";
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public SystemSettingsViewModel(AppSettingsService settings)
    {
        _settings = settings;
        ExchangeRateText = settings.Current.CnyPerUsd?.ToString("0.############################", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public async Task<bool> SaveAsync()
    {
        if (!decimal.TryParse(ExchangeRateText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || rate <= 0)
        {
            Status = "Exchange rate must be a number greater than zero.";
            return false;
        }

        try
        {
            await _settings.SaveExchangeRateAsync(rate);
            Status = "Settings saved.";
            OnPropertyChanged(nameof(PreviewText));
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
            return false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
