using MiniERP.Domain;
using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel.Customer
{
    class CustomerContactGridViewModel : ObservableViewModel
    {

        public CustomerDTO CustomerDTO { get; private set; } = null!;
        public ObservableCollection<CustomerContact> Contacts => CustomerDTO.Contacts;

        public void Initialize(object? parameter)
        {
            CustomerDTO = (parameter as CustomerDTO)
                ?? throw new ArgumentException("CustomerEditorContext is required");
            OnPropertyChanged(nameof(CustomerDTO));
            OnPropertyChanged(nameof(Contacts));
        }
    }
}
