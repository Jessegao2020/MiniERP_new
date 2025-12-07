using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using MiniERP.UI.Messaging;
using MiniERP.UI.Model;

namespace MiniERP.UI.ViewModel
{
    public class ArticleGridViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly INavigationService _nav;
        private readonly IArticleService _articleService;
        private readonly IEventBus _eventBus;
        private readonly IDialogService _dialog;
        private readonly Action<EntityChangedMessage<Article>> _articleChangedHandler;
        private readonly ArticleDataViewModel _articleDataViewModel;

        private ObservableCollection<Article> _articles = new();
        private Article? _selectedArticle;

        #endregion

        #region Properties

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

        public Article? SelectedArticle
        {
            get { return _selectedArticle; }
            set { _selectedArticle = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand OpenCreateViewCommand { get; }
        public ICommand DeleteArticleCommand { get; }
        public ICommand OpenEditViewCommand { get; }
        public ICommand LoadArticlesCommand { get; }    // 暂时未使用
        public ICommand RefreshCommand { get; }  // 暂时未使用

        #endregion

        #region Ctor

        public ArticleGridViewModel(
            INavigationService nav,
            ITabManager tabManager,
            IArticleService articleService,
            IEventBus eventBus,
            IDialogService dialog)
        {
            // Injection
            _nav = nav;
            _articleService = articleService;
            _eventBus = eventBus;
            _dialog = dialog;
            TabManager = tabManager;

            // Command Methods
            OpenCreateViewCommand = new RelayCommand<PageType>(tab => _nav.OpenTab(tab, customizedTitle: "New Article"));
            DeleteArticleCommand = new RelayCommand(DeleteArticleAsync);
            OpenEditViewCommand = new RelayCommand<Article?>(OpenDetails);
            LoadArticlesCommand = new RelayCommand(() => _ = LoadArticlesAsync());  // 暂时未使用
            RefreshCommand = new RelayCommand(() => _ = LoadArticlesAsync());   // 暂时未使用

            _ = LoadArticlesAsync();

            _articleChangedHandler = OnArticleChanged;
            _eventBus.Subscribe(_articleChangedHandler);
        }

        #endregion

        #region Local Methods

        private async Task LoadArticlesAsync()
        {
            try
            {
                var articles = await _articleService.GetAllArticlesAsync();
                Articles = new ObservableCollection<Article>(articles);
            }
            catch (Exception ex)
            {
                _dialog.ShowError($"加载数据失败: {ex.Message}");
            }
        }

        private async void DeleteArticleAsync()
        {
            try
            {
                if (SelectedArticle == null)
                {
                    _dialog.ShowWarning("Please select an article.");
                    return;
                }

                var result = _dialog.ShowConfirm("Are you sure to delete?");
                if (result == true)
                {
                    await _articleService.DeleteArticleAsync(SelectedArticle.Id);
                    Articles.Remove(SelectedArticle);
                }
            }
            catch (Exception ex)
            {
                _dialog.ShowError($"Delete failed: {ex.Message}");
            }
        }

        private void OpenDetails(Article? article)
        {
            if (article is null) return;

            _nav.OpenTab(PageType.ArticleData, "Article Details", article);
        }

        private void OnArticleChanged(EntityChangedMessage<Article> message)
        {
            switch (message.ChangeType)
            {
                case EntityChangeType.Created:
                    Articles.Add(message.Entity);
                    break;

                case EntityChangeType.Updated:
                    break;

                case EntityChangeType.Deleted:
                    Articles.Remove(message.Entity);
                    break;
            }
        }

        #endregion

        #region Event Handling

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion
    }
}
