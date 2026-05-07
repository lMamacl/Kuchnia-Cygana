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
using KuchniaUCygana.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KuchniaUCygana.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddScoped<IRepository<User>, BaseRepository<User>>();

        services
            .AddFluentMigratorCore()
            .ConfigureRunner(
                builder => builder
                    .AddSQLite()
                    .WithGlobalConnectionString(configuration.GetConnectionString("DefaultConnection"))
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
