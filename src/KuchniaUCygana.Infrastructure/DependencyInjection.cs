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
using KuchniaUCygana.Infrastructure.Persistence.TypeHandlers;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.Services.Menu;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.BackgroundJobs;
using KuchniaUCygana.Application.Services.Logistics;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MenuAppInterfaces = KuchniaUCygana.Application.Interfaces.Menu;
using MenuAppServices = KuchniaUCygana.Application.Services.Menu;

namespace KuchniaUCygana.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        DapperTypeHandlers.Register();

        services.AddSingleton<IDbConnectionFactory>(_ => new SqlServerConnectionFactory(connectionString));
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IRepository<User>, BaseRepository<User>>();

        // Module 3 repositories.
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>();
        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IProductionPlanRepository, ProductionPlanRepository>();
        services.AddScoped<IPackingSessionRepository, PackingSessionRepository>();
        services.AddScoped<ITemperatureLogRepository, TemperatureLogRepository>();

        // Module 2 (Menu) repositories.
        services.AddScoped<IAllergenRepository, AllergenRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IDietRepository, DietRepository>();
        services.AddScoped<IDietVariantRepository, DietVariantRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IMealAllergenRepository, MealAllergenRepository>();
        services.AddScoped<IMealImageRepository, MealImageRepository>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<INutritionFactRepository, NutritionFactRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();

        // Module 4 (Logistics) repositories.
        services.AddScoped<IDeliveryRouteRepository, DeliveryRouteRepository>();
        services.AddScoped<IDeliveryRouteStopRepository, DeliveryRouteStopRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IThermalBagRepository, ThermalBagRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();

        // Module 5 (HR / Admin) repositories.
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IWorkScheduleRepository, WorkScheduleRepository>();

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
        services.AddScoped<IDeliveryManifestProvider, M4DeliveryManifestProvider>();

        services.AddScoped<Domain.Services.FefoService>();
        services.AddScoped<Domain.Services.FoodCostCalculator>();
        services.AddScoped<Domain.Services.SmartInventoryAnalyzer>();
        services.AddScoped<Domain.Services.ProductionPlanGenerator>();

        // Module 3 application services.
        services.AddScoped<IProductionService, Application.Services.ProductionService>();
        services.AddScoped<IWarehouseService, Application.Services.WarehouseService>();
        services.AddScoped<IPackingService, Application.Services.PackingService>();
        services.AddScoped<ITemperatureService, Application.Services.TemperatureService>();

        // Module 2 (Menu) Domain services.
        services.AddScoped<IAllergenPropagationService, AllergenPropagationService>();
        services.AddScoped<IDietVariantScaler, DietVariantScaler>();
        services.AddScoped<IIngredientDeletionGuard, IngredientDeletionGuard>();
        services.AddScoped<INutritionCalculator, NutritionCalculator>();
        services.AddScoped<IRecipeEngine, RecipeEngine>();

        // Module 2 (Menu) Adapters.
        services.AddScoped<MenuAppInterfaces.IInternalAiService, InternalAiAdapter>();
        services.AddScoped<MenuAppInterfaces.IInternalFileStorageService, InternalFileStorageAdapter>();

        // Module 2 (Menu) Application services.
        services.AddScoped<MenuAppInterfaces.IAiDescriptionService, MenuAppServices.AiDescriptionService>();
        services.AddScoped<MenuAppInterfaces.IAllergenManagementService, MenuAppServices.AllergenManagementService>();
        services.AddScoped<MenuAppInterfaces.ICategoryService, MenuAppServices.CategoryService>();
        services.AddScoped<MenuAppInterfaces.IDietManagementService, MenuAppServices.DietManagementService>();
        services.AddScoped<MenuAppInterfaces.IImageManagementService, MenuAppServices.ImageManagementService>();
        services.AddScoped<MenuAppInterfaces.IIngredientManagementService, MenuAppServices.IngredientManagementService>();
        services.AddScoped<MenuAppInterfaces.IMealManagementService, MenuAppServices.MealManagementService>();
        services.AddScoped<MenuAppInterfaces.INutritionService, MenuAppServices.NutritionService>();

        // Module 4 (Logistics) services.
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<GeocodingOrchestrator>();
        services.AddScoped<IRouteOptimizer, GoogleMapsRoutingService>();
        services.AddHostedService<DailyGeocodingWorker>();

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
