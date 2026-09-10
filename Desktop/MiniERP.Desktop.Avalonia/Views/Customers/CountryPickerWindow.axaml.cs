using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop.Views.Customers;

public sealed record CountryOption(string Code, string Name);

public partial class CountryPickerWindow : Window
{
    // ISO 3166-1 alpha-2 countries and territories, plus XK (Kosovo), which is
    // widely used in international business systems even though it is not an
    // officially assigned ISO 3166-1 code.
    private static readonly CountryOption[] AllCountries =
    {
        new("AD", "Andorra"),
        new("AE", "United Arab Emirates"),
        new("AF", "Afghanistan"),
        new("AG", "Antigua and Barbuda"),
        new("AI", "Anguilla"),
        new("AL", "Albania"),
        new("AM", "Armenia"),
        new("AO", "Angola"),
        new("AQ", "Antarctica"),
        new("AR", "Argentina"),
        new("AS", "American Samoa"),
        new("AT", "Austria"),
        new("AU", "Australia"),
        new("AW", "Aruba"),
        new("AX", "Åland Islands"),
        new("AZ", "Azerbaijan"),
        new("BA", "Bosnia and Herzegovina"),
        new("BB", "Barbados"),
        new("BD", "Bangladesh"),
        new("BE", "Belgium"),
        new("BF", "Burkina Faso"),
        new("BG", "Bulgaria"),
        new("BH", "Bahrain"),
        new("BI", "Burundi"),
        new("BJ", "Benin"),
        new("BL", "Saint Barthélemy"),
        new("BM", "Bermuda"),
        new("BN", "Brunei"),
        new("BO", "Bolivia"),
        new("BQ", "Bonaire, Sint Eustatius and Saba"),
        new("BR", "Brazil"),
        new("BS", "Bahamas"),
        new("BT", "Bhutan"),
        new("BV", "Bouvet Island"),
        new("BW", "Botswana"),
        new("BY", "Belarus"),
        new("BZ", "Belize"),
        new("CA", "Canada"),
        new("CC", "Cocos (Keeling) Islands"),
        new("CD", "Congo, Democratic Republic of the"),
        new("CF", "Central African Republic"),
        new("CG", "Congo"),
        new("CH", "Switzerland"),
        new("CI", "Côte d'Ivoire"),
        new("CK", "Cook Islands"),
        new("CL", "Chile"),
        new("CM", "Cameroon"),
        new("CN", "China"),
        new("CO", "Colombia"),
        new("CR", "Costa Rica"),
        new("CU", "Cuba"),
        new("CV", "Cabo Verde"),
        new("CW", "Curaçao"),
        new("CX", "Christmas Island"),
        new("CY", "Cyprus"),
        new("CZ", "Czechia"),
        new("DE", "Germany"),
        new("DJ", "Djibouti"),
        new("DK", "Denmark"),
        new("DM", "Dominica"),
        new("DO", "Dominican Republic"),
        new("DZ", "Algeria"),
        new("EC", "Ecuador"),
        new("EE", "Estonia"),
        new("EG", "Egypt"),
        new("EH", "Western Sahara"),
        new("ER", "Eritrea"),
        new("ES", "Spain"),
        new("ET", "Ethiopia"),
        new("FI", "Finland"),
        new("FJ", "Fiji"),
        new("FK", "Falkland Islands"),
        new("FM", "Micronesia, Federated States of"),
        new("FO", "Faroe Islands"),
        new("FR", "France"),
        new("GA", "Gabon"),
        new("GB", "United Kingdom"),
        new("GD", "Grenada"),
        new("GE", "Georgia"),
        new("GF", "French Guiana"),
        new("GG", "Guernsey"),
        new("GH", "Ghana"),
        new("GI", "Gibraltar"),
        new("GL", "Greenland"),
        new("GM", "Gambia"),
        new("GN", "Guinea"),
        new("GP", "Guadeloupe"),
        new("GQ", "Equatorial Guinea"),
        new("GR", "Greece"),
        new("GS", "South Georgia and the South Sandwich Islands"),
        new("GT", "Guatemala"),
        new("GU", "Guam"),
        new("GW", "Guinea-Bissau"),
        new("GY", "Guyana"),
        new("HK", "Hong Kong"),
        new("HM", "Heard Island and McDonald Islands"),
        new("HN", "Honduras"),
        new("HR", "Croatia"),
        new("HT", "Haiti"),
        new("HU", "Hungary"),
        new("ID", "Indonesia"),
        new("IE", "Ireland"),
        new("IL", "Israel"),
        new("IM", "Isle of Man"),
        new("IN", "India"),
        new("IO", "British Indian Ocean Territory"),
        new("IQ", "Iraq"),
        new("IR", "Iran"),
        new("IS", "Iceland"),
        new("IT", "Italy"),
        new("JE", "Jersey"),
        new("JM", "Jamaica"),
        new("JO", "Jordan"),
        new("JP", "Japan"),
        new("KE", "Kenya"),
        new("KG", "Kyrgyzstan"),
        new("KH", "Cambodia"),
        new("KI", "Kiribati"),
        new("KM", "Comoros"),
        new("KN", "Saint Kitts and Nevis"),
        new("KP", "North Korea"),
        new("KR", "South Korea"),
        new("KW", "Kuwait"),
        new("KY", "Cayman Islands"),
        new("KZ", "Kazakhstan"),
        new("LA", "Laos"),
        new("LB", "Lebanon"),
        new("LC", "Saint Lucia"),
        new("LI", "Liechtenstein"),
        new("LK", "Sri Lanka"),
        new("LR", "Liberia"),
        new("LS", "Lesotho"),
        new("LT", "Lithuania"),
        new("LU", "Luxembourg"),
        new("LV", "Latvia"),
        new("LY", "Libya"),
        new("MA", "Morocco"),
        new("MC", "Monaco"),
        new("MD", "Moldova"),
        new("ME", "Montenegro"),
        new("MF", "Saint Martin (French part)"),
        new("MG", "Madagascar"),
        new("MH", "Marshall Islands"),
        new("MK", "North Macedonia"),
        new("ML", "Mali"),
        new("MM", "Myanmar"),
        new("MN", "Mongolia"),
        new("MO", "Macao"),
        new("MP", "Northern Mariana Islands"),
        new("MQ", "Martinique"),
        new("MR", "Mauritania"),
        new("MS", "Montserrat"),
        new("MT", "Malta"),
        new("MU", "Mauritius"),
        new("MV", "Maldives"),
        new("MW", "Malawi"),
        new("MX", "Mexico"),
        new("MY", "Malaysia"),
        new("MZ", "Mozambique"),
        new("NA", "Namibia"),
        new("NC", "New Caledonia"),
        new("NE", "Niger"),
        new("NF", "Norfolk Island"),
        new("NG", "Nigeria"),
        new("NI", "Nicaragua"),
        new("NL", "Netherlands"),
        new("NO", "Norway"),
        new("NP", "Nepal"),
        new("NR", "Nauru"),
        new("NU", "Niue"),
        new("NZ", "New Zealand"),
        new("OM", "Oman"),
        new("PA", "Panama"),
        new("PE", "Peru"),
        new("PF", "French Polynesia"),
        new("PG", "Papua New Guinea"),
        new("PH", "Philippines"),
        new("PK", "Pakistan"),
        new("PL", "Poland"),
        new("PM", "Saint Pierre and Miquelon"),
        new("PN", "Pitcairn"),
        new("PR", "Puerto Rico"),
        new("PS", "Palestine"),
        new("PT", "Portugal"),
        new("PW", "Palau"),
        new("PY", "Paraguay"),
        new("QA", "Qatar"),
        new("RE", "Réunion"),
        new("RO", "Romania"),
        new("RS", "Serbia"),
        new("RU", "Russian Federation"),
        new("RW", "Rwanda"),
        new("SA", "Saudi Arabia"),
        new("SB", "Solomon Islands"),
        new("SC", "Seychelles"),
        new("SD", "Sudan"),
        new("SE", "Sweden"),
        new("SG", "Singapore"),
        new("SH", "Saint Helena, Ascension and Tristan da Cunha"),
        new("SI", "Slovenia"),
        new("SJ", "Svalbard and Jan Mayen"),
        new("SK", "Slovakia"),
        new("SL", "Sierra Leone"),
        new("SM", "San Marino"),
        new("SN", "Senegal"),
        new("SO", "Somalia"),
        new("SR", "Suriname"),
        new("SS", "South Sudan"),
        new("ST", "Sao Tome and Principe"),
        new("SV", "El Salvador"),
        new("SX", "Sint Maarten (Dutch part)"),
        new("SY", "Syria"),
        new("SZ", "Eswatini"),
        new("TC", "Turks and Caicos Islands"),
        new("TD", "Chad"),
        new("TF", "French Southern Territories"),
        new("TG", "Togo"),
        new("TH", "Thailand"),
        new("TJ", "Tajikistan"),
        new("TK", "Tokelau"),
        new("TL", "Timor-Leste"),
        new("TM", "Turkmenistan"),
        new("TN", "Tunisia"),
        new("TO", "Tonga"),
        new("TR", "Türkiye"),
        new("TT", "Trinidad and Tobago"),
        new("TV", "Tuvalu"),
        new("TW", "Taiwan"),
        new("TZ", "Tanzania"),
        new("UA", "Ukraine"),
        new("UG", "Uganda"),
        new("UM", "United States Minor Outlying Islands"),
        new("US", "United States"),
        new("UY", "Uruguay"),
        new("UZ", "Uzbekistan"),
        new("VA", "Vatican City"),
        new("VC", "Saint Vincent and the Grenadines"),
        new("VE", "Venezuela"),
        new("VG", "Virgin Islands, British"),
        new("VI", "Virgin Islands, U.S."),
        new("VN", "Vietnam"),
        new("VU", "Vanuatu"),
        new("WF", "Wallis and Futuna"),
        new("WS", "Samoa"),
        new("XK", "Kosovo"),
        new("YE", "Yemen"),
        new("YT", "Mayotte"),
        new("ZA", "South Africa"),
        new("ZM", "Zambia"),
        new("ZW", "Zimbabwe")
    };

