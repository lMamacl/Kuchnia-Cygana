using FluentAssertions;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
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
    public async Task DemoDataSeeding_Should_Ensure_TodayLifecycle_And_Remain_Idempotent()
    {
        var seeder = new DatabaseSeeder(
            new SqlServerConnectionFactory(this.fixture.AppConnectionString),
            NullLogger<DatabaseSeeder>.Instance);

        await seeder.SeedAsync(DatabaseSeedingProfile.DemoData);
        await seeder.SeedAsync(DatabaseSeedingProfile.DemoData);

        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var orderNumber = $"DEMO-LIFE-{DateOnly.FromDateTime(today):yyyyMMdd}";
        var paymentIntentId = $"pi_demo_lifecycle_{DateOnly.FromDateTime(today):yyyyMMdd}";

        var todayPlanCount = await ScalarIntAsync(
            connection,
            "SELECT COUNT(1) FROM [DietMenuPlans] WHERE [PlanDate] = @today AND [Status] = N'Published' AND [IsDeleted] = 0;",
            ("@today", today));
        todayPlanCount.Should().Be(1);

        var todayPlanItemCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [DietMenuPlanItems] dpi
            INNER JOIN [DietMenuPlans] dp ON dp.[Id] = dpi.[DietMenuPlanId]
            WHERE dp.[PlanDate] = @today
              AND dp.[IsDeleted] = 0
              AND dpi.[IsDeleted] = 0
              AND dpi.[IsActive] = 1;
            """,
            ("@today", today));
        todayPlanItemCount.Should().BeGreaterThan(1);

        var orderCount = await ScalarIntAsync(
            connection,
            "SELECT COUNT(1) FROM [Orders] WHERE [OrderNumber] = @orderNumber AND [IsDeleted] = 0;",
            ("@orderNumber", orderNumber));
        orderCount.Should().Be(1);

        var orderId = await ScalarIntAsync(
            connection,
            "SELECT [Id] FROM [Orders] WHERE [OrderNumber] = @orderNumber AND [IsDeleted] = 0;",
            ("@orderNumber", orderNumber));

        var orderItemCount = await ScalarIntAsync(
            connection,
            "SELECT COUNT(1) FROM [OrderItems] WHERE [OrderId] = @orderId AND [IsDeleted] = 0;",
            ("@orderId", orderId));
        orderItemCount.Should().BeGreaterThanOrEqualTo(3);

        var paidOrInProductionOrderCount = await ScalarIntAsync(
            connection,
            "SELECT COUNT(1) FROM [Orders] WHERE [Id] = @orderId AND [Status] IN (2, 3) AND [IsDeleted] = 0;",
            ("@orderId", orderId));
        paidOrInProductionOrderCount.Should().Be(1);

        var deliveryCalendarCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [DeliveryCalendar]
            WHERE [OrderId] = @orderId
              AND [DeliveryDate] >= @today
              AND [DeliveryDate] < @tomorrow
              AND [IsSkipped] = 0
              AND [IsDeleted] = 0;
            """,
            ("@orderId", orderId),
            ("@today", today),
            ("@tomorrow", tomorrow));
        deliveryCalendarCount.Should().Be(1);

        var deliveryCalendarId = await ScalarIntAsync(
            connection,
            """
            SELECT TOP 1 [Id]
            FROM [DeliveryCalendar]
            WHERE [OrderId] = @orderId
              AND [DeliveryDate] >= @today
              AND [DeliveryDate] < @tomorrow
              AND [IsSkipped] = 0
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            ("@orderId", orderId),
            ("@today", today),
            ("@tomorrow", tomorrow));

        var paymentCount = await ScalarIntAsync(
            connection,
            "SELECT COUNT(1) FROM [Payments] WHERE [StripePaymentIntentId] = @paymentIntentId AND [OrderId] = @orderId AND [IsDeleted] = 0;",
            ("@paymentIntentId", paymentIntentId),
            ("@orderId", orderId));
        paymentCount.Should().Be(1);

        var packingSessionCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [PackingSessions]
            WHERE [PackingDate] = @today
              AND [OrderId] = @orderId
              AND [DeliveryCalendarId] = @deliveryCalendarId
              AND [IsDeleted] = 0;
            """,
            ("@today", today),
            ("@orderId", orderId),
            ("@deliveryCalendarId", deliveryCalendarId));
        packingSessionCount.Should().Be(1);

        var packingItemCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [PackingItems] pi
            INNER JOIN [PackingSessions] ps ON ps.[Id] = pi.[PackingSessionId]
            WHERE ps.[PackingDate] = @today
              AND ps.[OrderId] = @orderId
              AND ps.[DeliveryCalendarId] = @deliveryCalendarId
              AND ps.[IsDeleted] = 0
              AND pi.[IsDeleted] = 0;
            """,
            ("@today", today),
            ("@orderId", orderId),
            ("@deliveryCalendarId", deliveryCalendarId));
        packingItemCount.Should().BeGreaterThanOrEqualTo(3);

        var duplicateBoxCodes = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM
            (
                SELECT pi.[BoxCode]
                FROM [PackingItems] pi
                INNER JOIN [PackingSessions] ps ON ps.[Id] = pi.[PackingSessionId]
                WHERE ps.[OrderId] = @orderId
                  AND pi.[IsDeleted] = 0
                GROUP BY pi.[BoxCode]
                HAVING COUNT(1) > 1
            ) duplicates;
            """,
            ("@orderId", orderId));
        duplicateBoxCodes.Should().Be(0);
    }

    private static async Task<int> ScalarIntAsync(
        SqlConnection connection,
        string commandText,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
    }
}
