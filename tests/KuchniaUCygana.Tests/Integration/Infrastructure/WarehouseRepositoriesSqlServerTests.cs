using FluentAssertions;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
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
}
