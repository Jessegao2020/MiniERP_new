using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;
using MiniERP.UI.Helper;
using MiniERP.UI.Interface;
using MiniERP.UI.Messaging;

namespace MiniERP.UI.ViewModel
{
    public class ArticleDataViewModel : ObservableViewModel, IPolymorphicViewModel
    {

        private readonly IArticleService _articleService;
        private readonly INavigationService _nav;
        private readonly IEventBus _eventBus;
        private readonly IDialogService _dialog;

        private Article _article;
        private bool _isNew;

        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }  // 暂未使用

        public Article Article
        {
            get { return _article; }
            set { _article = value; OnPropertyChanged(); }
        }

        public ArticleDataViewModel(
            IArticleService articleService,
            INavigationService nav,
            IEventBus eventBus,
            IDialogService dialog)
        {
            _articleService = articleService;
            _nav = nav;
            _eventBus = eventBus;
            _dialog = dialog;

            Article = new();

            SaveCommand = new RelayCommand(SaveAsync);
            DeleteCommand = new RelayCommand(DeleteAsync);
        }

        public void Initialize(object? parameter = null)
        {
            switch (parameter)
            {
                case null:
                    _isNew = true; break;

                case Article article:
                    Article = article;
                    break;

                default: throw new ArgumentException($"Not supported argument: {parameter.GetType()}");
            }
        }

        private async void SaveAsync()
        {
            try
            {
                if (_isNew)
                    await _articleService.CreateArticleAsync(Article);
                else
                    await _articleService.UpdateArticleAsync(Article);

                var message = new EntityChangedMessage<Article>(Article, _isNew ? EntityChangeType.Created : EntityChangeType.Updated);
                _eventBus.Publish(message);

                _nav.CloseTab(this);
            }
            catch (Exception ex)
            {
                _dialog.ShowError($"保存失败：{ex.Message}");
            }
        }

        private async void DeleteAsync()
        {
            try
            {
                var result = _dialog.ShowConfirm("Are you sure to delete?");
                if (result == true)
                {
                    await _articleService.DeleteArticleAsync(Article.Id);

                    var message = new EntityChangedMessage<Article>(Article, EntityChangeType.Deleted);
                    _eventBus.Publish(message);

                    _nav.CloseTab(this);
                }

            }
            catch (Exception ex)
            {
                _dialog.ShowError($"Delete failed: {ex.Message}");
            }
        }
    }
}
