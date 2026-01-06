using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CustomerDataViewModel : ObservableViewModel
    {
        private readonly INavigationService _nav;
        private readonly IViewModelFactory _viewModelFactory;
        private readonly ICustomerService _customerService;

        private object currentPage;
        private CustomerAddressViewModel? _addressVm;
        private CustomerContactGridViewModel? _contactVm;
        private readonly Dictionary<PageType, object> _pageCache = new();


        public ICommand SaveCustomerCommand { get; }
        public ICommand DeleteCustomerCommand { get; }

        public CustomerDTO CustomerDTO { get; } = new();

        public object CurrentPage
        {
            get { return currentPage; }
            set
            {
                currentPage = value;
                OnPropertyChanged();
            }
        }

        public List<NavItem> NavItems { get; } = new()
        {
            new NavItem("Address", PageType.CustomerAddress),
            new NavItem("Contact", PageType.CustomerContact),
            new NavItem("History")
            {
                Children = new List<NavItem>
                {
                    new NavItem("Quote", PageType.CustomerQuote ),
                    new NavItem("Order", PageType.CustomerOrder),
                }
            },
        };

        public CustomerDataViewModel(INavigationService nav, IViewModelFactory viewModelFactory, ICustomerService customerService)
        {
            _nav = nav;
            _viewModelFactory = viewModelFactory;
            _customerService = customerService;
            SaveCustomerCommand = new RelayCommand(async () => await SaveCustomerAsync());
        }

        public void OnNavSelected(NavItem item)
        {
            if (item?.PageType is null) return;
            CurrentPage = GetOrCreatePage(item.PageType.Value);
        }

        private object GetOrCreatePage(PageType pageType)
        {
            if (_pageCache.TryGetValue(pageType, out var vm))
                return vm;

            vm = _viewModelFactory.CreateViewModel(pageType, CustomerDTO);
            _pageCache[pageType] = vm;
            return vm;
        }

        private async Task SaveCustomerAsync()
        {
            MiniERP.Domain.Customer customer = new Domain.Customer
            {
                Id = CustomerDTO.Id,
                Name = CustomerDTO.Name,
                AddressLine1 = CustomerDTO.AddressLine1,
                AddressLine2 = CustomerDTO.AddressLine2,
                City = CustomerDTO.City,
                State = CustomerDTO.State,
                PostalCode = CustomerDTO.PostalCode,
                IsActive = CustomerDTO.IsActive,
                Country = CustomerDTO.CountryCode
            };

            if (CustomerDTO.Id == 0)
            {
                await _customerService.CreateCustomerAsync(customer);
                CustomerDTO.Id = customer.Id;
            }
            else
                await _customerService.UpdateCustomerAsync(customer);
        }
    }
}
