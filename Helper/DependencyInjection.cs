using Microsoft.Extensions.DependencyInjection;
using MiniERP.UI.Interface;
using MiniERP.UI.Service;
using MiniERP.UI.View;
using MiniERP.UI.ViewModel;

namespace MiniERP.UI.Helper
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddUI(this IServiceCollection services)
        {
            // 注册 ViewModel
            services.AddTransient<ArticleViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<INavigationService,NavigationService>();
            services.AddSingleton<ITabManager, TabManager>();
            services.AddSingleton<IViewModelFactory, ViewModelFactory>();

            return services;
        }
    }
}


