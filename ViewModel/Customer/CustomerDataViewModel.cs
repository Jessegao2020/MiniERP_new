using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using System.Collections.ObjectModel;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CustomerDataViewModel : ObservableViewModel
    {
        private readonly INavigationService _nav;
        private readonly IViewModelFactory _viewModelFactory;
        private object currentPage;
        private CustomerAddressViewModel? _addressVm;
        private CustomerContactGridViewModel? _contactVm;

        public CustomerDTO CustomerDTO { get; } = new();
        private readonly Dictionary<PageType, object> _pageCache = new();

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

        public CustomerDataViewModel(INavigationService nav, IViewModelFactory viewModelFactory)
        {
            _nav = nav;
            _viewModelFactory = viewModelFactory;
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

            // 关键：子页面 parameter 统一传 Editor
            vm = _viewModelFactory.CreateViewModel(pageType, CustomerDTO);
            _pageCache[pageType] = vm;
            return vm;
        }
    }
}
