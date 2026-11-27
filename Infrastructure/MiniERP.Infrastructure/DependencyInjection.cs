using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Infrastructure.Data;
using MiniERP.Infrastructure.Repositories;

namespace MiniERP.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string? catalogConnectionString = null)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));

            // Legacy WPF callers without a catalog path continue using the original ERP file.
            // Avalonia supplies a separate catalog.db path explicitly.
            services.AddDbContext<CatalogDbContext>(options =>
                options.UseSqlite(catalogConnectionString ?? connectionString));

            services.AddScoped<IArticleRepository, ArticleRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IQuotationRepository, QuotationRepository>();
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IPackingListRepository, PackingListRepository>();
            services.AddScoped<IContractRepository, ContractRepository>();
            services.AddScoped<IUserRepository, UserRepository>();

            return services;
        }

        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IArticleService, ArticleService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IQuotationService, QuotationService>();
            services.AddSingleton(new QuotationPolicy());
            services.AddScoped<IQuotationWorkflow, QuotationWorkflow>();
            services.AddScoped<IInvoiceService, InvoiceService>();
            services.AddSingleton(new InvoicePolicy());
            services.AddScoped<IInvoiceWorkflow, InvoiceWorkflow>();
            services.AddScoped<IPackingListService, PackingListService>();
            services.AddScoped<IContractService, ContractService>();
            services.AddScoped<ISalesDocumentCopyService, SalesDocumentCopyService>();
            services.AddScoped<IUserService, UserService>();

            return services;
        }
    }
}
