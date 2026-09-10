using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Desktop.Views.Articles;
using MiniERP.Desktop.Views.Contracts;
using MiniERP.Desktop.Views.Customers;
using MiniERP.Desktop.Views.Invoices;
using MiniERP.Desktop.Views.PackingLists;
using MiniERP.Desktop.Views.Quotations;
using MiniERP.Desktop.Views.Settings;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, TabItem> _openTabs = new();
    private readonly ObservableCollection<TabItem> _workspaceTabs = new();

    public MainWindow()
    {
        InitializeComponent();
        ContentTabControl.ItemsSource = _workspaceTabs;
    }

    private void Article_Click(object? sender, RoutedEventArgs e) => OpenArticleList();
    private void Customer_Click(object? sender, RoutedEventArgs e) => OpenCustomerList();
    private void Quotation_Click(object? sender, RoutedEventArgs e) => OpenQuotationList();
    private void Invoice_Click(object? sender, RoutedEventArgs e) => OpenInvoiceList(InvoiceType.Commercial);
    private void PackingList_Click(object? sender, RoutedEventArgs e) => OpenPackingList();
    private void ProformaInvoice_Click(object? sender, RoutedEventArgs e) => OpenInvoiceList(InvoiceType.Proforma);
    private void Contract_Click(object? sender, RoutedEventArgs e) => OpenContractList();
    private void System_Click(object? sender, RoutedEventArgs e) => OpenSystemSettings();
    private void User_Click(object? sender, RoutedEventArgs e) => OpenUserSettings();

    private void OpenArticleList()
    {
        const string key = "article";
        if (SelectExisting(key)) return;
        AddWorkspace(key, "Article", new ArticleWorkspaceView());
    }

    private void OpenCustomerList()
    {
        const string key = "customer";
        if (SelectExisting(key)) return;
        AddWorkspace(key, "Customer", new CustomerWorkspaceView());
    }

    private void OpenCustomerEditor(Customer? customer)
    {
        const string key = "customer";
        var workspace = GetOrCreateCustomerWorkspace();
        workspace.ShowEditor(customer);
        SelectExisting(key);
    }

    private CustomerWorkspaceView GetOrCreateCustomerWorkspace()
    {
        const string key = "customer";
        if (_openTabs.TryGetValue(key, out var existing) && existing.Content is CustomerWorkspaceView workspace)
            return workspace;

        var created = new CustomerWorkspaceView();
        AddWorkspace(key, "Customer", created);
        return created;
    }

    private void OpenQuotationList()
    {
        const string key = "quotation";
        if (SelectExisting(key)) return;
        AddWorkspace(key, "Quotation", CreateQuotationWorkspace());
    }

    private void OpenQuotationEditor(Quotation? quotation)
    {
        const string key = "quotation";
        var workspace = GetOrCreateQuotationWorkspace();
        workspace.ShowEditor(quotation);
        SelectExisting(key);
    }

    private QuotationWorkspaceView CreateQuotationWorkspace()
    {
        var workspace = new QuotationWorkspaceView();
        workspace.CreateInvoiceRequested += async (source, type) => await CreateInvoiceFromQuotationAsync(source.Id, type);
        workspace.CreatePackingListRequested += async source => await CreatePackingListFromQuotationAsync(source.Id);
        workspace.CreateContractRequested += async source => await CreateContractFromQuotationAsync(source.Id);
        return workspace;
    }

    private QuotationWorkspaceView GetOrCreateQuotationWorkspace()
    {
        const string key = "quotation";
        if (_openTabs.TryGetValue(key, out var existing) && existing.Content is QuotationWorkspaceView workspace)
            return workspace;

        var created = CreateQuotationWorkspace();
        AddWorkspace(key, "Quotation", created);
        return created;
    }

    private void OpenInvoiceList(InvoiceType type)
    {
        var key = InvoiceWorkspaceKey(type);
        var title = type == InvoiceType.Proforma ? "P/I" : "Invoice";
        if (SelectExisting(key)) return;
        AddWorkspace(key, title, CreateInvoiceWorkspace(type));
    }

    private void OpenInvoiceEditor(Invoice? invoice, InvoiceType type)
    {
        var key = InvoiceWorkspaceKey(type);
        var workspace = GetOrCreateInvoiceWorkspace(type);
        workspace.ShowEditor(invoice);
        SelectExisting(key);
    }

    private InvoiceWorkspaceView CreateInvoiceWorkspace(InvoiceType type)
    {
        var workspace = new InvoiceWorkspaceView(type);
        workspace.CreateCommercialRequested += async source => await CreateInvoiceFromInvoiceAsync(source.Id, InvoiceType.Commercial);
        workspace.CreatePackingListRequested += async source => await CreatePackingListFromInvoiceAsync(source.Id);
        workspace.CreateContractRequested += async source => await CreateContractFromInvoiceAsync(source.Id);
        return workspace;
    }

    private InvoiceWorkspaceView GetOrCreateInvoiceWorkspace(InvoiceType type)
    {
        var key = InvoiceWorkspaceKey(type);
        if (_openTabs.TryGetValue(key, out var existing) && existing.Content is InvoiceWorkspaceView workspace)
            return workspace;

        var created = CreateInvoiceWorkspace(type);
        AddWorkspace(key, type == InvoiceType.Proforma ? "P/I" : "Invoice", created);
        return created;
    }

    private static string InvoiceWorkspaceKey(InvoiceType type)
        => type == InvoiceType.Proforma ? "invoice:proforma" : "invoice:commercial";

    private void OpenPackingList()
    {
        const string key = "packing-list";
        if (SelectExisting(key)) return;
        AddWorkspace(key, "P/L", new PackingListWorkspaceView());
    }

    private void OpenPackingListEditor(PackingList? packingList)
    {
        const string key = "packing-list";
        var workspace = GetOrCreatePackingListWorkspace();
        workspace.ShowEditor(packingList);
        SelectExisting(key);
    }

    private PackingListWorkspaceView GetOrCreatePackingListWorkspace()
    {
        const string key = "packing-list";
        if (_openTabs.TryGetValue(key, out var existing) && existing.Content is PackingListWorkspaceView workspace)
            return workspace;

        var created = new PackingListWorkspaceView();
        AddWorkspace(key, "P/L", created);
        return created;
    }

    private void OpenContractList()
    {
        const string key = "contract";
        if (SelectExisting(key)) return;
        AddWorkspace(key, "Contract", CreateContractWorkspace());
    }

    private void OpenContractEditor(Contract? contract)
    {
        const string key = "contract";
        var workspace = GetOrCreateContractWorkspace();
        workspace.ShowEditor(contract);
        SelectExisting(key);
    }

    private ContractWorkspaceView CreateContractWorkspace()
    {
        var workspace = new ContractWorkspaceView();
        workspace.CreateInvoiceRequested += async (source, type) => await CreateInvoiceFromContractAsync(source.Id, type);
        workspace.CreatePackingListRequested += async source => await CreatePackingListFromContractAsync(source.Id);
        return workspace;
    }

    private ContractWorkspaceView GetOrCreateContractWorkspace()
    {
        const string key = "contract";
        if (_openTabs.TryGetValue(key, out var existing) && existing.Content is ContractWorkspaceView workspace)
            return workspace;

        var created = CreateContractWorkspace();
        AddWorkspace(key, "Contract", created);
        return created;
    }

    private async Task CreateInvoiceFromQuotationAsync(int quotationId, InvoiceType type)
    {
        if (quotationId <= 0) return;
        using var scope = App.Services.CreateScope();
        var copy = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        OpenInvoiceEditor(await copy.CreateInvoiceFromQuotationAsync(quotationId, type), type);
    }

    private async Task CreatePackingListFromQuotationAsync(int quotationId)
    {
        if (quotationId <= 0) return;
        using var scope = App.Services.CreateScope();
        var copy = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        OpenPackingListEditor(await copy.CreatePackingListFromQuotationAsync(quotationId));
    }

    private async Task CreateContractFromQuotationAsync(int quotationId)
    {
        if (quotationId <= 0) return;
        using var scope = App.Services.CreateScope();
        var copy = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        OpenContractEditor(await copy.CreateContractFromQuotationAsync(quotationId));
    }

    private async Task CreateInvoiceFromInvoiceAsync(int invoiceId, InvoiceType targetType)
    {
        if (invoiceId <= 0) return;
        using var scope = App.Services.CreateScope();
        var copy = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        OpenInvoiceEditor(await copy.CreateInvoiceFromInvoiceAsync(invoiceId, targetType), targetType);
    }

    private async Task CreatePackingListFromInvoiceAsync(int invoiceId)
    {
        if (invoiceId <= 0) return;
        using var scope = App.Services.CreateScope();
        var copy = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        OpenPackingListEditor(await copy.CreatePackingListFromInvoiceAsync(invoiceId));
    }

    private async Task CreateContractFromInvoiceAsync(int invoiceId)
    {
        if (invoiceId <= 0) return;
        using var scope = App.Services.CreateScope();
        var copy = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        OpenContractEditor(await copy.CreateContractFromInvoiceAsync(invoiceId));
    }

    private async Task CreateInvoiceFromContractAsync(int contractId, InvoiceType targetType)
    {
        if (contractId <= 0) return;
        using var scope = App.Services.CreateScope();
        var copy = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        OpenInvoiceEditor(await copy.CreateInvoiceFromContractAsync(contractId, targetType), targetType);
    }

    private async Task CreatePackingListFromContractAsync(int contractId)
    {
        if (contractId <= 0) return;
        using var scope = App.Services.CreateScope();
        var copy = scope.ServiceProvider.GetRequiredService<ISalesDocumentCopyService>();
        OpenPackingListEditor(await copy.CreatePackingListFromContractAsync(contractId));
    }

    private void OpenSystemSettings()
    {
        const string key = "system";
        if (SelectExisting(key)) return;
        var view = new SystemSettingsView();
        view.Saved += (_, _) => RefreshOpenExchangeRates();
        AddWorkspace(key, "System", view);
    }

    private void OpenUserSettings()
    {
        const string key = "user";
        if (SelectExisting(key)) return;
        AddWorkspace(key, "User", new UserSettingsView());
    }

    private void RefreshOpenExchangeRates()
    {
        foreach (var tab in _workspaceTabs)
        {
            if (tab.Content is ArticleWorkspaceView articleWorkspace) articleWorkspace.RefreshExchangeRate();
            else if (tab.Content is QuotationWorkspaceView quotationWorkspace) quotationWorkspace.RefreshExchangeRate();
            else if (tab.Content is ContractWorkspaceView contractWorkspace) contractWorkspace.RefreshExchangeRate();
        }
    }

    private async Task RefreshArticleListAsync()
    {
        if (_openTabs.TryGetValue("article", out var tab) && tab.Content is ArticleWorkspaceView workspace)
            await workspace.ReloadListAsync();
    }

    private async Task RefreshCustomerListAsync()
    {
        if (_openTabs.TryGetValue("customer", out var tab) && tab.Content is CustomerWorkspaceView workspace)
            await workspace.ReloadListAsync();
    }

    private async Task RefreshQuotationListAsync()
    {
        if (_openTabs.TryGetValue("quotation", out var tab) && tab.Content is QuotationWorkspaceView workspace)
            await workspace.ReloadListAsync();
    }

    private async Task RefreshInvoiceListAsync(InvoiceType type)
    {
        var key = InvoiceWorkspaceKey(type);
        if (_openTabs.TryGetValue(key, out var tab) && tab.Content is InvoiceWorkspaceView workspace)
            await workspace.ReloadListAsync();
    }

    private async Task RefreshPackingListAsync()
    {
        if (_openTabs.TryGetValue("packing-list", out var tab) && tab.Content is PackingListWorkspaceView workspace)
            await workspace.ReloadListAsync();
    }

    private async Task RefreshContractListAsync()
    {
        if (_openTabs.TryGetValue("contract", out var tab) && tab.Content is ContractWorkspaceView workspace)
            await workspace.ReloadListAsync();
    }

    private bool SelectExisting(string key)
    {
        if (!_openTabs.TryGetValue(key, out var existing)) return false;
        ContentTabControl.SelectedItem = existing;
        return true;
    }

    private TabItem AddWorkspace(string key, string title, Control content)
    {
        var closeButton = new Button { Content = "×", Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(3, 0), MinWidth = 18, MinHeight = 18, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 11, FontWeight = FontWeight.Bold };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(closeButton);
        var tab = new TabItem { Header = header, Content = content };
        closeButton.Click += (_, _) => CloseWorkspace(key, tab);
        _openTabs[key] = tab;
        _workspaceTabs.Add(tab);
        ContentTabControl.SelectedItem = tab;
        return tab;
    }

    private void CloseWorkspace(string key, TabItem tab)
    {
        _workspaceTabs.Remove(tab);
        _openTabs.Remove(key);
    }
}
