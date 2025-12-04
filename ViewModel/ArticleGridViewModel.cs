using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using MiniERP.UI.Model;

namespace MiniERP.UI.ViewModel
{
    public class ArticleGridViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _nav;
        private readonly IArticleService _articleService;

        private ObservableCollection<Article> _articles = new();

        public ITabManager TabManager { get; set; }
        public ObservableCollection<Article> Articles
        {
            get => _articles;
            set
            {
                _articles = value;
                OnPropertyChanged();
            }
        }
        public ICommand OpenTabCommand { get; }
        public ICommand LoadArticlesCommand { get; }    // 暂时未使用
        public ICommand RefreshCommand { get; }  // 暂时未使用

        public ArticleGridViewModel(INavigationService nav, ITabManager tabManager, IArticleService articleService)
        {
            _nav = nav;
            _articleService = articleService;
            TabManager = tabManager;

            LoadArticlesCommand = new RelayCommand(() => _ = LoadArticlesAsync());  // 暂时未使用
            RefreshCommand = new RelayCommand(() => _ = LoadArticlesAsync());   // 暂时未使用
            OpenTabCommand = new RelayCommand<PageType>(tab => _nav.OpenTab(tab));
            _ = LoadArticlesAsync();
        }

        private async Task LoadArticlesAsync()
        {
            try
            {
                var articles = await _articleService.GetAllArticlesAsync();
                Articles = new ObservableCollection<Article>(articles);
            }
            catch (Exception ex)
            {
                // 这里可以添加日志记录
                System.Windows.MessageBox.Show($"加载数据失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
