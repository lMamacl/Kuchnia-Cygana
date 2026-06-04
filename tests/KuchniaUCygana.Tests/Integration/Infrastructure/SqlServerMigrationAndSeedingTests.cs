using FluentAssertions;
using KuchniaUCygana.Application;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Infrastructure;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task DemoDataSeeding_Should_Ensure_UnifiedScenario_And_Remain_Idempotent()
    {
        var seeder = new DatabaseSeeder(
            new SqlServerConnectionFactory(this.fixture.AppConnectionString),
            NullLogger<DatabaseSeeder>.Instance);

        await seeder.SeedAsync(DatabaseSeedingProfile.DemoData, resetDemoData: true);
        await seeder.SeedAsync(DatabaseSeedingProfile.DemoData);
        await seeder.SeedAsync(DatabaseSeedingProfile.DemoData);

        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var orderNumberPrefix = $"DEMO-M4-{DateOnly.FromDateTime(today):yyyyMMdd}-%";

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
            "SELECT COUNT(1) FROM [Orders] WHERE [OrderNumber] LIKE @orderNumberPrefix AND [IsDeleted] = 0;",
            ("@orderNumberPrefix", orderNumberPrefix));
        orderCount.Should().BeGreaterThanOrEqualTo(14);

        var deliveryCalendarCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [DeliveryCalendar]
            WHERE [OrderId] IN
              (
                  SELECT [Id]
                  FROM [Orders]
                  WHERE [OrderNumber] LIKE @orderNumberPrefix
                    AND [IsDeleted] = 0
              )
              AND [DeliveryDate] >= @today
              AND [DeliveryDate] < @tomorrow
              AND [IsSkipped] = 0
              AND [IsDeleted] = 0;
            """,
            ("@orderNumberPrefix", orderNumberPrefix),
            ("@today", today),
            ("@tomorrow", tomorrow));
        deliveryCalendarCount.Should().Be(orderCount);

        var orderItemCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [OrderItems]
            WHERE [OrderId] IN
              (
                  SELECT [Id]
                  FROM [Orders]
                  WHERE [OrderNumber] LIKE @orderNumberPrefix
                    AND [IsDeleted] = 0
              )
              AND [MealId] IS NOT NULL
              AND [DietMenuPlanItemId] IS NOT NULL
              AND [IsDeleted] = 0;
            """,
            ("@orderNumberPrefix", orderNumberPrefix));
        orderItemCount.Should().BeGreaterThan(orderCount);

        var orderItemsWithoutPackagingRequirements = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [OrderItems] oi
            WHERE [OrderId] IN
              (
                  SELECT [Id]
                  FROM [Orders]
                  WHERE [OrderNumber] LIKE @orderNumberPrefix
                    AND [IsDeleted] = 0
              )
              AND [MealId] IS NOT NULL
              AND [IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [PackagingRequirements] pr
                  WHERE pr.[MealId] = oi.[MealId]
                    AND pr.[IsDeleted] = 0
                    AND (pr.[StockItemId] IS NOT NULL OR pr.[WarehouseCategoryId] IS NOT NULL)
              );
            """,
            ("@orderNumberPrefix", orderNumberPrefix));
        orderItemsWithoutPackagingRequirements.Should().Be(0);

        var bialystokStopCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [DeliveryCalendar] dc
            INNER JOIN [Addresses] a ON a.[Id] = dc.[AddressId]
            INNER JOIN [Orders] o ON o.[Id] = dc.[OrderId]
            WHERE o.[OrderNumber] LIKE @orderNumberPrefix
              AND a.[City] = N'Bialystok'
              AND a.[Latitude] IS NOT NULL
              AND a.[Longitude] IS NOT NULL
              AND dc.[IsDeleted] = 0
              AND o.[IsDeleted] = 0
              AND a.[IsDeleted] = 0;
            """,
            ("@orderNumberPrefix", orderNumberPrefix));
        bialystokStopCount.Should().Be(orderCount);

        var productionPlanItemCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [ProductionPlanItems] ppi
            INNER JOIN [ProductionPlans] pp ON pp.[Id] = ppi.[ProductionPlanId]
            WHERE pp.[ProductionDate] = @today
              AND pp.[CreatedBy] = N'DemoSeeder'
              AND pp.[IsDeleted] = 0
              AND ppi.[IsDeleted] = 0;
            """,
            ("@today", today));
        productionPlanItemCount.Should().BeGreaterThan(0);

        var productionPlanItemsWithoutPackagingSnapshot = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [ProductionPlanItems] ppi
            INNER JOIN [ProductionPlans] pp ON pp.[Id] = ppi.[ProductionPlanId]
            WHERE pp.[ProductionDate] = @today
              AND pp.[CreatedBy] = N'DemoSeeder'
              AND pp.[IsDeleted] = 0
              AND ppi.[IsDeleted] = 0
              AND
              (
                  ppi.[M2SnapshotJson] IS NULL
                  OR ISJSON(ppi.[M2SnapshotJson]) <> 1
                  OR NOT EXISTS
                  (
                      SELECT 1
                      FROM OPENJSON(ppi.[M2SnapshotJson], '$.PackagingRequirements')
                  )
              );
            """,
            ("@today", today));
        productionPlanItemsWithoutPackagingSnapshot.Should().Be(0);

        var seededRouteCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [DeliveryRouteStops] drs
            WHERE drs.[DeliveryCalendarId] IN
              (
                  SELECT dc.[Id]
                  FROM [DeliveryCalendar] dc
                  INNER JOIN [Orders] o ON o.[Id] = dc.[OrderId]
                  WHERE o.[OrderNumber] LIKE @orderNumberPrefix
                    AND dc.[IsDeleted] = 0
                    AND o.[IsDeleted] = 0
              )
              AND drs.[IsDeleted] = 0;
            """,
            ("@orderNumberPrefix", orderNumberPrefix));
        seededRouteCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DemoDataSeeding_Should_Replenish_PackagingInventory_WhenStockWasConsumed()
    {
        var seeder = new DatabaseSeeder(
            new SqlServerConnectionFactory(this.fixture.AppConnectionString),
            NullLogger<DatabaseSeeder>.Instance);

        await seeder.SeedAsync(DatabaseSeedingProfile.DemoData, resetDemoData: true);

        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var boxStockItemId = await ScalarIntAsync(
            connection,
            "SELECT [Id] FROM [StockItems] WHERE [Name] = @name AND [IsDeleted] = 0;",
            ("@name", "Pudełko cateringowe 500ml"));
        boxStockItemId.Should().BeGreaterThan(0);

        await ExecuteNonQueryAsync(
            connection,
            """
            UPDATE [Batches]
            SET [CurrentQuantity] = 0,
                [IsDepleted] = 1
            WHERE [StockItemId] = @stockItemId
              AND [IsDeleted] = 0;
            """,
            ("@stockItemId", boxStockItemId));

        var depletedAvailable = await ScalarDecimalAsync(
            connection,
            """
            SELECT COALESCE(SUM([CurrentQuantity]), 0)
            FROM [Batches]
            WHERE [StockItemId] = @stockItemId
              AND [IsDeleted] = 0
              AND [IsDepleted] = 0;
            """,
            ("@stockItemId", boxStockItemId));
        depletedAvailable.Should().Be(0m);

        await seeder.SeedAsync(DatabaseSeedingProfile.DemoData);

        var availableAfterReseed = await ScalarDecimalAsync(
            connection,
            """
            SELECT COALESCE(SUM([CurrentQuantity]), 0)
            FROM [Batches]
            WHERE [StockItemId] = @stockItemId
              AND [IsDeleted] = 0
              AND [IsDepleted] = 0;
            """,
            ("@stockItemId", boxStockItemId));
        availableAfterReseed.Should().BeGreaterThanOrEqualTo(500m);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DemoWorkflow_Should_GenerateRoutes_And_LinkProductionPackingLoadingToSameOrders()
    {
        await using var provider = BuildWorkflowServiceProvider();
        using var scope = provider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

        await seeder.SeedAsync(DatabaseSeedingProfile.DemoData, resetDemoData: true);

        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var today = DateTime.Today;
        var todayOnly = DateOnly.FromDateTime(today);
        var orderNumberPrefix = $"DEMO-M4-{todayOnly:yyyyMMdd}-%";

        var orderCount = await ScalarIntAsync(
            connection,
            "SELECT COUNT(1) FROM [Orders] WHERE [OrderNumber] LIKE @orderNumberPrefix AND [IsDeleted] = 0;",
            ("@orderNumberPrefix", orderNumberPrefix));
        orderCount.Should().BeGreaterThan(0);

        var ordersWithProfiles = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(DISTINCT o.[Id])
            FROM [Orders] o
            INNER JOIN [Users] u ON u.[Id] = o.[CustomerId]
            INNER JOIN [CustomerProfiles] cp ON cp.[UserId] = u.[Id]
            WHERE o.[OrderNumber] LIKE @orderNumberPrefix
              AND o.[IsDeleted] = 0
              AND cp.[IsDeleted] = 0;
            """,
            ("@orderNumberPrefix", orderNumberPrefix));
        ordersWithProfiles.Should().Be(orderCount);

        var orderItemCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [OrderItems] oi
            INNER JOIN [Orders] o ON o.[Id] = oi.[OrderId]
            WHERE o.[OrderNumber] LIKE @orderNumberPrefix
              AND o.[IsDeleted] = 0
              AND oi.[IsDeleted] = 0;
            """,
            ("@orderNumberPrefix", orderNumberPrefix));
        orderItemCount.Should().BeGreaterThan(orderCount);

        var orderItemsMatchedToDietPlan = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [OrderItems] oi
            INNER JOIN [Orders] o ON o.[Id] = oi.[OrderId]
            INNER JOIN [DietMenuPlanItems] dpi
                ON dpi.[Id] = oi.[DietMenuPlanItemId]
               AND dpi.[MealId] = oi.[MealId]
               AND ISNULL(dpi.[MealVariantId], 0) = ISNULL(oi.[MealVariantId], 0)
               AND dpi.[DietVariantId] = oi.[DietVariantId]
            INNER JOIN [DietMenuPlans] dp
                ON dp.[Id] = dpi.[DietMenuPlanId]
               AND dp.[PlanDate] = @today
               AND dp.[Status] = N'Published'
               AND dp.[IsDeleted] = 0
            WHERE o.[OrderNumber] LIKE @orderNumberPrefix
              AND o.[IsDeleted] = 0
              AND oi.[IsDeleted] = 0
              AND dpi.[IsDeleted] = 0
              AND dpi.[IsActive] = 1;
            """,
            ("@orderNumberPrefix", orderNumberPrefix),
            ("@today", today));
        orderItemsMatchedToDietPlan.Should().Be(orderItemCount);

        var missingProductionPlanLinks = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM
            (
                SELECT DISTINCT
                    oi.[MealId],
                    oi.[DietVariantId],
                    oi.[DietMenuPlanItemId]
                FROM [OrderItems] oi
                INNER JOIN [Orders] o ON o.[Id] = oi.[OrderId]
                WHERE o.[OrderNumber] LIKE @orderNumberPrefix
                  AND o.[IsDeleted] = 0
                  AND oi.[IsDeleted] = 0
                  AND oi.[MealId] IS NOT NULL
                  AND oi.[DietMenuPlanItemId] IS NOT NULL
            ) required
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [ProductionPlanItems] ppi
                INNER JOIN [ProductionPlans] pp ON pp.[Id] = ppi.[ProductionPlanId]
                WHERE pp.[ProductionDate] = @today
                  AND pp.[CreatedBy] = N'DemoSeeder'
                  AND pp.[IsDeleted] = 0
                  AND ppi.[IsDeleted] = 0
                  AND ppi.[MealId] = required.[MealId]
                  AND ppi.[DietVariantId] = required.[DietVariantId]
                  AND ISNULL(ppi.[DietMenuPlanItemId], 0) = ISNULL(required.[DietMenuPlanItemId], 0)
            );
            """,
            ("@orderNumberPrefix", orderNumberPrefix),
            ("@today", today));
        missingProductionPlanLinks.Should().Be(0);

        var deliveryCalendarCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [DeliveryCalendar] dc
            INNER JOIN [Orders] o ON o.[Id] = dc.[OrderId]
            INNER JOIN [Addresses] a ON a.[Id] = dc.[AddressId]
            WHERE o.[OrderNumber] LIKE @orderNumberPrefix
              AND dc.[DeliveryDate] >= @today
              AND dc.[DeliveryDate] < DATEADD(day, 1, @today)
              AND dc.[IsSkipped] = 0
              AND dc.[IsDeleted] = 0
              AND o.[IsDeleted] = 0
              AND a.[IsDeleted] = 0
              AND a.[Latitude] IS NOT NULL
              AND a.[Longitude] IS NOT NULL;
            """,
            ("@orderNumberPrefix", orderNumberPrefix),
            ("@today", today));
        deliveryCalendarCount.Should().Be(orderCount);

        var planItemToApproveId = await ScalarIntAsync(
            connection,
            """
            SELECT TOP 1 ppi.[Id]
            FROM [ProductionPlanItems] ppi
            INNER JOIN [ProductionPlans] pp ON pp.[Id] = ppi.[ProductionPlanId]
            WHERE pp.[ProductionDate] = @today
              AND pp.[CreatedBy] = N'DemoSeeder'
              AND pp.[IsDeleted] = 0
              AND ppi.[IsDeleted] = 0
            ORDER BY ppi.[Id];
            """,
            ("@today", today));

        var plannedQuantityToApprove = await ScalarIntAsync(
            connection,
            "SELECT [PlannedQuantity] FROM [ProductionPlanItems] WHERE [Id] = @planItemId;",
            ("@planItemId", planItemToApproveId));

        var productionService = scope.ServiceProvider.GetRequiredService<IProductionService>();
        await productionService.ApproveCookingAsync(planItemToApproveId, plannedQuantityToApprove);

        var approvedPlanItemCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [ProductionPlanItems]
            WHERE [Id] = @planItemId
              AND [Status] = @cookedStatus
              AND [PackagingDeductedAt] IS NOT NULL;
            """,
            ("@planItemId", planItemToApproveId),
            ("@cookedStatus", (int)ProductionItemStatus.Cooked));
        approvedPlanItemCount.Should().Be(1);

        var seededRouteStopCount = await CountRouteStopsForDemoOrdersAsync(connection, orderNumberPrefix);
        seededRouteStopCount.Should().Be(0, "DatabaseSeeder prepares delivery candidates, while M4 generates routes");

        var routeService = scope.ServiceProvider.GetRequiredService<IDeliveryRouteService>();
        var routeResult = await routeService.GenerateDailyRoutesAsync(new GenerateDailyRoutesRequest
        {
            RouteDate = new DateTimeOffset(today),
            DefaultDeliveryLoadKg = 1m,
        });

        routeResult.Succeeded.Should().BeTrue(string.Join("; ", routeResult.Issues.Select(issue => issue.Message)));
        routeResult.PlannedStopsCount.Should().Be(deliveryCalendarCount);

        var generatedRouteStopCount = await CountRouteStopsForDemoOrdersAsync(connection, orderNumberPrefix);
        generatedRouteStopCount.Should().Be(deliveryCalendarCount);

        var packingSynchronizationService = scope.ServiceProvider.GetRequiredService<IPackingSynchronizationService>();
        var syncResult = await packingSynchronizationService.RefreshFromRoutesAsync(todayOnly, "IntegrationTest");
        syncResult.RefreshedRoutes.Should().Be(routeResult.GeneratedRoutesCount);
        syncResult.CreatedItems.Should().Be(orderItemCount);

        var packingService = scope.ServiceProvider.GetRequiredService<IPackingService>();
        var sessions = (await packingService.GetSessionsByDateAsync(todayOnly))
            .Where(session => session.OrderId.GetValueOrDefault() > 0 && session.DeliveryCalendarId.HasValue)
            .ToList();
        sessions.Should().HaveCount(deliveryCalendarCount);
        sessions.Sum(session => session.Items.Count).Should().Be(orderItemCount);

        foreach (var session in sessions)
        {
            foreach (var item in session.Items)
            {
                await packingService.PrintFoilLabelAsync(item.Id, "IntegrationKitchen");
                await packingService.MarkBoxPackedAsync(item.Id, "IntegrationPacking");
            }

            await packingService.PackOrderBagAsync(session.Id, "IntegrationPacking");
            var labels = (await packingService.GenerateTransportLabelsAsync(session.Id)).ToList();
            labels.Should().ContainSingle();
            await packingService.ConfirmTransportLabelAttachedAsync(labels.Single().Id);
        }

        var routeIds = routeResult.Routes.Select(route => route.Id).ToList();
        var loadingService = scope.ServiceProvider.GetRequiredService<ILoadingService>();
        var manifestService = scope.ServiceProvider.GetRequiredService<IManifestService>();
        foreach (var routeId in routeIds)
        {
            await manifestService.GenerateManifestAsync(todayOnly, routeId, "IntegrationPacking");
            await manifestService.ApproveManifestByWorkerAsync(todayOnly, routeId, "IntegrationPacking");

            var route = await loadingService.GetRouteDetailsAsync(todayOnly, routeId);
            route.Should().NotBeNull();
            route!.Bags.Should().NotBeEmpty();
            foreach (var bag in route.Bags)
            {
                await loadingService.LoadBagByCodeAsync(routeId, bag.BagCode);
            }

            await manifestService.ApproveManifestBySupervisorAsync(todayOnly, routeId, "IntegrationSupervisor");
            await loadingService.DispatchAsync(todayOnly, routeId);
        }

        var dispatchedSessionCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [PackingSessions] ps
            INNER JOIN [DeliveryCalendar] dc ON dc.[Id] = ps.[DeliveryCalendarId]
            INNER JOIN [Orders] o ON o.[Id] = dc.[OrderId]
            WHERE o.[OrderNumber] LIKE @orderNumberPrefix
              AND ps.[Status] = @dispatchedStatus
              AND ps.[IsDeleted] = 0;
            """,
            ("@orderNumberPrefix", orderNumberPrefix),
            ("@dispatchedStatus", (int)PackingStatus.Dispatched));
        dispatchedSessionCount.Should().Be(deliveryCalendarCount);

        var verifiedManifestCount = await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [PackingManifests] pm
            WHERE pm.[PackingDate] = @today
              AND pm.[RouteId] IN
              (
                  SELECT DISTINCT drs.[RouteId]
                  FROM [DeliveryRouteStops] drs
                  INNER JOIN [DeliveryCalendar] dc ON dc.[Id] = drs.[DeliveryCalendarId]
                  INNER JOIN [Orders] o ON o.[Id] = dc.[OrderId]
                  WHERE o.[OrderNumber] LIKE @orderNumberPrefix
                    AND drs.[IsDeleted] = 0
                    AND dc.[IsDeleted] = 0
                    AND o.[IsDeleted] = 0
              )
              AND pm.[IsVerified] = 1
              AND pm.[SentToLogisticsAt] IS NOT NULL
              AND pm.[RequiresRegeneration] = 0
              AND pm.[IsSuperseded] = 0;
            """,
            ("@today", today),
            ("@orderNumberPrefix", orderNumberPrefix));
        verifiedManifestCount.Should().Be(routeIds.Count);
    }

    private ServiceProvider BuildWorkflowServiceProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = this.fixture.AppConnectionString,
                ["ConnectionStrings:MigrationConnection"] = this.fixture.MigrationConnectionString,
                ["OrderProvider"] = "M1",
                ["DeliveryManifestProvider"] = "M4",
                ["Application:BaseUrl"] = "https://integration.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(config);

        return services.BuildServiceProvider();
    }

    private static async Task<int> CountRouteStopsForDemoOrdersAsync(SqlConnection connection, string orderNumberPrefix)
    {
        return await ScalarIntAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM [DeliveryRouteStops] drs
            WHERE drs.[DeliveryCalendarId] IN
              (
                  SELECT dc.[Id]
                  FROM [DeliveryCalendar] dc
                  INNER JOIN [Orders] o ON o.[Id] = dc.[OrderId]
                  WHERE o.[OrderNumber] LIKE @orderNumberPrefix
                    AND dc.[IsDeleted] = 0
                    AND o.[IsDeleted] = 0
              )
              AND drs.[IsDeleted] = 0;
            """,
            ("@orderNumberPrefix", orderNumberPrefix));
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

    private static async Task<decimal> ScalarDecimalAsync(
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

        return Convert.ToDecimal(await command.ExecuteScalarAsync() ?? 0m);
    }

    private static async Task ExecuteNonQueryAsync(
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

        await command.ExecuteNonQueryAsync();
    }
}
