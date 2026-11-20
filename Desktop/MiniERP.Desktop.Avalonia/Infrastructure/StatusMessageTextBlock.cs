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
        // Only invalid operations, validation failures and errors should block the
        // user. Ordinary feedback ("Position added", "Saved", "PDF exported",
        // "Item moved", etc.) remains non-modal.
        //
        // Use an explicit error classifier instead of treating every Status
        // change as a notification. New status messages default to non-modal.
        var value = message.Trim();

        if (value.StartsWith("Please select", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Select a ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Select an ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Set the ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Please set ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Remove ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Save or discard ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Complete or remove ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Enter a ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Enter an ", StringComparison.OrdinalIgnoreCase))
            return true;

        return value.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || value.Contains("required", StringComparison.OrdinalIgnoreCase)
            || value.Contains("cannot", StringComparison.OrdinalIgnoreCase)
            || value.Contains("can't ", StringComparison.OrdinalIgnoreCase)
            || value.Contains("must ", StringComparison.OrdinalIgnoreCase)
            || value.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unapplied changes", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unsaved changes", StringComparison.OrdinalIgnoreCase)
            || value.Contains("already exists", StringComparison.OrdinalIgnoreCase)
            || value.Contains("already first", StringComparison.OrdinalIgnoreCase)
            || value.Contains("already last", StringComparison.OrdinalIgnoreCase)
            || value.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || value.Contains("does not have", StringComparison.OrdinalIgnoreCase)
            || value.Contains("no longer exists", StringComparison.OrdinalIgnoreCase)
            || value.Contains("not configured", StringComparison.OrdinalIgnoreCase)
            || value.Contains("not loaded", StringComparison.OrdinalIgnoreCase)
            || value.Contains("not linked", StringComparison.OrdinalIgnoreCase)
            || value.Contains("not available", StringComparison.OrdinalIgnoreCase)
            || value.Contains("not supported", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            || value.Contains("greater than", StringComparison.OrdinalIgnoreCase)
            || value.Contains("less than", StringComparison.OrdinalIgnoreCase)
            || value.Contains("between 0 and", StringComparison.OrdinalIgnoreCase)
            || value.Contains("between 1 and", StringComparison.OrdinalIgnoreCase)
            || value.Contains("cannot be", StringComparison.OrdinalIgnoreCase);
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
                Title = "MiniERP - Attention",
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

}
