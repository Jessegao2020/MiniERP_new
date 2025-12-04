using MiniERP.UI.Model;

namespace MiniERP.UI.Interface
{
    public interface INavigationService
    {
        void OpenTab(PageType type);
        void CloseTab(PageType type);
    }
}
