using MiniERP.UI.Interface;
using MiniERP.UI.Model;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CustomerDataViewModel : ObservableViewModel
    {
        private readonly INavigationService _nav;
        private readonly IViewModelFactory _viewModelFactory;
        private object currentPage;

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
            if (item?.PageType is null) return; // 父节点不切换

            CurrentPage = item.PageType.Value switch
            {
                PageType.CustomerAddress => _viewModelFactory.CreateViewModel(PageType.CustomerAddress),
                _ => null
            };
        }
    }
}
