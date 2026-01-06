using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.UI.Interface;
using MiniERP.UI.Messaging;
using MiniERP.UI.Model;
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
            services.AddSingleton<CustomerDTO>();

            // Services
            services.AddSingleton<INavigationService,NavigationService>();
            services.AddSingleton<ITabManager, TabManager>();
            services.AddSingleton<IViewModelFactory, ViewModelFactory>();
            services.AddSingleton<IEventBus, EventBus>();
            services.AddSingleton<IDialogService, DialogService>();

            return services;
        }
    }
}


