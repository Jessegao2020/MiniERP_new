using MiniERP.Domain;
using MiniERP.UI.ViewModel;
using System.Collections.ObjectModel;

namespace MiniERP.UI.Model
{
    public class CustomerDTO : ObservableViewModel
    {
        public int Id { get; set; }
        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        private string? _countryCode;
        public string? CountryCode { get => _countryCode; set { _countryCode = value; OnPropertyChanged(); } }

        public ObservableCollection<CustomerContact> Contacts { get; } = new();
    }
}
