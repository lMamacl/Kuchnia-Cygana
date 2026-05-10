using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public static class DatabaseSeedingBootstrapper
{
    public static async Task TrySeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        SeedingTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        var options = configuration.GetSection(DatabaseSeedingOptions.SectionName).Get<DatabaseSeedingOptions>()
            ?? new DatabaseSeedingOptions();

        if (!ShouldRun(trigger, options, environment))
        {
            logger.LogInformation("Database seeding skipped for trigger {Trigger} in environment {EnvironmentName}.", trigger, environment.EnvironmentName);
            return;
        }

        var profile = ResolveProfile(options.Profile);
        using var scope = services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync(profile, cancellationToken);
        logger.LogInformation("Database seeding finished with profile {Profile}.", profile);
    }

    private static bool ShouldRun(SeedingTrigger trigger, DatabaseSeedingOptions options, IHostEnvironment environment)
    {
        var isDevOrTest = environment.IsDevelopment() || environment.IsEnvironment("Test");
        if (!isDevOrTest)
        {
            return false;
        }

        var enabled = options.Enabled ?? true;
        if (!enabled)
        {
            return false;
        }

        var mode = ResolveMode(options.Mode);

        return trigger switch
        {
            SeedingTrigger.Startup => mode is DatabaseSeedingMode.Startup or DatabaseSeedingMode.Both,
            SeedingTrigger.Command => mode is DatabaseSeedingMode.Command or DatabaseSeedingMode.Both,
            _ => false,
        };
    }

    private static DatabaseSeedingMode ResolveMode(string? mode)
    {
        if (Enum.TryParse<DatabaseSeedingMode>(mode, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return DatabaseSeedingMode.Both;
    }

    private static DatabaseSeedingProfile ResolveProfile(string? profile)
    {
        if (Enum.TryParse<DatabaseSeedingProfile>(profile, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return DatabaseSeedingProfile.MinimalRealistic;
    }
}
