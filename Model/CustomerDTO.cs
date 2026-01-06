using MiniERP.Domain;
using MiniERP.UI.ViewModel;
using System.Collections.ObjectModel;

namespace MiniERP.UI.Model
{
    public class CustomerDTO : ObservableViewModel
    {
        public int Id { get; set; }

        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string? addressLine1;
        public string? AddressLine1
        {
            get => addressLine1;
            set { addressLine1 = value; OnPropertyChanged(); }
        }

        private string? addressLine2;
        public string? AddressLine2
        {
            get => addressLine2;
            set { addressLine2 = value; OnPropertyChanged(); }
        }

        private string? city;
        public string? City
        {
            get => city;
            set { city = value; OnPropertyChanged(); }
        }

        private string? state;
        public string? State
        {
            get => state;
            set { state = value; OnPropertyChanged(); }
        }

        private string? postalCode;
        public string? PostalCode
        {
            get => postalCode;
            set { postalCode = value; OnPropertyChanged(); }
        }

        private string? _countryCode;
        public string? CountryCode
        {
            get => _countryCode;
            set { _countryCode = value; OnPropertyChanged(); }
        }

        private bool isActive = true;
        public bool IsActive
        {
            get => isActive;
            set { isActive = value; OnPropertyChanged(); }
        }

        public ObservableCollection<CustomerContact> Contacts { get; } = new();
    }
}
