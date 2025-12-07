using MiniERP.UI.Model;

namespace MiniERP.UI.Interface
{
    public interface INavigationService
    {
        void OpenTab(PageType type, string? customizedTitle = null, object? parameter = null);
        void CloseTab(object viewmodel);
    }
}
