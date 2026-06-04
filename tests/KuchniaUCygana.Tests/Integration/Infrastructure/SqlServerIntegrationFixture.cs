using KuchniaUCygana.Infrastructure;
using KuchniaUCygana.Infrastructure.Persistence.Migrations;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace KuchniaUCygana.Tests.Integration.Infrastructure;

public sealed class SqlServerIntegrationFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string DatabaseName { get; } = $"KuchniaUCygana_Int_{Guid.NewGuid():N}";

    public string AppConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Prepare a disposable SQL Server instance for integration tests: start the container, construct the application connection string, run migrations, and seed the database.
    /// </summary>
    /// <returns>A task that completes when the container is started, the migration runner has executed, and the database seeding has finished.</returns>
    public async Task InitializeAsync()
    {
        await container.StartAsync();

        AppConnectionString = BuildAppConnectionString(container.GetConnectionString(), DatabaseName);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = AppConnectionString,
                ["DatabaseSeeding:Enabled"] = "true",
                ["DatabaseSeeding:Mode"] = "Both",
                ["DatabaseSeeding:Profile"] = "MinimalRealistic",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        MigrationRunner.RunMigrations(provider, NullLogger.Instance);

        using var scope = provider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync(DatabaseSeedingProfile.MinimalRealistic);
    }

    /// <summary>
    /// Stops and disposes the SQL Server test container used by the fixture.
    /// </summary>
    /// <remarks>
    /// Cleans up resources created during initialization so the container is removed when the fixture is disposed.
    /// </remarks>
    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    /// <summary>
    /// Builds an application connection string that targets the specified database and normalizes encryption/trust settings.
    /// </summary>
    /// <param name="baseConnectionString">The original connection string to modify.</param>
    /// <param name="databaseName">The database name to set in the resulting connection string.</param>
    /// <returns>A connection string based on <paramref name="baseConnectionString"/> with the database set to <paramref name="databaseName"/>, `Encrypt=False`, and `TrustServerCertificate=True`.</returns>
    private static string BuildAppConnectionString(string baseConnectionString, string databaseName)
    {
        var normalized = baseConnectionString
            .Replace("Trust Server Certificate=", "TrustServerCertificate=", StringComparison.OrdinalIgnoreCase);

        var parts = normalized.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        var databaseSet = false;

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = $"Database={databaseName}";
                databaseSet = true;
            }
        }

        if (!databaseSet)
        {
            parts.Add($"Database={databaseName}");
        }

        parts.RemoveAll(p => p.StartsWith("Encrypt=", StringComparison.OrdinalIgnoreCase));
        parts.Add("Encrypt=False");
        parts.RemoveAll(p => p.StartsWith("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase));
        parts.Add("TrustServerCertificate=True");

        return string.Join(';', parts) + ";";
    }
}
