using FluentAssertions;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Infrastructure.Adapters;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;
using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.SqlServer;
using Xunit;

namespace KuchniaUCygana.Tests.Integration.Infrastructure;

[Collection("SqlServerIntegration")]
public sealed class WarehouseRepositoriesSqlServerTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public WarehouseRepositoriesSqlServerTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BatchRepository_GetActiveBatchesByStockItemAsync_ShouldReturnFefoAndNullLast()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "FEFO-Test");

        await InsertBatchAsync(connectionFactory, stockItemId, "FEFO-1", DateTimeOffset.UtcNow.AddDays(10), false, false);
        await InsertBatchAsync(connectionFactory, stockItemId, "FEFO-2", DateTimeOffset.UtcNow.AddDays(3), false, false);
        await InsertBatchAsync(connectionFactory, stockItemId, "FEFO-3", null, false, false);
        await InsertBatchAsync(connectionFactory, stockItemId, "FEFO-4", DateTimeOffset.UtcNow.AddDays(1), true, false);
        await InsertBatchAsync(connectionFactory, stockItemId, "FEFO-5", DateTimeOffset.UtcNow.AddDays(2), false, true);

        var repository = new BatchRepository(connectionFactory);
        var result = (await repository.GetActiveBatchesByStockItemAsync(stockItemId)).ToList();

        result.Should().HaveCount(3);
        result.All(x => !x.IsDeleted && !x.IsDepleted).Should().BeTrue();

        result[0].SupplierBatchNumber.Should().Be("FEFO-2");
        result[1].SupplierBatchNumber.Should().Be("FEFO-1");
        result[2].SupplierBatchNumber.Should().Be("FEFO-3");
        result[2].ExpiryDate.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BatchRepository_GetExpiringBeforeAsync_ShouldIncludeCutoffBoundary()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Expiry-Test");
        var cutoff = new DateTimeOffset(2026, 1, 12, 12, 0, 0, TimeSpan.Zero);

        await InsertBatchAsync(connectionFactory, stockItemId, "EXP-1", cutoff.AddDays(-1), false, false);
        await InsertBatchAsync(connectionFactory, stockItemId, "EXP-2", cutoff, false, false);
        await InsertBatchAsync(connectionFactory, stockItemId, "EXP-3", cutoff.AddMinutes(1), false, false);
        await InsertBatchAsync(connectionFactory, stockItemId, "EXP-4", null, false, false);

        var repository = new BatchRepository(connectionFactory);
        var result = (await repository.GetExpiringBeforeAsync(cutoff)).ToList();

        result.Select(x => x.SupplierBatchNumber).Should().Contain(new[] { "EXP-1", "EXP-2" });
        result.Select(x => x.SupplierBatchNumber).Should().NotContain(new[] { "EXP-3", "EXP-4" });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InventoryTransactionRepository_GetByDateRangeAsync_ShouldIncludeFromAndToBoundaries()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Range-Test");
        var batchId = await InsertBatchAsync(connectionFactory, stockItemId, "RANGE-BATCH", DateTimeOffset.UtcNow.AddDays(20), false, false);

        var from = new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero);

        await InsertInventoryTransactionAsync(connectionFactory, batchId, from.AddMinutes(-1), "RNG-BEF");
        await InsertInventoryTransactionAsync(connectionFactory, batchId, from, "RNG-FROM");
        await InsertInventoryTransactionAsync(connectionFactory, batchId, to, "RNG-TO");
        await InsertInventoryTransactionAsync(connectionFactory, batchId, to.AddMinutes(1), "RNG-AFT");

        var repository = new InventoryTransactionRepository(connectionFactory);
        var result = (await repository.GetByDateRangeAsync(from, to)).ToList();

        var refs = result.Select(x => x.ReferenceDocument).ToList();
        refs.Should().Contain("RNG-FROM");
        refs.Should().Contain("RNG-TO");
        refs.Should().NotContain("RNG-BEF");
        refs.Should().NotContain("RNG-AFT");
    }

    private SqlServerConnectionFactory CreateConnectionFactory()
    {
        var ormLite = new OrmLiteConnectionFactory(fixture.AppConnectionString, SqlServerDialect.Provider);
        return new SqlServerConnectionFactory(ormLite);
    }

    private static async Task<int> CreateStockItemAsync(IDbConnectionFactory connectionFactory, string nameSuffix)
    {
        using var db = connectionFactory.CreateConnection();

        var unit = new UnitOfMeasure
        {
            Symbol = $"kg-{Guid.NewGuid():N}"[..8],
            Name = $"Kilogram {nameSuffix}",
            Description = "Jednostka testowa",
        };

        var unitId = (int)await db.InsertAsync(unit, selectIdentity: true);

        var stockItem = new StockItem
        {
            Name = $"Skladnik {nameSuffix}",
            BaseIngredientId = 9000,
            DefaultUnitOfMeasureId = unitId,
            MinimumLevel = 5.25m,
            LeadTimeDays = 2,
            CreatedBy = "IntegrationTest",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return (int)await db.InsertAsync(stockItem, selectIdentity: true);
    }

    private static async Task<int> InsertBatchAsync(
        IDbConnectionFactory connectionFactory,
        int stockItemId,
        string supplierBatchNumber,
        DateTimeOffset? expiryDate,
        bool isDepleted,
        bool isDeleted)
    {
        using var db = connectionFactory.CreateConnection();

        var batch = new Batch
        {
            StockItemId = stockItemId,
            SupplierBatchNumber = supplierBatchNumber,
            CurrentQuantity = 100m,
            ReceivedDate = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiryDate = expiryDate,
            IsDepleted = isDepleted,
            IsDeleted = isDeleted,
            CreatedBy = "IntegrationTest",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return (int)await db.InsertAsync(batch, selectIdentity: true);
    }

    private static async Task InsertInventoryTransactionAsync(
        IDbConnectionFactory connectionFactory,
        int batchId,
        DateTimeOffset createdAt,
        string referenceDocument)
    {
        using var db = connectionFactory.CreateConnection();

        var row = new InventoryTransactionInsertRow
        {
            BatchId = batchId,
            TransactionType = (int)InventoryTransactionType.Receipt,
            QuantityChanged = 10m,
            Reason = "Test przedzialu dat",
            ReferenceDocument = referenceDocument,
            CreatedAt = createdAt,
        };

        await db.InsertAsync(row);
    }

    [Alias("InventoryTransactions")]
    private sealed class InventoryTransactionInsertRow
    {
        public int BatchId { get; set; }

        public int TransactionType { get; set; }

        public decimal QuantityChanged { get; set; }

        public string? Reason { get; set; }

        public string? ReferenceDocument { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PackingSessionRepository_GetActiveByDateAsync_ShouldFilterByStatus()
    {
        var connectionFactory = CreateConnectionFactory();
        using var db = connectionFactory.CreateConnection();
        var date = new DateOnly(2026, 1, 15);

        await db.InsertAsync(new KuchniaUCygana.Domain.Entities.Packing.PackingSession
        {
            PackingDate = date,
            Status = PackingStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.InsertAsync(new KuchniaUCygana.Domain.Entities.Packing.PackingSession
        {
            PackingDate = date,
            Status = PackingStatus.Dispatched,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.InsertAsync(new KuchniaUCygana.Domain.Entities.Packing.PackingSession
        {
            PackingDate = date.AddDays(1),
            Status = PackingStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.InsertAsync(new KuchniaUCygana.Domain.Entities.Packing.PackingSession
        {
            PackingDate = date,
            Status = PackingStatus.Pending,
            IsDeleted = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var repo = new PackingSessionRepository(connectionFactory);
        var result = (await repo.GetActiveByDateAsync(date)).ToList();

        result.Should().HaveCount(1);
        result[0].PackingDate.Should().Be(date);
        result[0].Status.Should().Be(PackingStatus.Pending);
        result[0].IsDeleted.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PackingSessionRepository_GetWithItemsAsync_ShouldLoadNonDeletedItems()
    {
        var connectionFactory = CreateConnectionFactory();
        using var db = connectionFactory.CreateConnection();

        var sessionId = await InsertPackingSessionAsync(db, new DateOnly(2026, 1, 16), PackingStatus.Pending, false);

        await InsertPackingItemAsync(db, sessionId, "Test meal", false);
        await InsertPackingItemAsync(db, sessionId, "Deleted meal", true);

        var repo = new PackingSessionRepository(connectionFactory);
        var result = await repo.GetWithItemsAsync(sessionId);

        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle();
        result.Items[0].MealName.Should().Be("Test meal");
    }
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TemperatureLogRepository_GetByLocationAsync_ShouldFilterProperly()
    {
        var connectionFactory = CreateConnectionFactory();
        using var db = connectionFactory.CreateConnection();

        await db.InsertAsync(new TemperatureLog
        {
            DeviceNameOrLocation = "Cold room A",
            RecordedTemperatureCelsius = 4.2m,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.InsertAsync(new TemperatureLog
        {
            DeviceNameOrLocation = "Cold room B",
            RecordedTemperatureCelsius = 3.5m,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.InsertAsync(new TemperatureLog
        {
            DeviceNameOrLocation = "Cold room A",
            RecordedTemperatureCelsius = 8.5m,
            RecordedAt = DateTimeOffset.UtcNow,
            IsDeleted = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var repo = new TemperatureLogRepository(connectionFactory);
        var result = (await repo.GetByLocationAsync("Cold room A")).ToList();

        result.Should().ContainSingle();
        result[0].DeviceNameOrLocation.Should().Be("Cold room A");
        result[0].IsDeleted.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TemperatureLogRepository_GetByDateRangeAsync_ShouldIncludeBoundariesAndSkipDeleted()
    {
        var connectionFactory = CreateConnectionFactory();
        using var db = connectionFactory.CreateConnection();
        var from = new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

        await InsertTemperatureLogAsync(db, "Before", from.AddMinutes(-1), false);
        await InsertTemperatureLogAsync(db, "From", from, false);
        await InsertTemperatureLogAsync(db, "To", to, false);
        await InsertTemperatureLogAsync(db, "Deleted", from.AddMinutes(30), true);
        await InsertTemperatureLogAsync(db, "After", to.AddMinutes(1), false);

        var repo = new TemperatureLogRepository(connectionFactory);
        var result = (await repo.GetByDateRangeAsync(from, to)).ToList();

        result.Select(x => x.DeviceNameOrLocation).Should().Contain(new[] { "From", "To" });
        result.Select(x => x.DeviceNameOrLocation).Should().NotContain(new[] { "Before", "Deleted", "After" });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task M1OrderDataProvider_GetActiveOrdersAsync_ShouldReadSeededM1OrderItems()
    {
        var connectionFactory = CreateConnectionFactory();
        var deliveryDate = new DateOnly(2026, 3, 10);
        var seeded = await SeedM1OrderAsync(connectionFactory, deliveryDate);

        var provider = new M1OrderDataProvider(connectionFactory);
        var result = (await provider.GetActiveOrdersAsync(deliveryDate)).ToList();

        result.Should().ContainSingle(x =>
            x.OrderId == seeded.OrderId &&
            x.ClientId == seeded.CustomerId &&
            x.ClientName == seeded.CustomerName &&
            x.DietVariantId == seeded.DietVariantId &&
            x.DeliveryDate == deliveryDate);
    }

    private static async Task InsertTemperatureLogAsync(
        System.Data.IDbConnection db,
        string location,
        DateTimeOffset recordedAt,
        bool isDeleted)
    {
        await db.InsertAsync(new TemperatureLog
        {
            DeviceNameOrLocation = location,
            RecordedTemperatureCelsius = 4.0m,
            RecordedAt = recordedAt,
            IsDeleted = isDeleted,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static async Task<int> InsertPackingSessionAsync(
        System.Data.IDbConnection db,
        DateOnly packingDate,
        PackingStatus status,
        bool isDeleted)
    {
        return await db.SqlScalarAsync<int>(
            """
            INSERT INTO PackingSessions
                (PackingDate, Status, IsDeleted, CreatedAt)
            VALUES
                (@packingDate, @status, @isDeleted, @createdAt);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                packingDate = packingDate.ToDateTime(TimeOnly.MinValue),
                status = (int)status,
                isDeleted,
                createdAt = DateTime.UtcNow,
            });
    }

    private static async Task InsertPackingItemAsync(
        System.Data.IDbConnection db,
        int sessionId,
        string mealName,
        bool isDeleted)
    {
        await db.ExecuteSqlAsync(
            """
            INSERT INTO PackingItems
                (PackingSessionId, MealId, MealName, DietVariantId, IsDeleted, CreatedAt)
            VALUES
                (@sessionId, @mealId, @mealName, @dietVariantId, @isDeleted, @createdAt);
            """,
            new
            {
                sessionId,
                mealId = isDeleted ? 102 : 101,
                mealName,
                dietVariantId = isDeleted ? 202 : 201,
                isDeleted,
                createdAt = DateTime.UtcNow,
            });
    }

    private static async Task<SeededM1Order> SeedM1OrderAsync(
        IDbConnectionFactory connectionFactory,
        DateOnly deliveryDate)
    {
        using var db = connectionFactory.CreateConnection();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        var userId = await db.SqlScalarAsync<int>(
            """
            INSERT INTO Users
                (Email, PasswordHash, FirstName, LastName, Role, CreatedAt)
            VALUES
                (@email, @passwordHash, @firstName, @lastName, @role, @createdAt);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                email = $"m1-{unique}@kuchnia.local",
                passwordHash = "test",
                firstName = "M1",
                lastName = $"Client {unique}",
                role = UserRoles.Client,
                createdAt = now,
            });

        var addressId = await db.SqlScalarAsync<int>(
            """
            INSERT INTO Addresses
                (UserId, Label, Street, BuildingNumber, City, PostalCode, IsDeleted, CreatedAt)
            VALUES
                (@userId, @label, @street, @buildingNumber, @city, @postalCode, 0, @createdAt);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                userId,
                label = "Test",
                street = "Testowa",
                buildingNumber = "1",
                city = "Warszawa",
                postalCode = "00-001",
                createdAt = now,
            });

        var orderId = await db.SqlScalarAsync<int>(
            """
            INSERT INTO Orders
                (CustomerId, OrderNumber, Status, TotalPrice, DiscountAmount, FinalPrice, StartDate, EndDate, IsDeleted, CreatedAt)
            VALUES
                (@customerId, @orderNumber, @status, @totalPrice, 0, @finalPrice, @startDate, @endDate, 0, @createdAt);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                customerId = userId,
                orderNumber = $"M1-{unique}",
                status = (int)OrderStatus.Paid,
                totalPrice = 100m,
                finalPrice = 100m,
                startDate = deliveryDate.ToDateTime(TimeOnly.MinValue),
                endDate = deliveryDate.ToDateTime(TimeOnly.MinValue),
                createdAt = now,
            });

        var dietVariantId = 501;
        await db.ExecuteSqlAsync(
            """
            INSERT INTO OrderItems
                (OrderId, DietId, DietName, DietVariantId, VariantName, CaloriesPerDay, PricePerDay, TotalDays, TotalPrice, IsDeleted, CreatedAt)
            VALUES
                (@orderId, @dietId, @dietName, @dietVariantId, @variantName, @caloriesPerDay, @pricePerDay, @totalDays, @totalPrice, 0, @createdAt);
            """,
            new
            {
                orderId,
                dietId = 301,
                dietName = "Test diet",
                dietVariantId,
                variantName = "2000 kcal",
                caloriesPerDay = 2000,
                pricePerDay = 100m,
                totalDays = 1,
                totalPrice = 100m,
                createdAt = now,
            });

        await db.ExecuteSqlAsync(
            """
            INSERT INTO DeliveryCalendar
                (OrderId, AddressId, DeliveryWindowId, DeliveryDate, Status, IsSkipped, IsDeleted, CreatedAt)
            VALUES
                (@orderId, @addressId, @deliveryWindowId, @deliveryDate, @status, 0, 0, @createdAt);
            """,
            new
            {
                orderId,
                addressId,
                deliveryWindowId = 1,
                deliveryDate = deliveryDate.ToDateTime(new TimeOnly(8, 0)),
                status = (int)DeliveryStatus.Scheduled,
                createdAt = now,
            });

        return new SeededM1Order(orderId, userId, $"M1 Client {unique}", dietVariantId);
    }

    private sealed record SeededM1Order(
        int OrderId,
        int CustomerId,
        string CustomerName,
        int DietVariantId);
}
