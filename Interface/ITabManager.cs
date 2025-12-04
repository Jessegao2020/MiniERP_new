using MiniERP.UI.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MiniERP.UI.Interface
{
    public interface ITabManager : INotifyPropertyChanged
    {
        ObservableCollection<TabPageModel> OpenedTabs { get; }
        object SelectedTab { get; set; }

        void AddTab(TabPageModel tab);
        void RemoveTab(TabPageModel tab);
    }
}
