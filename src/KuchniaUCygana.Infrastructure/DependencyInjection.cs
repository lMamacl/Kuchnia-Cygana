using FluentMigrator.Runner;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Cache;
using KuchniaUCygana.Infrastructure.ExternalServices.AI;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;
using KuchniaUCygana.Infrastructure.ExternalServices.Stripe;
using KuchniaUCygana.Infrastructure.FileStorage;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using KuchniaUCygana.Infrastructure.Pdf;
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
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>();
        services.AddScoped<Domain.Interfaces.Warehouse.IStockItemRepository, Persistence.Repositories.Warehouse.StockItemRepository>();
        services.AddScoped<Domain.Interfaces.Production.IProductionPlanRepository, Persistence.Repositories.Production.ProductionPlanRepository>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        // === Moduł 3: External Providers ===
        // Mock M1 (zamówienia — moduł niedostępny)
        services.AddScoped<Domain.Interfaces.External.IOrderDataProvider, Mocks.MockOrderDataProvider>();
        // Prawdziwy adapter M2 (diety/receptury — moduł dostępny w Domain/Entities/Menu)
        services.AddScoped<Domain.Interfaces.External.IDietDataProvider, Adapters.DietDataAdapter>();
        // Mock M4 (logistyka — moduł niedostępny)
        services.AddScoped<Domain.Interfaces.External.IDeliveryManifestProvider, Mocks.MockDeliveryManifestProvider>();

        // === Moduł 3: Domain Services ===
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

        return services;
    }
}
