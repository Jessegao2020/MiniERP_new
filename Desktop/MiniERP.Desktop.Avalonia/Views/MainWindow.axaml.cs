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
        var view = new ArticleListView();
        view.OpenArticleRequested += OpenArticleEditor;
        AddWorkspace(key, "Article", view);
    }

    private void OpenArticleEditor(Article? article)
    {
        var key = article is null ? "article:new" : $"article:{article.Id}";
        var title = article is null ? "New Article" : "Article Details";
        if (SelectExisting(key)) return;
        var editor = new ArticleEditorView(article);
        var tab = AddWorkspace(key, title, editor);
        editor.Saved += async (_, _) => await RefreshArticleListAsync();
        editor.Deleted += async (_, _) => await RefreshArticleListAsync();
        editor.RequestClose += (_, _) => CloseWorkspace(key, tab);
    }

    private void OpenCustomerList()
    {
        const string key = "customer";
        if (SelectExisting(key)) return;
        var view = new CustomerListView();
        view.OpenCustomerRequested += OpenCustomerEditor;
        AddWorkspace(key, "Customer", view);
    }

    private void OpenCustomerEditor(Customer? customer)
    {
        var key = customer is null ? "customer:new" : $"customer:{customer.Id}";
        var title = customer is null ? "New Customer" : $"Customer Details: {customer.Name}";
        if (SelectExisting(key)) return;
        var editor = new CustomerEditorView(customer);
        var tab = AddWorkspace(key, title, editor);
        editor.Saved += async (_, _) => await RefreshCustomerListAsync();
        editor.Deleted += async (_, _) => await RefreshCustomerListAsync();
        editor.RequestClose += (_, _) => CloseWorkspace(key, tab);
    }

    private void OpenQuotationList()
    {
        const string key = "quotation";
        if (SelectExisting(key)) return;
        var view = new QuotationListView();
        view.OpenQuotationRequested += OpenQuotationEditor;
        AddWorkspace(key, "Quotation", view);
    }

    private void OpenQuotationEditor(Quotation? quotation)
    {
        var key = quotation is null ? "quotation:new" : $"quotation:{quotation.Id}";
        var title = quotation is null ? "New Quotation" : $"Quotation: {quotation.QuotationNumber}";
        if (SelectExisting(key)) return;
        var editor = new QuotationEditorView(quotation);
        var tab = AddWorkspace(key, title, editor);
        editor.Saved += async (_, _) => await RefreshQuotationListAsync();
        editor.Deleted += async (_, _) => await RefreshQuotationListAsync();
        editor.CreateInvoiceRequested += async (source, type) => await CreateInvoiceFromQuotationAsync(source.Id, type);
        editor.CreatePackingListRequested += async source => await CreatePackingListFromQuotationAsync(source.Id);
        editor.CreateContractRequested += async source => await CreateContractFromQuotationAsync(source.Id);
        editor.RequestClose += (_, _) => CloseWorkspace(key, tab);
    }

    private void OpenInvoiceList(InvoiceType type)
    {
        var key = type == InvoiceType.Proforma ? "invoice:proforma" : "invoice:commercial";
        var title = type == InvoiceType.Proforma ? "P/I" : "Invoice";
        if (SelectExisting(key)) return;
        var view = new InvoiceListView(type);
        view.OpenInvoiceRequested += invoice => OpenInvoiceEditor(invoice, type);
        AddWorkspace(key, title, view);
    }

    private void OpenInvoiceEditor(Invoice? invoice, InvoiceType type)
    {
        var prefix = type == InvoiceType.Proforma ? "pi" : "invoice";
        var key = invoice is null ? $"{prefix}:new" : invoice.Id > 0 ? $"{prefix}:{invoice.Id}" : $"{prefix}:draft:{invoice.InvoiceNumber}";
        var title = invoice is null ? $"New {(type == InvoiceType.Proforma ? "P/I" : "Invoice")}" : $"{(type == InvoiceType.Proforma ? "P/I" : "Invoice")}: {invoice.InvoiceNumber}";
        if (SelectExisting(key)) return;
        var editor = new InvoiceEditorView(invoice, type);
        var tab = AddWorkspace(key, title, editor);
        editor.Saved += async (_, _) => await RefreshInvoiceListAsync(type);
        editor.Deleted += async (_, _) => await RefreshInvoiceListAsync(type);
        editor.CreateCommercialRequested += async source => await CreateInvoiceFromInvoiceAsync(source.Id, InvoiceType.Commercial);
        editor.CreatePackingListRequested += async source => await CreatePackingListFromInvoiceAsync(source.Id);
        editor.CreateContractRequested += async source => await CreateContractFromInvoiceAsync(source.Id);
        editor.RequestClose += (_, _) => CloseWorkspace(key, tab);
    }

    private void OpenPackingList()
    {
        const string key = "packing-list";
        if (SelectExisting(key)) return;
        var view = new PackingListListView();
        view.OpenPackingListRequested += OpenPackingListEditor;
        AddWorkspace(key, "P/L", view);
    }

    private void OpenPackingListEditor(PackingList? packingList)
    {
        var key = packingList is null ? "packing-list:new" : packingList.Id > 0 ? $"packing-list:{packingList.Id}" : $"packing-list:draft:{packingList.PackingListNumber}";
        var title = packingList is null ? "New Packing List" : $"P/L: {packingList.PackingListNumber}";
        if (SelectExisting(key)) return;
        var editor = new PackingListEditorView(packingList);
        var tab = AddWorkspace(key, title, editor);
        editor.Saved += async (_, _) => await RefreshPackingListAsync();
        editor.Deleted += async (_, _) => await RefreshPackingListAsync();
        editor.RequestClose += (_, _) => CloseWorkspace(key, tab);
    }

    private void OpenContractList()
    {
        const string key = "contract";
        if (SelectExisting(key)) return;
        var view = new ContractListView();
        view.OpenContractRequested += OpenContractEditor;
        AddWorkspace(key, "Contract", view);
    }

    private void OpenContractEditor(Contract? contract)
    {
        var key = contract is null ? "contract:new" : contract.Id > 0 ? $"contract:{contract.Id}" : $"contract:draft:{contract.ContractNumber}";
        var title = contract is null ? "New Contract" : $"Contract: {contract.ContractNumber}";
        if (SelectExisting(key)) return;
        var editor = new ContractEditorView(contract);
        var tab = AddWorkspace(key, title, editor);
        editor.Saved += async (_, _) => await RefreshContractListAsync();
        editor.Deleted += async (_, _) => await RefreshContractListAsync();
        editor.CreateInvoiceRequested += async (source, type) => await CreateInvoiceFromContractAsync(source.Id, type);
        editor.CreatePackingListRequested += async source => await CreatePackingListFromContractAsync(source.Id);
        editor.RequestClose += (_, _) => CloseWorkspace(key, tab);
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
            if (tab.Content is ArticleEditorView articleEditor) articleEditor.RefreshExchangeRate();
            else if (tab.Content is QuotationEditorView quotationEditor) quotationEditor.RefreshExchangeRate();
            else if (tab.Content is ContractEditorView contractEditor) contractEditor.RefreshExchangeRate();
        }
    }

    private async Task RefreshArticleListAsync()
    {
        if (_openTabs.TryGetValue("article", out var tab) && tab.Content is ArticleListView list) await list.ReloadAsync();
    }

    private async Task RefreshCustomerListAsync()
    {
        if (_openTabs.TryGetValue("customer", out var tab) && tab.Content is CustomerListView list) await list.ReloadAsync();
    }

    private async Task RefreshQuotationListAsync()
    {
        if (_openTabs.TryGetValue("quotation", out var tab) && tab.Content is QuotationListView list) await list.ReloadAsync();
    }

    private async Task RefreshInvoiceListAsync(InvoiceType type)
    {
        var key = type == InvoiceType.Proforma ? "invoice:proforma" : "invoice:commercial";
        if (_openTabs.TryGetValue(key, out var tab) && tab.Content is InvoiceListView list) await list.ReloadAsync();
    }

    private async Task RefreshPackingListAsync()
    {
        if (_openTabs.TryGetValue("packing-list", out var tab) && tab.Content is PackingListListView list) await list.ReloadAsync();
    }

    private async Task RefreshContractListAsync()
    {
        if (_openTabs.TryGetValue("contract", out var tab) && tab.Content is ContractListView list) await list.ReloadAsync();
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
