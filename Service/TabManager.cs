using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MiniERP.UI.Service
{
    public class TabManager : ITabManager
    {
        public ObservableCollection<TabPageModel> OpenedTabs { get; } = new();

        private object _selectedTab;

        public object SelectedTab
        {
            get { return _selectedTab; }
            set
            {
                if (_selectedTab != value)
                {
                    _selectedTab = value;
                    OnPropertyChanged();
                }
            }
        }

        public void AddTab(TabPageModel tab)
        {
            OpenedTabs.Add(tab);
            SelectedTab = tab;
        }

        public void RemoveTab(TabPageModel tab)
        {
            OpenedTabs.Remove(tab);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
