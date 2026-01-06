using MiniERP.ApplicationLayer.Services;
using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel.Customer
{
    public class CustomerAddressViewModel : ObservableViewModel, IPolymorphicViewModel
    {
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialog;

        public ICommand PickCountryCommand { get; }
        public CustomerDTO CustomerDTO { get; private set; } = null!;

        public CustomerAddressViewModel(IDialogService dialog)
        {
            _dialog = dialog;

            PickCountryCommand = new RelayCommand(PickCountry);
        }

        public void Initialize(object? parameter)
        {
            CustomerDTO = (parameter as CustomerDTO)
                ?? throw new ArgumentException("CustomerDTO is required");
            OnPropertyChanged(nameof(CustomerDTO));
        }

        private void PickCountry()
        {
            var code = _dialog.PickCountryCode(CustomerDTO.CountryCode);
            if (code is null) return;
            CustomerDTO.CountryCode = code;
        }
    }
}
