using System.Windows.Controls;

namespace MiniERP.UI.Helper
{
    /// <summary>
    /// 导航服务接口，用于在标签页中打开视图
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// 在标签页中打开指定的视图
        /// </summary>
        /// <param name="view">要打开的视图控件</param>
        /// <param name="title">标签页标题</param>
        void OpenView(UserControl view, string title);
    }
}

