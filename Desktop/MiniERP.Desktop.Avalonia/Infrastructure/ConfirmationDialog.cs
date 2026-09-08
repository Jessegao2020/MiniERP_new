using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MiniERP.Desktop.Infrastructure;

internal static class ConfirmationDialog
{
    public static async Task<bool> ShowAsync(Control owner, string title, string message)
    {
        var ownerWindow = TopLevel.GetTopLevel(owner) as Window;
        if (ownerWindow is null)
            return false;

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var yesButton = new Button
        {
            Content = "Delete",
            Width = 90,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        yesButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        buttons.Children.Add(yesButton);
        buttons.Children.Add(cancelButton);

        var content = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        content.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });

        Grid.SetRow(buttons, 1);
        content.Children.Add(buttons);
        dialog.Content = content;

        return await dialog.ShowDialog<bool>(ownerWindow);
    }
}
