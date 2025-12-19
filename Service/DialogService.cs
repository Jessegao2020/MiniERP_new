using MiniERP.UI.Interface;
using MiniERP.UI.View.Customer;
using MiniERP.UI.ViewModel.Customer;
using System.Windows;

namespace MiniERP.UI.Service
{
    public class DialogService : IDialogService
    {
        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public bool ShowConfirm(string message, string title = "Confirm")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }

        public string? PickCountryCode(string? currentCode = null)
        {
            var vm = new CountryPickerViewModel(currentCode);
            var win = new CountryPickerWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            var ok = win.ShowDialog() == true;
            return ok ? vm.SelectedCountryCode : null;
        }
    }
}
