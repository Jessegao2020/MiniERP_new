using MiniERP.UI.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CountryPickerViewModel : ObservableViewModel
    {
        public ObservableCollection<Country> Countries { get; } = new();

        private Country? selectedCountry;

        public Country? SelectedCountry
        {
            get { return selectedCountry; }
            set { selectedCountry = value; OnPropertyChanged(); }
        }

        public string? SelectedCountryCode => SelectedCountry?.Code;

        public CountryPickerViewModel(string? currentCode = null)
        {
            Countries.Add(new Country("CN", "China"));
            Countries.Add(new Country("GB", "United Kingdom")); // ISO 推荐 GB（UK 可做别名）
            Countries.Add(new Country("DE", "Germany"));
            Countries.Add(new Country("FR", "France"));
            Countries.Add(new Country("US", "United States"));
            Countries.Add(new Country("JP", "Japan"));
            Countries.Add(new Country("KR", "Korea, Republic of"));

            if (!string.IsNullOrWhiteSpace(currentCode))
            {
                var code = currentCode.Trim().ToUpperInvariant();
                SelectedCountry = Countries.FirstOrDefault(c =>
                    c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
