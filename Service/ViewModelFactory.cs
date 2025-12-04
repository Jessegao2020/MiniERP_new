using Microsoft.Extensions.DependencyInjection;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;
using MiniERP.UI.ViewModel;

namespace MiniERP.UI.Service
{
    public class ViewModelFactory : IViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ViewModelFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object CreateViewModel(PageType type)
        {
            var viewmodel = type switch
            {
                PageType.Article => ActivatorUtilities.CreateInstance<ArticleGridViewModel>(_serviceProvider),
                _ => throw new NotSupportedException()
            };

            return viewmodel;
        }
    }
}
