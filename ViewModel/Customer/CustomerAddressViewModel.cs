using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CustomerAddressViewModel : ObservableViewModel
    {
        private ICustomerService _customerService;
        private IDialogService _dialog;

        public ICommand PickCountryCommand { get; }
        public ICommand SaveCustomerCommand { get; }

        private string? countryCode;

        public string? CountryCode
        {
            get { return countryCode; }
            set
            {
                countryCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CountryDisplay));
            }
        }

        public string CountryDisplay => CountryCode switch
        {
            "CN" => "CN - China",
            "GB" => "GB - United Kingdom",
            "DE" => "DE - Germany",
            "FR" => "FR - France",
            "US" => "US - United States",
            "JP" => "JP - Japan",
            "KR" => "KR - Korea, Republic of",
            null or "" => "",
            _ => CountryCode ?? ""
        };


        public string Name { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public bool IsActive { get; set; }

        public CustomerAddressViewModel(ICustomerService customerService, IDialogService dialog)
        {
            _customerService = customerService;
            _dialog = dialog;

            SaveCustomerCommand = new RelayCommand(async () => await SaveAsync());
            PickCountryCommand = new RelayCommand(PickCountry);

        }

        private async Task SaveAsync()
        {
            Domain.Customer customer = new()
            {
                Name = Name,
                AddressLine1 = AddressLine1,
                AddressLine2 = AddressLine2,
                City = City,
                State = State,
                PostalCode = PostalCode,
                Country = Country,
                IsActive = IsActive
            };

            await _customerService.CreateCustomerAsync(customer);
        }

        private void PickCountry()
        {
            var code = _dialog.PickCountryCode(CountryCode);
            if (code is null) return;      // 用户取消
            CountryCode = code;            // 存 code
        }
    }
}
