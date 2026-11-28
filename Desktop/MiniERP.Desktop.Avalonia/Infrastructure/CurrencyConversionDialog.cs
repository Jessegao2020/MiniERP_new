using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>Shared desktop preview. Confirmation only returns a plan; it never writes data.</summary>
internal static class CurrencyConversionDialog
{
    public static async Task<CurrencyConversionPlan?> ShowAsync(Control owner, string source, string target,
        decimal rate, IReadOnlyList<CurrencyConversionLine> lines, IReadOnlyCollection<Article> catalog, bool saved)
    {
        if (TopLevel.GetTopLevel(owner) is not Window parent) return null;
        var dialog = new Window
        {
            Title = $"Change currency: {source} → {target}", Width = 680, Height = 490,
            MinWidth = 450, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var modes = new ComboBox
        {
            ItemsSource = new[] { "Convert existing prices (recommended)", "Reprice using current catalog",
                "Keep numeric prices (changes transaction value)" },
            SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var info = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var preview = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var totals = new TextBlock { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        var acknowledge = new CheckBox { Content = "I understand the transaction value will change.", IsVisible = false };
        var confirm = new Button { Content = "Apply", MinWidth = 90 };
        var cancel = new Button { Content = "Cancel", MinWidth = 90 };
        CurrencyConversionPlan? selected = null;
        void Update()
        {
            var mode = (CurrencyConversionMode)modes.SelectedIndex;
            acknowledge.IsVisible = mode == CurrencyConversionMode.KeepNumericPrices;
            if (!CurrencyConversionService.TryPlan(source, target, rate, mode, lines, catalog, out selected, out var error))
            {
                info.Text = error; preview.Text = string.Empty; totals.Text = string.Empty; confirm.IsEnabled = false;
                return;
            }
            info.Text = mode switch
            {
                CurrencyConversionMode.ConvertExistingPrices => "Preserve negotiated prices using the displayed exchange rate. Unit prices are rounded to two decimals.",
                CurrencyConversionMode.RepriceFromCatalog => "Replace negotiated unit prices with current catalog prices. Every line must have a valid catalog link.",
                _ => "Warning: the same numeric unit prices in another currency change the transaction value."
            };
            preview.Text = string.Join(Environment.NewLine + Environment.NewLine, selected!.Lines.Select((l, index) =>
                $"{index + 1}. {l.Name}  ·  Qty {l.Quantity:0.####}  ·  Discount {l.DiscountPercent:0.####}%{Environment.NewLine}" +
                $"Unit: {source} {l.OldUnitPrice:0.####} → {target} {l.NewUnitPrice:0.####}    |    Total: {l.OldTotal:N2} → {l.NewTotal:N2}"));
            totals.Text = $"Total: {source} {selected.OldTotal:N2} → {target} {selected.NewTotal:N2}";
            confirm.IsEnabled = !acknowledge.IsVisible || acknowledge.IsChecked == true;
        }
        modes.SelectionChanged += (_, _) => { acknowledge.IsChecked = false; Update(); };
        acknowledge.IsCheckedChanged += (_, _) => Update();
        confirm.Click += (_, _) => { if (confirm.IsEnabled && selected is not null) dialog.Close(selected); };
        cancel.Click += (_, _) => dialog.Close(null);

        var panel = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto,Auto") };
        void Add(Control control, int row) { Grid.SetRow(control, row); panel.Children.Add(control); }
        Add(new TextBlock { Text = $"1 USD = {rate:0.####} CNY" +
            (saved ? " · Only modify a saved document if it has not already been issued or agreed." : ""),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) }, 0);
        Add(modes, 1);
        info.Margin = new Thickness(0,10,0,10); Add(info, 2);
        Add(new ScrollViewer { Content = preview, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, 3);
        totals.Margin = new Thickness(0,10,0,5); Add(totals, 4);
        Add(acknowledge, 5);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0,10,0,0) };
        buttons.Children.Add(confirm); buttons.Children.Add(cancel); Add(buttons, 6);
        dialog.Content = panel;
        Update();
        return await dialog.ShowDialog<CurrencyConversionPlan?>(parent);
    }
}
