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
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));

            services.AddScoped<IArticleRepository, ArticleRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IQuotationRepository, QuotationRepository>();
            services.AddScoped<IUserRepository, UserRepository>();

            return services;
        }

        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IArticleService, ArticleService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IUserService, UserService>();

            return services;
        }
    }
}
