using Microsoft.Extensions.DependencyInjection;
using MiniERP.UI.ViewModel;

namespace MiniERP.UI.Helper
{
    /// <summary>
    /// UI 层依赖注入配置
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// 注册 UI 层的服务（ViewModel、导航服务等）
        /// </summary>
        public static IServiceCollection AddUI(this IServiceCollection services)
        {
            // 注册 ViewModel
            services.AddTransient<ArticleViewModel>();
            
            // 注册 MainViewModel 为单例，同时作为 INavigationService 的实现
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<MainViewModel>());
            
            return services;
        }
    }
}

