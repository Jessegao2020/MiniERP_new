using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.UI.Helper;
using MiniERP.UI.View;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MiniERP.UI.ViewModel
{
    public class ArticleViewModel : INotifyPropertyChanged
    {
        private readonly IArticleService _articleService;
        private readonly INavigationService _navigationService;
        private ObservableCollection<Article> _articles = new();

        public ObservableCollection<Article> Articles
        {
            get => _articles;
            set
            {
                _articles = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadArticlesCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenCreateViewCommand { get; }

        public ArticleViewModel(IArticleService articleService, INavigationService navigationService)
        {
            _articleService = articleService;
            _navigationService = navigationService;
            LoadArticlesCommand = new RelayCommand(() => _ = LoadArticlesAsync());
            RefreshCommand = new RelayCommand(() => _ = LoadArticlesAsync());
            OpenCreateViewCommand = new RelayCommand(OpenCreateView);
            
            // 初始化时加载数据
            _ = LoadArticlesAsync();
        }

        private void OpenCreateView()
        {
            var createView = new ArticleDataView();
            _navigationService.OpenView(createView, "新建物料");
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

