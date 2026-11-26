using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Customers;

internal sealed class ContactEditorWindow : Window
{
    internal sealed record Result(string Title, string Name);

    public ContactEditorWindow(CustomerContact? contact)
    {
        Title = contact is null ? "New Contact" : "Edit Contact";
        Width = 450;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Avalonia's standard ComboBox is selection-only. A text box with
        // a suggestion flyout provides an editable title and explicit dropdown.
        var titleBox = new TextBox
        {
            Text = contact?.Title ?? string.Empty,
            MinWidth = 0,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var choices = new StackPanel { MinWidth = 100, Spacing = 2 };
        var flyout = new Flyout { Content = choices };
        foreach (var title in new[] { "Mr.", "Ms.", "Mrs." })
        {
            var choice = new Button
            {
                Content = title,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            choice.Click += (_, _) =>
            {
                titleBox.Text = title;
                flyout.Hide();
            };
            choices.Children.Add(choice);
        }

        var dropButton = new Button
        {
            Content = "▾",
            Flyout = flyout,
            MinWidth = 28,
            Padding = new Thickness(4, 0),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        ToolTip.SetTip(dropButton, "Select title");
        var titleInput = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        titleInput.Children.Add(titleBox);
        Grid.SetColumn(dropButton, 1);
        titleInput.Children.Add(dropButton);

        var nameBox = new TextBox
        {
            Text = contact?.Name ?? string.Empty,
            MinWidth = 0,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var fields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 4
        };
        fields.Children.Add(new TextBlock { Text = "Title" });
        var nameLabel = new TextBlock { Text = "Name" };
        Grid.SetColumn(nameLabel, 1);
        fields.Children.Add(nameLabel);
        Grid.SetRow(titleInput, 1);
        fields.Children.Add(titleInput);
        Grid.SetRow(nameBox, 1);
        Grid.SetColumn(nameBox, 1);
        fields.Children.Add(nameBox);

        var okButton = new Button
        {
            Content = "OK",
            Width = 85,
            IsDefault = true,
            IsEnabled = !string.IsNullOrWhiteSpace(nameBox.Text),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        nameBox.TextChanged += (_, _) =>
            okButton.IsEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) return;
            Close(new Result((titleBox.Text ?? string.Empty).Trim(), nameBox.Text.Trim()));
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 85,
            IsCancel = true,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        cancelButton.Click += (_, _) => Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);

        var content = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        content.Children.Add(fields);
        content.Children.Add(buttons);
        Content = content;
        Opened += (_, _) => nameBox.Focus();
    }
}
