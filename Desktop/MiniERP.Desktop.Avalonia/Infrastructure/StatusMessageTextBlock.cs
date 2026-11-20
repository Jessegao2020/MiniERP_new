using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Receives the existing ViewModel.Status binding without showing an in-page
/// status strip. Actionable status updates are displayed as owner-modal dialogs.
/// Routine load/filter/selection status is intentionally not a dialog.
/// </summary>
public sealed class StatusMessageTextBlock : TextBlock
{
    private static readonly SemaphoreSlim DialogGate = new(1, 1);
    private bool _attached;
    private INotifyPropertyChanged? _source;
    private string? _lastMessage;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _lastMessage = Text;
        SubscribeToStatus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeFromStatus();
        _attached = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (!_attached)
            return;

        UnsubscribeFromStatus();
        _lastMessage = Text;
        SubscribeToStatus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // For INotifyPropertyChanged ViewModels, listen directly to Status rather
        // than Text. Text bindings may not raise a change when the same validation
        // message is emitted twice in succession.
        if (change.Property == TextProperty && _attached && _source is null)
            HandleMessage(Text, allowRepeat: false);
    }

    private void SubscribeToStatus()
    {
        _source = DataContext as INotifyPropertyChanged;
        if (_source is not null)
            _source.PropertyChanged += StatusPropertyChanged;
    }

    private void UnsubscribeFromStatus()
    {
        if (_source is not null)
            _source.PropertyChanged -= StatusPropertyChanged;
        _source = null;
    }

    private void StatusPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_attached || (e.PropertyName != "Status" && !string.IsNullOrEmpty(e.PropertyName)))
            return;

        var message = sender?.GetType().GetProperty("Status")?.GetValue(sender)?.ToString();
        HandleMessage(message, allowRepeat: true);
    }

    private void HandleMessage(string? value, bool allowRepeat)
    {
        var message = (value ?? string.Empty).Trim();
        if (message.Length == 0 || (!allowRepeat && string.Equals(_lastMessage, message, StringComparison.Ordinal)))
            return;

        _lastMessage = message;
        if (ShouldOpenDialog(message))
            _ = ShowStatusDialogAsync(message);
    }

    private static bool ShouldOpenDialog(string message)
    {
        // These are ambient page status indicators, not notifications. Showing
        // them as modal alerts would interrupt every navigation and filter edit.
        if (message is "Ready." or "New quotation." or "New user." or "New customer." or "New article.")
            return false;

        if (message.StartsWith("Editing ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Exchange rate refreshed", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Quotation currency changed", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Quotation repriced", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Article selected:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Country set to ", StringComparison.OrdinalIgnoreCase))
            return false;

        // Lists publish a count on load and with every filter keystroke.
        if (char.IsDigit(message[0]) && (message.Contains(" of ", StringComparison.Ordinal)
            || message.Contains(" record", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" item(s)", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" quotation(s)", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" customer(s)", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" article(s)", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" contract(s)", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" document(s)", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" packing list(s)", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" user(s)", StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private async Task ShowStatusDialogAsync(string message)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        await DialogGate.WaitAsync();
        try
        {
            // A workspace may have closed while other dialogs were awaiting OK.
            if (!_attached || !ReferenceEquals(TopLevel.GetTopLevel(this), owner))
                return;

            var dialog = new Window
            {
                Title = IsErrorMessage(message) ? "MiniERP - Attention" : "MiniERP - Notification",
                Width = 460,
                MinHeight = 165,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 88,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            okButton.Click += (_, _) => dialog.Close();

            var content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 22
            };
            content.Children.Add(messageText);
            content.Children.Add(okButton);
            dialog.Content = content;

            // ShowDialog is owner-modal. Until OK / close, the underlying
            // MiniERP window cannot be used.
            await dialog.ShowDialog(owner);
        }
        catch (InvalidOperationException)
        {
            // The main window can close while an alert is queued.
        }
        finally
        {
            DialogGate.Release();
        }
    }

    private static bool IsErrorMessage(string message)
        => message.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot", StringComparison.OrdinalIgnoreCase)
            || message.Contains("must ", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unapplied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
}
