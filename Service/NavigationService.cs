using MiniERP.UI.Interface;
using MiniERP.UI.Model;

namespace MiniERP.UI.Service
{
    public class NavigationService : INavigationService
    {
        private readonly ITabManager _tabManager;
        private readonly IViewModelFactory _factory;

        public NavigationService(IViewModelFactory factory, ITabManager tabManager)
        {
            _tabManager = tabManager;
            _factory = factory;
        }

        public void OpenTab(PageType type)
        {
            var existing = _tabManager.OpenedTabs.FirstOrDefault(t => t.Title == type.ToString());
            if (existing != null)
            {
                _tabManager.SelectedTab = existing;
                return;
            }

            var viewmodel = new TabPageModel(type.ToString(), _factory.CreateViewModel(type));
            _tabManager.AddTab(viewmodel);
            _tabManager.SelectedTab = viewmodel;
        }

        public void CloseTab(PageType type)
        {
            var tab = _tabManager.OpenedTabs.FirstOrDefault(t=>t.Title == type.ToString());
            _tabManager.RemoveTab(tab);
        }
    }
}
