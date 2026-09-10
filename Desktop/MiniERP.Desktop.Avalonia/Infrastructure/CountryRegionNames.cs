using System.Globalization;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Converts the two-letter country/region codes stored by Customer into English
/// display names for externally printed documents. Customer editing deliberately
/// keeps the compact code (DE, US, CN, HK, TW, ...).
/// </summary>
public static class CountryRegionNames
{
    private static readonly IReadOnlyDictionary<string, string> PreferredNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CN"] = "China",
            ["HK"] = "Hong Kong",
            ["MO"] = "Macao",
            ["TW"] = "Taiwan",
            ["US"] = "United States",
            ["GB"] = "United Kingdom",
            ["KR"] = "South Korea",
            ["KP"] = "North Korea",
            ["XK"] = "Kosovo"
        };

    public static string? GetDisplayName(string? codeOrName)
    {
        if (string.IsNullOrWhiteSpace(codeOrName))
            return null;

        var value = codeOrName.Trim();
        if (value.Length != 2)
            return value;

        var code = value.ToUpperInvariant();
        if (PreferredNames.TryGetValue(code, out var preferred))
            return preferred;

        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            // Preserve unknown/custom values rather than losing address data.
            return value;
        }
    }

    /// <summary>
    /// Expands a legacy address snapshot whose last non-empty line is a two-letter
    /// country/region code. This keeps old saved quotations printable without
    /// requiring the user to open and re-save them first.
    /// </summary>
    public static string? ExpandAddressCountry(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return address;

        var normalized = address.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        for (var index = lines.Count - 1; index >= 0; index--)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                continue;

            var current = lines[index].Trim();
            if (current.Length == 2)
                lines[index] = GetDisplayName(current) ?? current;
            break;
        }

        return string.Join("\n", lines);
    }
}
