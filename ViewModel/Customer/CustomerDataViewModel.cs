using MiniERP.UI.Model;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CustomerDataViewModel
    {
        public List<NavItem> NavItems { get;  } = new()
        {
            new NavItem("Address", PageType.CustomerAddress),
            new NavItem("History")
            {
                Children = new List<NavItem>
                {
                    new NavItem("Quote", PageType.CustomerQuote ),
                    new NavItem("Order", PageType.CustomerOrder),
                }
            },
        };
    }
}
