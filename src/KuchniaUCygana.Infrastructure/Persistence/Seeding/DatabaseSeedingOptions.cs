namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeedingOptions
{
    public const string SectionName = "DatabaseSeeding";

    public bool? Enabled { get; set; }

    public string? Mode { get; set; }

    public string? Profile { get; set; }

    public bool ResetDemoData { get; set; }

    public bool DryRun { get; set; }

    public bool ResetVolumeDemo { get; set; }

    public int Days { get; set; } = 14;

    public int ActiveCustomers { get; set; } = 300;

    public int PeakOrders { get; set; } = 500;

    public int Seed { get; set; } = 20260608;
}

public sealed record VolumeDemoConfig(
    int Days,
    int ActiveCustomers,
    int PeakOrders,
    int Seed,
    bool DryRun,
    bool ResetVolumeDemo)
{
    public static VolumeDemoConfig Default { get; } = new(
        Days: 14,
        ActiveCustomers: 300,
        PeakOrders: 500,
        Seed: 20260608,
        DryRun: false,
        ResetVolumeDemo: false);
}

public enum DatabaseSeedingMode
{
    Startup,
    Command,
    Both,
}

public enum DatabaseSeedingProfile
{
    MinimalRealistic,
    DemoData,
    VolumeDemo,
}

public enum SeedingTrigger
{
    Startup,
    Command,
}
