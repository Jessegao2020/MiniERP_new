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
    public class MainViewModel : INotifyPropertyChanged, INavigationService
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

            OpenView(view, pageName);
        }

        /// <summary>
        /// 实现 INavigationService 接口，在标签页中打开视图
        /// </summary>
        public void OpenView(UserControl view, string title)
        {
            var tab = new TabPageModel
            {
                Title = title,
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