    private readonly ObservableCollection<CountryOption> _countries = new();
    private readonly SelectLineSortState _sortState = new();

    public CountryPickerWindow(string? currentCode = null)
    {
        InitializeComponent();
        CountryGrid.ItemsSource = _countries;
        ApplyFilter();
        Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Loaded);

        if (!string.IsNullOrWhiteSpace(currentCode))
        {
            var current = AllCountries.FirstOrDefault(country =>
                country.Code.Equals(currentCode.Trim(), StringComparison.OrdinalIgnoreCase));

            if (current is not null)
            {
                CountryGrid.SelectedItem = current;
                Dispatcher.UIThread.Post(() =>
                {
                    if (CountryGrid.Columns.Count > 0)
                        CountryGrid.ScrollIntoView(current, CountryGrid.Columns[0]);
                }, DispatcherPriority.Loaded);
            }
        }
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var codeFilter = CodeFilterTextBox.Text?.Trim() ?? string.Empty;
        var nameFilter = NameFilterTextBox.Text?.Trim() ?? string.Empty;

        _countries.Clear();
        foreach (var country in AllCountries
                     .Where(country =>
                         (string.IsNullOrEmpty(codeFilter) || country.Code.Contains(codeFilter, StringComparison.OrdinalIgnoreCase)) &&
                         (string.IsNullOrEmpty(nameFilter) || country.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase))
        {
            _countries.Add(country);
        }

        ApplySort();
    }

    private void SortHeader_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string field) return;
        _sortState.Toggle(field);
        ApplySort();
        CodeSortArrow.Text = _sortState.Arrow("Code");
        NameSortArrow.Text = _sortState.Arrow("Name");
    }

    private void ApplySort()
    {
        if (_sortState.Field is { } field)
            SelectLineGridSupport.SortInPlace(_countries, field, _sortState.Ascending);
    }

    private void TableLayout_SizeChanged(object? sender, SizeChangedEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void ColumnSplitter_DragDelta(object? sender, VectorEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void SyncDataGridColumnWidths()
        => SelectLineGridSupport.SyncColumnWidths(CountryTableLayout, CountryGrid);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void CountryGrid_DoubleTapped(object? sender, TappedEventArgs e) => CloseSelected();
    private void Ok_Click(object? sender, RoutedEventArgs e) => CloseSelected();
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void CloseSelected()
    {
        if (CountryGrid.SelectedItem is CountryOption selected)
            Close(selected.Code);
    }
}
