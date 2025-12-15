using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.UI.Helper;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CustomerAddressViewModel
    {
        private ICustomerService _customerService;

        public ICommand SaveCustomerCommand { get; }

        public string Name { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public bool IsActive { get; set; }

        public CustomerAddressViewModel(ICustomerService customerService)
        {
            _customerService = customerService;

            SaveCustomerCommand = new RelayCommand(async () =>await SaveAsync());

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
    }
}
