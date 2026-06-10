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
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? commandLineArgs = null)
    {
        var options = configuration.GetSection(DatabaseSeedingOptions.SectionName).Get<DatabaseSeedingOptions>()
            ?? new DatabaseSeedingOptions();
        ApplyCommandLineOptions(options, commandLineArgs);

        if (!ShouldRun(trigger, options, environment))
        {
            logger.LogInformation("Database seeding skipped for trigger {Trigger} in environment {EnvironmentName}.", trigger, environment.EnvironmentName);
            return;
        }

        var requestedProfile = ResolveProfile(options.Profile);
        var profile = trigger == SeedingTrigger.Startup && requestedProfile == DatabaseSeedingProfile.VolumeDemo
            ? DatabaseSeedingProfile.MinimalRealistic
            : requestedProfile;

        if (profile != requestedProfile)
        {
            logger.LogInformation(
                "Database seeding profile {RequestedProfile} is command-only; using {EffectiveProfile} for startup.",
                requestedProfile,
                profile);
        }

        using var scope = services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync(profile, options.ResetDemoData, cancellationToken, CreateVolumeDemoConfig(options));
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

    private static VolumeDemoConfig CreateVolumeDemoConfig(DatabaseSeedingOptions options)
    {
        return new VolumeDemoConfig(
            Days: Math.Clamp(options.Days, 1, 45),
            ActiveCustomers: Math.Clamp(options.ActiveCustomers, 1, 1150),
            PeakOrders: Math.Clamp(options.PeakOrders, 1, 2000),
            Seed: options.Seed,
            DryRun: options.DryRun,
            ResetVolumeDemo: options.ResetVolumeDemo);
    }

    private static void ApplyCommandLineOptions(DatabaseSeedingOptions options, IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
        {
            return;
        }

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (arg.Equals("seed", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("db:seed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--profile", out var profile) &&
                !string.IsNullOrWhiteSpace(profile))
            {
                options.Profile = profile;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--days", out var days) && int.TryParse(days, out var parsedDays))
            {
                options.Days = parsedDays;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--active-customers", out var activeCustomers) &&
                int.TryParse(activeCustomers, out var parsedActiveCustomers))
            {
                options.ActiveCustomers = parsedActiveCustomers;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--peak-orders", out var peakOrders) &&
                int.TryParse(peakOrders, out var parsedPeakOrders))
            {
                options.PeakOrders = parsedPeakOrders;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--seed", out var seed) && int.TryParse(seed, out var parsedSeed))
            {
                options.Seed = parsedSeed;
                continue;
            }

            if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options.DryRun = true;
                continue;
            }

            if (arg.Equals("--reset-volume-demo", StringComparison.OrdinalIgnoreCase))
            {
                options.ResetVolumeDemo = true;
                continue;
            }

            if (arg.Equals("--reset-demo-data", StringComparison.OrdinalIgnoreCase))
            {
                options.ResetDemoData = true;
            }
        }
    }

    private static bool TryReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out string? value)
    {
        var arg = args[index];
        value = null;

        if (arg.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(optionName.Length + 1)..];
            return !string.IsNullOrWhiteSpace(value);
        }

        if (!arg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return true;
        }

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value);
    }
}
