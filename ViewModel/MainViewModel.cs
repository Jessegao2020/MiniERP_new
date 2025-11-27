using MiniERP.UI.Helper;
using MiniERP.UI.Model;
using MiniERP.UI.View;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel
{
    public class MainViewModel:INotifyPropertyChanged
    {
        public ICommand OpenPageCommand { get; }
        public ICommand CloseTabCommand { get; }

        public ObservableCollection<TabPageModel> OpenedTabs { get; } = new();

        private TabPageModel _selectedTab;

        public TabPageModel SelectedTab
        {
            get => _selectedTab;
            set
            {
                if(_selectedTab != value)
                {
                    _selectedTab = value;
                }
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            OpenPageCommand = new RelayCommand<string?>(OpenPage);
            CloseTabCommand = new RelayCommand<TabPageModel>(CloseTab);
        }

        private void OpenPage(string pageName)
        {
            UserControl view = pageName switch
            {
                "Article" => new ArticleGridView(),
                "Quotation" => new QuotationGridView(),
                "Customer" => new CustomerGridView(),
                "Order" => new OrderGridView(),
                _ => throw new NotImplementedException(),
            };

            var tab = new TabPageModel
            {
                Title = pageName,
                ContentView = view
            };

            OpenedTabs.Add(tab);
            SelectedTab = tab;
        }

        private void CloseTab(TabPageModel tab)
        {
            OpenedTabs.Remove(tab);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
