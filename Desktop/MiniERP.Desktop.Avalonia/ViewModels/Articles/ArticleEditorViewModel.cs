using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Articles;

public sealed class ArticleEditorViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settings;
    private string _status = string.Empty;
    private string _priceText = string.Empty;

    public Article Article { get; }
    public bool IsNew { get; private set; }

    public string PriceText
    {
        get => _priceText;
        set
        {
            if (_priceText == value) return;
            _priceText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsdPriceText));
        }
    }

    public string UsdPriceText
    {
        get
        {
            var rate = _settings.Current.CnyPerUsd;
            if (rate is null || rate <= 0)
                return string.Empty;

            if (!decimal.TryParse(PriceText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var cnyPrice))
                return string.Empty;

            return (cnyPrice / rate.Value).ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public ArticleEditorViewModel(Article? source, AppSettingsService settings)
    {
        _settings = settings;
        IsNew = source is null;
        Article = source is null ? new Article() : Clone(source);
        PriceText = Article.Price?.ToString("0.############################", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public void RefreshExchangeRate()
        => OnPropertyChanged(nameof(UsdPriceText));

    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Article.Name))
        {
            Status = "Name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(PriceText))
        {
            Article.Price = null;
        }
        else if (decimal.TryParse(PriceText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) && price >= 0)
        {
            Article.Price = price;
        }
        else
        {
            Status = "Price must be a valid non-negative number.";
            return false;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IArticleService>();

            if (IsNew)
            {
                await service.CreateArticleAsync(Article);
                IsNew = false;
            }
            else
            {
                await service.UpdateArticleAsync(Article);
            }

            Status = "Saved.";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteAsync()
    {
        if (IsNew || Article.Id == 0)
            return true;

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IArticleService>();
            await service.DeleteArticleAsync(Article.Id);
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
            return false;
        }
    }

    private static Article Clone(Article source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Price = source.Price,
        MinimumPrice = source.MinimumPrice,
        Description = source.Description,
        Specification = source.Specification,
        Discount = source.Discount,
        Note = source.Note,
        Specs_EN = source.Specs_EN,
        Category = source.Category,
        Name_EN = source.Name_EN,
        Description_EN = source.Description_EN,
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedBy = source.LastModifiedBy,
        LastModifiedAt = source.LastModifiedAt
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
