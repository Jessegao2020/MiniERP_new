using MiniERP.UI.ViewModel.Customer;
using System.Windows;
using System.Windows.Input;

namespace MiniERP.UI.View.Customer
{
    /// <summary>
    /// CountryPickerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CountryPickerWindow : Window
    {
        public CountryPickerWindow()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CountryPickerViewModel vm && vm.SelectedCountry != null)
                DialogResult = true;
            else
                DialogResult = false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CountryPickerViewModel vm && vm.SelectedCountry != null)
                DialogResult = true;
        }
    }
}
