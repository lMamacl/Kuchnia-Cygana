using FluentMigrator.Runner;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Cache;
using KuchniaUCygana.Infrastructure.ExternalServices.AI;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;
using KuchniaUCygana.Infrastructure.ExternalServices.Stripe;
using KuchniaUCygana.Infrastructure.FileStorage;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using KuchniaUCygana.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.SqlServer;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.Services.Menu;
using KuchniaUCygana.Infrastructure.Adapters;

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
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        // Serwisy Domenowe Modu³u 2
        services.AddScoped<IRecipeEngine, RecipeEngine>();
        services.AddScoped<IAllergenPropagationService, AllergenPropagationService>();
        services.AddScoped<IDietVariantScaler, DietVariantScaler>();
        services.AddScoped<INutritionCalculator, NutritionCalculator>();
        services.AddScoped<IIngredientDeletionGuard, IngredientDeletionGuard>();
        // Adapters
        services.AddScoped<KuchniaUCygana.Application.Interfaces.Menu.IInternalAiService, InternalAiAdapter>();
        services.AddScoped<KuchniaUCygana.Application.Interfaces.Menu.IInternalFileStorageService, InternalFileStorageAdapter>();
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
