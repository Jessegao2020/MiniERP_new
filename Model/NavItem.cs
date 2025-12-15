namespace MiniERP.UI.Model
{
    public class NavItem
    {
        public string Header { get; set; }
        public PageType? PageType { get; set; }
        public List<NavItem> Children { get; set; }

        public NavItem(string header, PageType? pageType = null)
        {
            Header = header;
            PageType = pageType;
        }
    }
}
