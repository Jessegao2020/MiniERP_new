using MiniERP.UI.Model;
using MiniERP.UI.ViewModel.Customer;
using System.Windows;
using System.Windows.Controls;

namespace MiniERP.UI.View
{
    public partial class CustomerDataView : UserControl
    {
        public CustomerDataView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if(DataContext is CustomerDataViewModel viewModel && e.NewValue is NavItem item)
                viewModel.OnNavSelected(item);
        }
    }
}
