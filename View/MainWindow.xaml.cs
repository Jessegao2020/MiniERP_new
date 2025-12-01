using Microsoft.Extensions.DependencyInjection;
using MiniERP.UI.ViewModel;
using System.Windows;

namespace MiniERP.UI.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 从依赖注入容器获取 MainViewModel
            DataContext = App.ServiceProvider.GetRequiredService<MainViewModel>();
        }
    }
}