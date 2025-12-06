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
    public class ArticleDataViewModel : INotifyPropertyChanged
    {
        private readonly IArticleService _articleService;
        private readonly INavigationService _nav;
        private readonly IEventBus _eventBus;
        private readonly IDialogService _dialog;

        private Article _article;

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
        }

        private async void SaveAsync()
        {
            try
            {
                await _articleService.CreateArticleAsync(Article);

                var message = new EntityChangedMessage<Article>(Article, EntityChangeType.Created);
                _eventBus.Publish(message);

                _nav.CloseTab(PageType.ArticleData);
            }
            catch (Exception ex)
            {
                _dialog.ShowError($"保存失败：{ex.Message}");
            }
        }

        private async void DeleteAsync(int selectedArticle)
        {
            try
            {
                await _articleService.DeleteArticleAsync(selectedArticle);
                
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Delete failed: {ex.Message}");
            }
        }


        #region Event Handling

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
