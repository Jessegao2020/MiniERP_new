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
        #region Fields

        private readonly INavigationService _nav;

        #endregion

        #region Properties

        public ITabManager TabManager { get; set; }

        #endregion

        #region Commands

        public ICommand OpenPageCommand { get; }
        public ICommand CloseTabCommand { get; }

        #endregion

        #region Ctor

        public MainViewModel(ITabManager tabManager, INavigationService nav)
        {
            TabManager = tabManager;
            _nav = nav;

            OpenPageCommand = new RelayCommand<PageType>(page => _nav.OpenTab(page));
            CloseTabCommand = new RelayCommand<TabPageModel>(tab => TabManager.RemoveTab(tab));
        }

        #endregion

        #region Event Handling

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
