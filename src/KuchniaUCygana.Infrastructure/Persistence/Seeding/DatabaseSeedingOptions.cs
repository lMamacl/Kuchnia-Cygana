namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeedingOptions
{
    public const string SectionName = "DatabaseSeeding";

    public bool? Enabled { get; set; }

    public string? Mode { get; set; }

    public string? Profile { get; set; }

    public bool ResetDemoData { get; set; }
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
