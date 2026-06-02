using FluentMigrator.Runner;
using KuchniaUCygana.Application.Configuration;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Customers;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Adapters;
using KuchniaUCygana.Infrastructure.Cache;
using KuchniaUCygana.Infrastructure.Configuration;
using KuchniaUCygana.Infrastructure.ExternalServices.AI;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;
using KuchniaUCygana.Infrastructure.ExternalServices.Stripe;
using KuchniaUCygana.Infrastructure.FileStorage;
using KuchniaUCygana.Infrastructure.Mocks;
using KuchniaUCygana.Infrastructure.Pdf;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Providers;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.Providers;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using KuchniaUCygana.Infrastructure.Persistence.TypeHandlers;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.Services.Menu;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Auth;
using KuchniaUCygana.Infrastructure.BackgroundJobs;
using KuchniaUCygana.Application.Services.Logistics;
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
        var migrationConnectionString = configuration.GetConnectionString("MigrationConnection")
            ?? connectionString;

        DapperTypeHandlers.Register();

        services.Configure<PackingResourcesOptions>(configuration.GetSection("PackingResources"));

        services.AddSingleton<IDbConnectionFactory>(_ => new SqlServerConnectionFactory(connectionString));
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IRepository<User>, BaseRepository<User>>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Module 3 repositories.
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IWarehouseCommandRepository, WarehouseCommandRepository>();
        services.AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>();
        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IWarehouseCategoryRepository, WarehouseCategoryRepository>();
        services.AddScoped<IHaccpLocationRepository, HaccpLocationRepository>();
        services.AddScoped<IHaccpTemperatureAlertRepository, HaccpTemperatureAlertRepository>();
        services.AddScoped<IProductionPlanRepository, ProductionPlanRepository>();
        services.AddScoped<IPackingSessionRepository, PackingSessionRepository>();
        services.AddScoped<IPackingBagRepository, PackingBagRepository>();
        services.AddScoped<IPackingLabelRepository, PackingLabelRepository>();
        services.AddScoped<IRepository<PackingLabel>>(sp => sp.GetRequiredService<IPackingLabelRepository>());
        services.AddScoped<IPackingManifestRepository, PackingManifestRepository>();
        services.AddScoped<IRepository<PackingManifest>>(sp => sp.GetRequiredService<IPackingManifestRepository>());
        services.AddScoped<IPackingManifestIssueRepository, PackingManifestIssueRepository>();
        services.AddScoped<IRepository<PackingManifestIssue>>(sp => sp.GetRequiredService<IPackingManifestIssueRepository>());
        services.AddScoped<IPackingIncidentRepository, PackingIncidentRepository>();
        services.AddScoped<IBoxLabelRepository, BoxLabelRepository>();
        services.AddScoped<ITemperatureLogRepository, TemperatureLogRepository>();
        services.AddScoped<IBatchExpiryChangeLogRepository, BatchExpiryChangeLogRepository>();
        services.AddScoped<IPackingStatusLogRepository, PackingStatusLogRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Module 2 (Menu) repositories.
        services.AddScoped<IAllergenRepository, AllergenRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IDietRepository, DietRepository>();
        services.AddScoped<IDietVariantRepository, DietVariantRepository>();
        services.AddScoped<IDietVariantMealRepository, DietVariantMealRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IMealAllergenRepository, MealAllergenRepository>();
        services.AddScoped<IMealImageRepository, MealImageRepository>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<INutritionFactRepository, NutritionFactRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IRecipeComponentRepository, RecipeComponentRepository>();
        services.AddScoped<IDietMenuPlanRepository, DietMenuPlanRepository>();

        // Module 4 (Logistics) repositories.
        services.AddScoped<IDeliveryRouteRepository, DeliveryRouteRepository>();
        services.AddScoped<IDeliveryRouteStopRepository, DeliveryRouteStopRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IDriverVehicleAssignmentRepository, DriverVehicleAssignmentRepository>();
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
        if (string.Equals(configuration["DeliveryManifestProvider"], "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IDeliveryManifestProvider, MockDeliveryManifestProvider>();
        }
        else
        {
            services.AddScoped<IDeliveryManifestProvider, M4DeliveryManifestProvider>();
        }

        services.AddScoped<ILogisticsDeliveryDataProvider, LogisticsDeliveryDataProvider>();

        services.AddScoped<Domain.Services.FefoService>();
        services.AddScoped<Domain.Services.FoodCostCalculator>();
        services.AddScoped<Domain.Services.SmartInventoryAnalyzer>();
        services.AddScoped<Domain.Services.ProductionPlanGenerator>();

        // Module 3 application services.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IApplicationUrlProvider, ConfigurationApplicationUrlProvider>();
        services.AddScoped<IProductionService, Application.Services.ProductionService>();
        services.AddScoped<IWarehouseService, Application.Services.WarehouseService>();
        services.AddScoped<IWarehouseCategoryService, Application.Services.WarehouseCategoryService>();
        services.AddScoped<IHaccpLocationService, Application.Services.HaccpLocationService>();
        services.AddScoped<INotificationService, Application.Services.NotificationService>();
        services.AddScoped<IPackingService, Application.Services.PackingService>();
        services.AddScoped<IPackingSynchronizationService, Application.Services.PackingSynchronizationService>();
        services.AddScoped<IPackingBagService, Application.Services.PackingBagService>();
        services.AddScoped<IPackingIncidentService, Application.Services.PackingIncidentService>();
        services.AddScoped<IBoxLabelService, Application.Services.BoxLabelService>();
        services.AddScoped<ICookingSessionService, Application.Services.CookingSessionService>();
        services.AddScoped<ILoadingService, Application.Services.LoadingService>();
        services.AddScoped<IManifestService>(sp => (Application.Services.LoadingService)sp.GetRequiredService<ILoadingService>());
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
        services.AddScoped<MenuAppInterfaces.IRecipeComponentManagementService, MenuAppServices.RecipeComponentManagementService>();
        services.AddScoped<MenuAppInterfaces.IDietMenuPlanManagementService, MenuAppServices.DietMenuPlanManagementService>();

        // Module 4 (Logistics) services.
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IDeliveryRouteService, RoutingService>();
        services.AddScoped<GeocodingOrchestrator>();
        services.AddScoped<IRouteOptimizer, NearestNeighborRouteOptimizer>();
        services.AddHostedService<DailyGeocodingWorker>();

        services
            .AddFluentMigratorCore()
            .ConfigureRunner(
                builder => builder
                    .AddSqlServer()
                    .WithGlobalConnectionString(migrationConnectionString)
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
        services.AddScoped<IDeliveryRouteRepository, DeliveryRouteRepository>();
        services.AddScoped<IDeliveryRouteStopRepository, DeliveryRouteStopRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IThermalBagRepository, ThermalBagRepository>();

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
