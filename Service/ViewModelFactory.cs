using Microsoft.Extensions.DependencyInjection;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using MiniERP.UI.ViewModel;
using MiniERP.UI.ViewModel.Customer;

namespace MiniERP.UI.Service
{
    public class ViewModelFactory : IViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;

        private static readonly Dictionary<PageType, Type> viewmodelDic = new()
        {
            {PageType.Article, typeof(ArticleGridViewModel) },
            {PageType.ArticleData, typeof(ArticleDataViewModel) },
            {PageType.Customer, typeof(CustomerGridViewModel) },
            {PageType.CustomerData, typeof(CustomerDataViewModel) },
            {PageType.CustomerAddress, typeof(CustomerAddressViewModel) },
            {PageType.CustomerContact, typeof(CustomerContactGridViewModel) },
        };

        public ViewModelFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object CreateViewModel(PageType type, object? parameter = null)
        {
            if (!viewmodelDic.TryGetValue(type, out var vmType))
                throw new NotSupportedException();

            var viewmodel = ActivatorUtilities.CreateInstance(_serviceProvider, vmType);

            if (viewmodel is IPolymorphicViewModel vm)
                vm.Initialize(parameter);

            return viewmodel;
        }
    }
}
