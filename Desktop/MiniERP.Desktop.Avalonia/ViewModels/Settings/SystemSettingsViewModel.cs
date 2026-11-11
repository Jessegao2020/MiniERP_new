using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop.ViewModels.Settings;

public sealed class SystemSettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settings;
    private string _exchangeRateText = string.Empty;
    private string _validityDaysText = string.Empty;
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

    public string ValidityDaysText
    {
        get => _validityDaysText;
        set
        {
            if (_validityDaysText == value) return;
            _validityDaysText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ValidityPreviewText));
        }
    }

    public string ValidityPreviewText
        => int.TryParse(ValidityDaysText, NumberStyles.None, CultureInfo.InvariantCulture, out var days)
            && days is >= 1 and <= 3650
            ? $"New quotations will be valid for {days} calendar day(s) from the quotation date."
            : "Enter a whole number of days between 1 and 3650.";

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
        ValidityDaysText = settings.Current.DefaultValidityDays.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<bool> SaveAsync()
    {
        decimal? rate = null;
        if (!string.IsNullOrWhiteSpace(ExchangeRateText))
        {
            if (!decimal.TryParse(ExchangeRateText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedRate) || parsedRate <= 0)
            {
                Status = "Exchange rate must be a number greater than zero, or blank if not configured.";
                return false;
            }
            rate = parsedRate;
        }

        if (!int.TryParse(ValidityDaysText, NumberStyles.None, CultureInfo.InvariantCulture, out var validityDays)
            || validityDays is < 1 or > 3650)
        {
            Status = "Default validity must be a whole number between 1 and 3650 days.";
            return false;
        }

        try
        {
            await _settings.SaveSettingsAsync(rate, validityDays);
            Status = "Settings saved.";
            OnPropertyChanged(nameof(PreviewText));
            OnPropertyChanged(nameof(ValidityPreviewText));
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
