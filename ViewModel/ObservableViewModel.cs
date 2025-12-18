using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MiniERP.UI.ViewModel
{
    public class ObservableViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
