using Microsoft.Extensions.DependencyInjection;
using MiniERP.UI.Interface;
using MiniERP.UI.Service;
using MiniERP.UI.ViewModel;

namespace MiniERP.UI.Helper
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddUI(this IServiceCollection services)
        {
            // ViewModels
            services.AddSingleton<MainViewModel>();

            // Services
            services.AddSingleton<INavigationService,NavigationService>();
            services.AddSingleton<ITabManager, TabManager>();
            services.AddSingleton<IViewModelFactory, ViewModelFactory>();

            return services;
        }
    }
}


