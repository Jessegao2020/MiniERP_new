using System.Globalization;
using Avalonia.Data.Converters;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Bridges the DateTimeOffset? values used by the existing document view models
/// with CalendarDatePicker.SelectedDate, which uses DateTime?.
/// </summary>
public sealed class DateTimeOffsetDateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            DateTimeOffset date => date.DateTime,
            DateTime date => date,
            _ => null
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            DateTime date => new DateTimeOffset(date),
            DateTimeOffset date => date,
            _ => null
        };
}
