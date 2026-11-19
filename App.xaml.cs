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
            var catalogPath = Path.Combine(baseDir, "catalog.db");
            
            // 注册服务
            services.AddInfrastructure(connectionString, $"Data Source={catalogPath}");
            services.AddApplication();
            services.AddUI();
            
            ServiceProvider = services.BuildServiceProvider();

            // WPF now shares the same two-database separation contract as Avalonia.
            // Its paths remain beside the executable for backward compatibility.
            using (var scope = ServiceProvider.CreateScope())
            {
                DatabaseStartupMigrator.Upgrade(
                    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
                    scope.ServiceProvider.GetRequiredService<CatalogDbContext>(),
                    rootDbPath, catalogPath);
            }
            
            base.OnStartup(e);
        }
    }
}
