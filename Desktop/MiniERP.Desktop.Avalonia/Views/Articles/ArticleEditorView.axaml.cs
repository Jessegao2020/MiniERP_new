using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniERP.Desktop.ViewModels.Articles;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Articles;

public partial class ArticleEditorView : UserControl
{
    private ArticleEditorViewModel ViewModel => (ArticleEditorViewModel)DataContext!;
    private bool _normalizingPriceText;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;

    public ArticleEditorView(Article? article)
    {
        InitializeComponent();
        DataContext = new ArticleEditorViewModel(article);

        PriceTextBox.TextInput += (_, e) =>
        {
            var insertedText = e.Text ?? string.Empty;
            var proposed = BuildProposedText(PriceTextBox, insertedText);
            if (!IsValidPriceText(proposed))
                e.Handled = true;
        };

        PriceTextBox.TextChanged += (_, _) => NormalizePriceText();
    }

    private static string BuildProposedText(TextBox textBox, string insertedText)
    {
        var current = textBox.Text ?? string.Empty;
        var start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
        var length = Math.Abs(textBox.SelectionEnd - textBox.SelectionStart);

        return current.Remove(start, length).Insert(start, insertedText);
    }

    private static bool IsValidPriceText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        var decimalPointSeen = false;

        foreach (var c in text)
        {
            if (char.IsDigit(c))
                continue;

            if (c == '.' && !decimalPointSeen)
            {
                decimalPointSeen = true;
                continue;
            }

            return false;
        }

        return true;
    }

    private void NormalizePriceText()
    {
        if (_normalizingPriceText)
            return;

        var current = PriceTextBox.Text ?? string.Empty;
        var normalized = SanitizePriceText(current);

        if (current == normalized)
            return;

        _normalizingPriceText = true;
        PriceTextBox.Text = normalized;
        PriceTextBox.CaretIndex = normalized.Length;
        _normalizingPriceText = false;
    }

    private static string SanitizePriceText(string text)
    {
        var result = new StringBuilder(text.Length);
        var decimalPointSeen = false;

        foreach (var c in text)
        {
            if (char.IsDigit(c))
            {
                result.Append(c);
                continue;
            }

            if (c == '.' && !decimalPointSeen)
            {
                result.Append(c);
                decimalPointSeen = true;
            }
        }

        return result.ToString();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.SaveAsync()) return;

        Saved?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void Discard_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.DeleteAsync()) return;

        Deleted?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
