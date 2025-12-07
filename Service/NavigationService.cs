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

        public void OpenTab(PageType type, string? customizedTitle = null, object? parameter = null)
        {
            var title = customizedTitle ?? type.ToString();
            var existing = _tabManager.OpenedTabs.FirstOrDefault(t => t.Title == title);
            if (existing != null)
            {
                _tabManager.SelectedTab = existing;
                return;
            }

            var viewmodel = new TabPageModel(title, _factory.CreateViewModel(type, parameter));
            _tabManager.AddTab(viewmodel);
            _tabManager.SelectedTab = viewmodel;
        }

        public void CloseTab(object viewmodel)
        {
            var tab = _tabManager.OpenedTabs.FirstOrDefault(t=>ReferenceEquals(t.ContentView, viewmodel));
            _tabManager.RemoveTab(tab);
        }
    }
}
