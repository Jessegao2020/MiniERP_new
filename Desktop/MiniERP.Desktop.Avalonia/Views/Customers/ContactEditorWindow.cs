using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Customers;

public sealed partial class ContactEditorWindow : Window
{
    internal sealed record Result(string Title, string Name);

    public ContactEditorWindow(CustomerContact? contact)
    {
        InitializeComponent();

        Title = contact is null ? "New Contact" : "Edit Contact";
        TitleBox.Text = contact?.Title ?? string.Empty;
        NameBox.Text = contact?.Name ?? string.Empty;
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);
        Opened += (_, _) => NameBox.Focus();
    }

    private void TitleChoice_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Header: string title })
            TitleBox.Text = title;
    }

    private void NameBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (OkButton is not null)
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text)) return;
        Close(new Result((TitleBox.Text ?? string.Empty).Trim(), NameBox.Text.Trim()));
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
