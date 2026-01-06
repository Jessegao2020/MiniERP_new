using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CustomerGridViewModel
    {
        private readonly ICustomerService _customerService;
        private readonly INavigationService _nav;

        public ObservableCollection<MiniERP.Domain.Customer> Customers { get; set; }

        public ICommand OpenCreateViewCommand { get; }
        public ICommand OpenEditViewCommand { get; }


        public CustomerGridViewModel(ICustomerService customerService, INavigationService nav)
        {
            _customerService = customerService;
            _nav = nav;

            OpenCreateViewCommand = new RelayCommand<PageType>(type => _nav.OpenTab(type, "Customer Details"));
            OpenEditViewCommand = new RelayCommand<Domain.Customer?>(OpenDetails);

            _ = LoadCustomersAsync();
        }

        private async Task LoadCustomersAsync()
        {
            var customers = await _customerService.GetAllCustomersAsync();
            Customers = new ObservableCollection<Domain.Customer>(customers);
        }

        private void OpenDetails(Domain.Customer? customer)
        {
            if (customer == null) return;

            _nav.OpenTab(PageType.CustomerData, "Customer Details", customer);
        }
    }
}
