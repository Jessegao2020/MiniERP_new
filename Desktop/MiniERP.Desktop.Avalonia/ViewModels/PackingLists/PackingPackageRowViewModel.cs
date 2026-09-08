using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.PackingLists;

public sealed class PackingPackageRowViewModel : INotifyPropertyChanged
{
    private string _cartonNumber;
    private string _packageCountText;
    private string? _contents;
    private string _lengthText;
    private string _widthText;
    private string _heightText;
    private string _netWeightText;
    private string _grossWeightText;

    public int Id { get; }
    public string CartonNumber { get => _cartonNumber; set => SetField(ref _cartonNumber, value ?? string.Empty); }
    public string PackageCountText { get => _packageCountText; set => SetIntegerText(ref _packageCountText, value); }
    public string? Contents { get => _contents; set => SetField(ref _contents, value); }
    public string LengthText { get => _lengthText; set => SetDecimalText(ref _lengthText, value); }
    public string WidthText { get => _widthText; set => SetDecimalText(ref _widthText, value); }
    public string HeightText { get => _heightText; set => SetDecimalText(ref _heightText, value); }
    public string NetWeightText { get => _netWeightText; set => SetDecimalText(ref _netWeightText, value); }
    public string GrossWeightText { get => _grossWeightText; set => SetDecimalText(ref _grossWeightText, value); }

    public int PackageCount => int.TryParse(PackageCountText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    public decimal LengthCm => ParseDecimal(LengthText);
    public decimal WidthCm => ParseDecimal(WidthText);
    public decimal HeightCm => ParseDecimal(HeightText);
    public decimal NetWeightKg => ParseDecimal(NetWeightText);
    public decimal GrossWeightKg => ParseDecimal(GrossWeightText);
    public decimal Cbm => decimal.Round(LengthCm * WidthCm * HeightCm / 1_000_000m, 4, MidpointRounding.AwayFromZero);
    public decimal TotalCbm => Cbm * PackageCount;
    public decimal TotalNetWeight => NetWeightKg * PackageCount;
    public decimal TotalGrossWeight => GrossWeightKg * PackageCount;

    public PackingPackageRowViewModel(PackingPackage? source = null)
    {
        Id = source?.Id ?? 0;
        _cartonNumber = source?.CartonNumber ?? string.Empty;
        _packageCountText = (source?.PackageCount ?? 1).ToString(CultureInfo.InvariantCulture);
        _contents = source?.Contents;
        _lengthText = ToText(source?.LengthCm ?? 0m);
        _widthText = ToText(source?.WidthCm ?? 0m);
        _heightText = ToText(source?.HeightCm ?? 0m);
        _netWeightText = ToText(source?.NetWeightKg ?? 0m);
        _grossWeightText = ToText(source?.GrossWeightKg ?? 0m);
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(CartonNumber)) { error = "Every package row needs a carton number or range."; return false; }
        if (PackageCount <= 0) { error = $"Package count for '{CartonNumber}' must be a whole number greater than zero."; return false; }
        if (LengthCm < 0 || WidthCm < 0 || HeightCm < 0 || NetWeightKg < 0 || GrossWeightKg < 0)
        { error = $"Dimensions and weights for '{CartonNumber}' cannot be negative."; return false; }
        error = string.Empty;
        return true;
    }

    public PackingPackage ToEntity(int sortOrder) => new()
    {
        Id = Id,
        SortOrder = sortOrder,
        CartonNumber = CartonNumber.Trim(),
        PackageCount = PackageCount,
        Contents = Contents,
        LengthCm = LengthCm,
        WidthCm = WidthCm,
        HeightCm = HeightCm,
        NetWeightKg = NetWeightKg,
        GrossWeightKg = GrossWeightKg
    };

    private void SetIntegerText(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = string.IsNullOrEmpty(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());

        if (field == normalized) return;
        field = normalized;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(PackageCount));
        OnPropertyChanged(nameof(TotalCbm));
        OnPropertyChanged(nameof(TotalNetWeight));
        OnPropertyChanged(nameof(TotalGrossWeight));
    }

    private void SetDecimalText(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = NormalizeDecimalInput(value);
        if (field == normalized) return;
        field = normalized;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(Cbm));
        OnPropertyChanged(nameof(TotalCbm));
        OnPropertyChanged(nameof(TotalNetWeight));
        OnPropertyChanged(nameof(TotalGrossWeight));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private static string NormalizeDecimalInput(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var builder = new StringBuilder();
        var hasSeparator = false;
        foreach (var c in value)
        {
            if (char.IsDigit(c)) { builder.Append(c); continue; }
            if ((c == '.' || c == ',') && !hasSeparator) { builder.Append('.'); hasSeparator = true; }
        }
        if (builder.Length > 0 && builder[0] == '.') builder.Insert(0, '0');
        return builder.ToString();
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    private static string ToText(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
