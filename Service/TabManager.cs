using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;

namespace MiniERP.UI.Service
{
    public class TabManager : ITabManager
    {
        private object _selectedTab;

        public ObservableCollection<TabPageModel> OpenedTabs { get; } = new();
        public object SelectedTab
        {
            get => _selectedTab;
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
            => OpenedTabs.Remove(tab);


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
