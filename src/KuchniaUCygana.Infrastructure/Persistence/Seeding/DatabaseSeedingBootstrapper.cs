using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public static class DatabaseSeedingBootstrapper
{
    /// <summary>
    /// Conditionally runs database seeding based on configuration, the current environment, and the provided trigger.
    /// </summary>
    /// <param name="trigger">The event that initiated evaluation of seeding (for example, Startup or Command).</param>
    /// <param name="cancellationToken">A token to cancel the seeding operation.</param>
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
        await seeder.SeedAsync(profile, options.ResetDemoData, cancellationToken);
        logger.LogInformation("Database seeding finished with profile {Profile}.", profile);
    }

    /// <summary>
    /// Determines whether database seeding should run for the given trigger and runtime context.
    /// </summary>
    /// <param name="trigger">The event that requests seeding (e.g., startup or explicit command).</param>
    /// <param name="options">Configuration for database seeding (may be null-properties); used to check enabled state and mode.</param>
    /// <param name="environment">Host environment used to verify development or test environments.</param>
    /// <returns>`true` if the current environment is Development or "Test", seeding is enabled in options, and the configured mode permits the provided trigger; `false` otherwise.</returns>
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

        var profile = ResolveProfile(options.Profile);
        if (trigger == SeedingTrigger.Startup && profile == DatabaseSeedingProfile.VolumeDemo)
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

    /// <summary>
    /// Parse a seeding mode name into a DatabaseSeedingMode value.
    /// </summary>
    /// <param name="mode">The mode name to parse (case-insensitive); may be null or unrecognized.</param>
    /// <returns>The parsed <see cref="DatabaseSeedingMode"/>, or <see cref="DatabaseSeedingMode.Both"/> if <paramref name="mode"/> is null or cannot be parsed.</returns>
    private static DatabaseSeedingMode ResolveMode(string? mode)
    {
        if (Enum.TryParse<DatabaseSeedingMode>(mode, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return DatabaseSeedingMode.Both;
    }

    /// <summary>
    /// Parses the provided profile name into a <see cref="DatabaseSeedingProfile"/> value.
    /// </summary>
    /// <param name="profile">The configured profile name (case-insensitive); may be null or empty.</param>
    /// <returns>The parsed <see cref="DatabaseSeedingProfile"/>, or <see cref="DatabaseSeedingProfile.MinimalRealistic"/> if the input is null, empty, or cannot be parsed.</returns>
    private static DatabaseSeedingProfile ResolveProfile(string? profile)
    {
        if (Enum.TryParse<DatabaseSeedingProfile>(profile, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return DatabaseSeedingProfile.MinimalRealistic;
    }
}
