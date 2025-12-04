using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _nav;

        public ITabManager TabManager { get; set; }

        public ICommand OpenPageCommand { get; }
        public ICommand CloseTabCommand { get; }



        public MainViewModel(ITabManager tabManager, INavigationService nav)
        {
            TabManager = tabManager;
            _nav = nav;

            OpenPageCommand = new RelayCommand<PageType>(page => _nav.OpenTab(page));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
