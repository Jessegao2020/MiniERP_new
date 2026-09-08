using System.Text;
using Avalonia.Controls;

namespace MiniERP.Desktop.Controls;

public sealed class DecimalTextBoxFilter
{
    private readonly TextBox _textBox;
    private bool _normalizing;

    public DecimalTextBoxFilter(TextBox textBox)
    {
        _textBox = textBox;
        _textBox.TextInput += OnTextInput;
        _textBox.TextChanged += OnTextChanged;
    }

    private void OnTextInput(object? sender, Avalonia.Input.TextInputEventArgs e)
    {
        var insertedText = e.Text ?? string.Empty;
        var proposed = BuildProposedText(insertedText);

        if (!IsValidDecimalText(proposed))
            e.Handled = true;
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (_normalizing)
            return;

        var current = _textBox.Text ?? string.Empty;
        var normalized = SanitizeDecimalText(current);

        if (current == normalized)
            return;

        _normalizing = true;
        _textBox.Text = normalized;
        _textBox.CaretIndex = normalized.Length;
        _normalizing = false;
    }

    private string BuildProposedText(string insertedText)
    {
        var current = _textBox.Text ?? string.Empty;
        var start = Math.Min(_textBox.SelectionStart, _textBox.SelectionEnd);
        var length = Math.Abs(_textBox.SelectionEnd - _textBox.SelectionStart);
        return current.Remove(start, length).Insert(start, insertedText);
    }

    private static bool IsValidDecimalText(string text)
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

    private static string SanitizeDecimalText(string text)
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
}
