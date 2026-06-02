using Dapper;
using FluentAssertions;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KuchniaUCygana.Tests.Integration.Infrastructure;

[CollectionDefinition("SqlServerIntegration")]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationFixture>
{
}

[Collection("SqlServerIntegration")]
public sealed class SqlServerMigrationAndSeedingTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public SqlServerMigrationAndSeedingTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Migrations_Should_Create_CoreTables_WithExpectedTypes()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var checks = new (string TableName, string ColumnName, string ExpectedType)[]
        {
            ("Users", "CreatedAt", "datetimeoffset"),
            ("Users", "Role", "nvarchar"),
            ("Users", "Email", "nvarchar"),
        };

        foreach (var check in checks)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName
                """;
            command.Parameters.AddWithValue("@tableName", check.TableName);
            command.Parameters.AddWithValue("@columnName", check.ColumnName);

            var actualType = (string?)await command.ExecuteScalarAsync();
            actualType.Should().Be(check.ExpectedType);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Seeding_Should_Insert_MinimalRealistic_Dataset()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        await using var usersCountCommand = connection.CreateCommand();
        usersCountCommand.CommandText = "SELECT COUNT(1) FROM [Users]";
        var usersCount = (int)(await usersCountCommand.ExecuteScalarAsync() ?? 0);
        usersCount.Should().BeGreaterThan(0);

        await using var rolesCommand = connection.CreateCommand();
        rolesCommand.CommandText = "SELECT COUNT(1) FROM [Users] WHERE [Role] IN (N'Admin', N'Kitchen', N'Driver')";
        var knownRolesCount = (int)(await rolesCommand.ExecuteScalarAsync() ?? 0);
        knownRolesCount.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LogisticsDemoDataSeeder_Should_Create_Idempotent_ReadyDriverScenario()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var routeName = $"DEMO-M4-{today:yyyyMMdd}";
        var seeder = new LogisticsDemoDataSeeder(NullLogger<LogisticsDemoDataSeeder>.Instance);

        await seeder.SeedAsync(connection, now, "IntegrationTest");
        await seeder.SeedAsync(connection, now, "IntegrationTest");

        var counts = await connection.QuerySingleAsync<LogisticsScenarioCounts>(
            """
            SELECT
                (SELECT COUNT(1)
                 FROM [DeliveryRoutes]
                 WHERE [Name] = @routeName AND [IsDeleted] = 0) AS [Routes],
                (SELECT COUNT(1)
                 FROM [DeliveryRouteStops] s
                 INNER JOIN [DeliveryRoutes] r ON r.[Id] = s.[RouteId]
                 WHERE r.[Name] = @routeName AND s.[IsDeleted] = 0) AS [Stops],
                (SELECT COUNT(1)
                 FROM [PackingSessions] ps
                 INNER JOIN [DeliveryRouteStops] s ON s.[DeliveryCalendarId] = ps.[DeliveryCalendarId]
                 INNER JOIN [DeliveryRoutes] r ON r.[Id] = s.[RouteId]
                 WHERE r.[Name] = @routeName AND ps.[IsDeleted] = 0) AS [Sessions],
                (SELECT COUNT(1)
                 FROM [PackingBags] b
                 INNER JOIN [PackingSessions] ps ON ps.[Id] = b.[PackingSessionId]
                 INNER JOIN [DeliveryRouteStops] s ON s.[DeliveryCalendarId] = ps.[DeliveryCalendarId]
                 INNER JOIN [DeliveryRoutes] r ON r.[Id] = s.[RouteId]
                 WHERE r.[Name] = @routeName AND b.[IsDeleted] = 0) AS [Bags],
                (SELECT COUNT(1)
                 FROM [PackingManifests] m
                 INNER JOIN [DeliveryRoutes] r ON r.[Id] = m.[RouteId]
                 WHERE r.[Name] = @routeName
                   AND m.[IsVerified] = 1
                   AND m.[SentToLogisticsAt] IS NOT NULL
                   AND m.[RequiresRegeneration] = 0) AS [ReadyManifests],
                (SELECT COUNT(1)
                 FROM [ThermalBags]
                 WHERE [SerialNumber] LIKE N'THERM-DEMO-%'
                   AND [IsDeleted] = 0) AS [ThermalBags];
            """,
            new { routeName });

        counts.Routes.Should().Be(1);
        counts.Stops.Should().Be(4);
        counts.Sessions.Should().Be(4);
        counts.Bags.Should().Be(4);
        counts.ReadyManifests.Should().Be(1);
        counts.ThermalBags.Should().Be(12);
    }

    private sealed class LogisticsScenarioCounts
    {
        public int Routes { get; set; }

        public int Stops { get; set; }

        public int Sessions { get; set; }

        public int Bags { get; set; }

        public int ReadyManifests { get; set; }

        public int ThermalBags { get; set; }
    }
}
