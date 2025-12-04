using MiniERP.UI.Model;

namespace MiniERP.UI.Interface
{
    public interface IViewModelFactory
    {
        object CreateViewModel(PageType type);
    }
}
