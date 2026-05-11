using FluentMigrator.Runner;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Customers;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Adapters;
using KuchniaUCygana.Infrastructure.Cache;
using KuchniaUCygana.Infrastructure.ExternalServices.AI;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;
using KuchniaUCygana.Infrastructure.ExternalServices.Stripe;
using KuchniaUCygana.Infrastructure.FileStorage;
using KuchniaUCygana.Infrastructure.Mocks;
using KuchniaUCygana.Infrastructure.Pdf;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.SqlServer;

namespace KuchniaUCygana.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddSingleton(new OrmLiteConnectionFactory(connectionString, SqlServerDialect.Provider));
        services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
        services.AddScoped<IRepository<User>, BaseRepository<User>>();

        // Module 3 repositories.
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>();
        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IProductionPlanRepository, ProductionPlanRepository>();
        services.AddScoped<IPackingSessionRepository, PackingSessionRepository>();
        services.AddScoped<ITemperatureLogRepository, TemperatureLogRepository>();

        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        if (configuration["OrderProvider"] == "M1")
        {
            services.AddScoped<IOrderDataProvider, M1OrderDataProvider>();
        }
        else
        {
            services.AddScoped<IOrderDataProvider, MockOrderDataProvider>();
        }

        services.AddScoped<IDietDataProvider, DietDataAdapter>();
        services.AddScoped<IDeliveryManifestProvider, MockDeliveryManifestProvider>();

        services.AddScoped<Domain.Services.FefoService>();
        services.AddScoped<Domain.Services.FoodCostCalculator>();
        services.AddScoped<Domain.Services.SmartInventoryAnalyzer>();
        services.AddScoped<Domain.Services.ProductionPlanGenerator>();

        services
            .AddFluentMigratorCore()
            .ConfigureRunner(
                builder => builder
                    .AddSqlServer()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(DependencyInjection).Assembly).For.Migrations())
            .AddLogging(loggingBuilder => loggingBuilder.AddFluentMigratorConsole());

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddScoped<IPaymentService, StripePaymentService>();
        services.AddScoped<IGeocodeService, OpenStreetMapService>();
        services.AddScoped<IAiDescriptionService, OpenAiDescriptionService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IPdfGenerator, QuestPdfGenerator>();
        services.AddHttpClient<IGeocodeService, OpenStreetMapService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.Add("User-Agent", "KuchniaUCygana/1.0");
        });

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderItemRepository, OrderItemRepository>();
        services.AddScoped<IDeliveryCalendarRepository, DeliveryCalendarRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IDiscountCodeRepository, DiscountCodeRepository>();
        services.AddScoped<IDeliveryWindowRepository, DeliveryWindowRepository>();
        services.AddScoped<ICustomerProfileRepository, CustomerProfileRepository>();

        return services;
    }
}
