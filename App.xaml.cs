using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.Infrastructure.Data;
using MiniERP.Infrastructure;
using MiniERP.UI.Helper;
using System.IO;
using System.Windows;

namespace MiniERP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 配置依赖注入
            var services = new ServiceCollection();

            // 数据库连接字符串 - 优先查找项目根目录，如果不存在则使用输出目录
            var baseDir = AppContext.BaseDirectory;
            var rootDbPath = Path.Combine(baseDir, "erp.db");
            var connectionString = $"Data Source={rootDbPath}";
            
            // 注册服务
            services.AddInfrastructure(connectionString);
            services.AddApplication();
            services.AddUI();
            
            ServiceProvider = services.BuildServiceProvider();

            // The legacy WPF shell uses its own erp.db and has no separate catalogue path.
            // Initialize an empty schema if it is launched without the old bundled db file.
            using (var scope = ServiceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
            }
            
            base.OnStartup(e);
        }
    }
}
