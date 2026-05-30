using FluentAssertions;
using KuchniaUCygana.Domain.Entities.Auth;
using Dapper;
using AutoMapper;
using KuchniaUCygana.Application.Mappings;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Adapters;
using KuchniaUCygana.Infrastructure.Mocks;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StockItemRepository_SearchStockLookupAsync_ShouldReturnAtMostRequestedLimit()
    {
        var connectionFactory = CreateConnectionFactory();
        var unique = $"Lookup-{Guid.NewGuid():N}"[..15];

        for (var i = 0; i < 25; i++)
        {
            var stockItemId = await CreateStockItemAsync(connectionFactory, $"{unique}-{i:D2}");
            await InsertBatchAsync(connectionFactory, stockItemId, $"LOOK-{i:D2}", DateTimeOffset.UtcNow.AddDays(10), false, false);
        }

        var repository = new StockItemRepository(connectionFactory);

        var result = (await repository.SearchStockLookupAsync(unique, limit: 20, onlyAvailable: false)).ToList();

        result.Should().HaveCount(20);
        result.Should().OnlyContain(x => x.Name.Contains(unique));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StockItemRepository_GetStockTablePageAsync_ShouldReturnTotalCountAndRequestedPage()
    {
        var connectionFactory = CreateConnectionFactory();
        var unique = $"Page-{Guid.NewGuid():N}"[..14];

        for (var i = 0; i < 3; i++)
        {
            var stockItemId = await CreateStockItemAsync(connectionFactory, $"{unique}-{i:D2}");
            await InsertBatchAsync(connectionFactory, stockItemId, $"PAGE-{i:D2}", DateTimeOffset.UtcNow.AddDays(14), false, false);
        }

        var repository = new StockItemRepository(connectionFactory);
        var query = new StockItemTableQuery(unique, CategoryId: null, LegacyCategory: null, ShowExpiredOnly: false, ShowLowStockOnly: false, ShowExpiringSoonOnly: false, Page: 2, PageSize: 2);

        var (items, totalCount) = await repository.GetStockTablePageAsync(query);

        totalCount.Should().Be(3);
        items.Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StockItemRepository_GetStockTablePageAsync_ShouldFilterExpiringSoonWithoutExpired()
    {
        var connectionFactory = CreateConnectionFactory();
        var unique = $"ExpSoon-{Guid.NewGuid():N}"[..18];
        var expiredId = await CreateStockItemAsync(connectionFactory, $"{unique}-Expired");
        var soonId = await CreateStockItemAsync(connectionFactory, $"{unique}-Soon");
        var laterId = await CreateStockItemAsync(connectionFactory, $"{unique}-Later");

        await InsertBatchAsync(connectionFactory, expiredId, "EXPSOON-EXPIRED", DateTimeOffset.UtcNow.AddDays(-1), false, false);
        await InsertBatchAsync(connectionFactory, soonId, "EXPSOON-SOON", DateTimeOffset.UtcNow.AddDays(5), false, false);
        await InsertBatchAsync(connectionFactory, laterId, "EXPSOON-LATER", DateTimeOffset.UtcNow.AddDays(14), false, false);

        var repository = new StockItemRepository(connectionFactory);
        var query = new StockItemTableQuery(
            unique,
            CategoryId: null,
            LegacyCategory: null,
            ShowExpiredOnly: false,
            ShowLowStockOnly: false,
            ShowExpiringSoonOnly: true,
            Page: 1,
            PageSize: 10);

        var (items, totalCount) = await repository.GetStockTablePageAsync(query);
        var page = items.ToList();

        totalCount.Should().Be(1);
        page.Should().ContainSingle()
            .Which.Name.Should().Contain("Soon");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StockItemRepository_GetStockTablePageAsync_ShouldFilterByWarehouseCategoryId()
    {
        var connectionFactory = CreateConnectionFactory();
        var unique = $"Cat-{Guid.NewGuid():N}"[..14];
        var meatCategoryItemId = await CreateStockItemAsync(connectionFactory, $"{unique}-Alpha", warehouseCategoryId: 1);
        var dryCategoryItemId = await CreateStockItemAsync(connectionFactory, $"{unique}-Beta", warehouseCategoryId: 4);

        await InsertBatchAsync(connectionFactory, meatCategoryItemId, "CAT-MEAT", DateTimeOffset.UtcNow.AddDays(10), false, false);
        await InsertBatchAsync(connectionFactory, dryCategoryItemId, "CAT-DRY", DateTimeOffset.UtcNow.AddDays(10), false, false);

        var repository = new StockItemRepository(connectionFactory);
        var query = new StockItemTableQuery(
            unique,
            CategoryId: 1,
            LegacyCategory: null,
            ShowExpiredOnly: false,
            ShowLowStockOnly: false,
            ShowExpiringSoonOnly: false,
            Page: 1,
            PageSize: 10);

        var (items, totalCount) = await repository.GetStockTablePageAsync(query);
        var page = items.ToList();

        totalCount.Should().Be(1);
        page.Should().ContainSingle()
            .Which.CategoryId.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BatchRepository_GetFefoReportPageAsync_ShouldFilterBeforePaging()
    {
        var connectionFactory = CreateConnectionFactory();
        var unique = $"Fefo-{Guid.NewGuid():N}"[..14];
        var matchingNameId = await CreateStockItemAsync(connectionFactory, $"{unique}-Name");
        var matchingBatchId = await CreateStockItemAsync(connectionFactory, "Fefo other product");
        var expiredId = await CreateStockItemAsync(connectionFactory, $"{unique}-Expired");

        await InsertBatchAsync(connectionFactory, matchingNameId, "LOT-NAME", DateTimeOffset.UtcNow.AddDays(5), false, false);
        await InsertBatchAsync(connectionFactory, matchingBatchId, $"{unique}-BATCH", DateTimeOffset.UtcNow.AddDays(5), false, false);
        await InsertBatchAsync(connectionFactory, expiredId, "LOT-EXPIRED", DateTimeOffset.UtcNow.AddDays(-1), false, false);

        var repository = new BatchRepository(connectionFactory);
        var query = new FefoReportQuery(unique, Status: "Warning", Page: 1, PageSize: 1, UsePaging: true);

        var (items, totalCount) = await repository.GetFefoReportPageAsync(query);

        totalCount.Should().Be(2);
        items.Should().ContainSingle()
            .Which.Status.Should().Be("Warning");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BatchRepository_GetBatchInventoryPageAsync_ShouldFilterByProductOrBatchBeforePaging()
    {
        var connectionFactory = CreateConnectionFactory();
        var unique = $"BatchInv-{Guid.NewGuid():N}"[..18];
        var matchingNameId = await CreateStockItemAsync(connectionFactory, $"{unique}-Name");
        var matchingBatchId = await CreateStockItemAsync(connectionFactory, "Other inventory product");

        await InsertBatchAsync(connectionFactory, matchingNameId, "INV-NAME", DateTimeOffset.UtcNow.AddDays(5), false, false, currentQuantity: 4m);
        await InsertBatchAsync(connectionFactory, matchingBatchId, $"{unique}-BATCH", DateTimeOffset.UtcNow.AddDays(6), false, false, currentQuantity: 6m);
        await InsertBatchAsync(connectionFactory, matchingBatchId, "INV-HIDDEN", DateTimeOffset.UtcNow.AddDays(7), false, false, currentQuantity: 8m);

        var repository = new BatchRepository(connectionFactory);
        var query = new BatchInventoryQuery(unique, CategoryId: null, LegacyCategory: null, Page: 1, PageSize: 1);

        var (items, totalCount) = await repository.GetBatchInventoryPageAsync(query);

        totalCount.Should().Be(2);
        items.Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InventoryTransactionRepository_GetTransactionHistoryPageAsync_ShouldReturnLatestPage()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "HistoryPage-Test");
        var batchId = await InsertBatchAsync(connectionFactory, stockItemId, "HISTORY-BATCH", DateTimeOffset.UtcNow.AddDays(20), false, false);
        var start = new DateTimeOffset(2040, 1, 1, 8, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 30; i++)
        {
            await InsertInventoryTransactionAsync(connectionFactory, batchId, start.AddMinutes(i), $"HIST-{i:D2}");
        }

        var repository = new InventoryTransactionRepository(connectionFactory);
        var query = new TransactionHistoryQuery(StockItemId: null, From: null, To: null, TransactionType: null, Page: 1, PageSize: 25);

        var (items, totalCount) = await repository.GetTransactionHistoryPageAsync(query);
        var page = items.ToList();

        totalCount.Should().BeGreaterThanOrEqualTo(30);
        page.Should().HaveCount(25);
        page[0].ReferenceDocument.Should().Be("HIST-29");
        page.Select(x => x.PerformedAt).Should().BeInDescendingOrder();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WarehouseCommandRepository_ApplyInventoryAsync_ShouldCreateSurplusBatchAdjustmentAndTransaction()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Inventory-Surplus");
        await InsertBatchAsync(connectionFactory, stockItemId, "INV-SUR-BASE", DateTimeOffset.UtcNow.AddDays(10), false, false, currentQuantity: 10m);

        var repository = new WarehouseCommandRepository(connectionFactory);

        var results = await repository.ApplyInventoryAsync(
            new[] { new InventoryAdjustmentCommand(stockItemId, 15m, "Test nadwyżki") },
            "IntegrationTest");

        results.Should().ContainSingle()
            .Which.Difference.Should().Be(5m);

        using var db = connectionFactory.CreateConnection();
        var surplusBatch = await db.QuerySingleAsync<Batch>(
            """
            SELECT TOP 1 *
            FROM [Batches]
            WHERE [StockItemId] = @stockItemId
              AND [SupplierBatchNumber] LIKE 'INV-%'
            ORDER BY [Id] DESC;
            """,
            new { stockItemId });

        surplusBatch.CurrentQuantity.Should().Be(5m);
        surplusBatch.ExpiryDate.Should().BeNull();

        var transaction = await db.QuerySingleAsync<InventoryTransaction>(
            """
            SELECT TOP 1 *
            FROM [InventoryTransactions]
            WHERE [BatchId] = @batchId
            ORDER BY [Id] DESC;
            """,
            new { batchId = surplusBatch.Id });

        transaction.StockItemId.Should().Be(stockItemId);
        transaction.TransactionType.Should().Be(InventoryTransactionType.Adjustment);
        transaction.QuantityChanged.Should().Be(5m);

        var adjustmentCount = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM [InventoryAdjustments] WHERE [StockItemId] = @stockItemId;",
            new { stockItemId });
        adjustmentCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WarehouseCommandRepository_ApplyInventoryAsync_ShouldDeductShortageByFefo()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Inventory-Shortage");
        var firstBatchId = await InsertBatchAsync(connectionFactory, stockItemId, "INV-SHO-1", DateTimeOffset.UtcNow.AddDays(1), false, false, currentQuantity: 10m);
        var secondBatchId = await InsertBatchAsync(connectionFactory, stockItemId, "INV-SHO-2", DateTimeOffset.UtcNow.AddDays(2), false, false, currentQuantity: 20m);

        var repository = new WarehouseCommandRepository(connectionFactory);

        var results = await repository.ApplyInventoryAsync(
            new[] { new InventoryAdjustmentCommand(stockItemId, 25m, "Test niedoboru") },
            "IntegrationTest");

        results.Should().ContainSingle()
            .Which.Difference.Should().Be(-5m);

        using var db = connectionFactory.CreateConnection();
        var firstQuantity = await db.ExecuteScalarAsync<decimal>(
            "SELECT [CurrentQuantity] FROM [Batches] WHERE [Id] = @firstBatchId;",
            new { firstBatchId });
        var secondQuantity = await db.ExecuteScalarAsync<decimal>(
            "SELECT [CurrentQuantity] FROM [Batches] WHERE [Id] = @secondBatchId;",
            new { secondBatchId });

        firstQuantity.Should().Be(5m);
        secondQuantity.Should().Be(20m);

        var transaction = await db.QuerySingleAsync<InventoryTransaction>(
            """
            SELECT TOP 1 *
            FROM [InventoryTransactions]
            WHERE [BatchId] = @firstBatchId
            ORDER BY [Id] DESC;
            """,
            new { firstBatchId });

        transaction.StockItemId.Should().Be(stockItemId);
        transaction.QuantityChanged.Should().Be(-5m);
        transaction.TransactionType.Should().Be(InventoryTransactionType.Adjustment);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WarehouseCommandRepository_ApplyBatchInventoryAsync_ShouldAdjustOnlySelectedBatch()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Inventory-Batch");
        var expiredBatchId = await InsertBatchAsync(connectionFactory, stockItemId, "BAT-OLD", DateTimeOffset.UtcNow.AddDays(-1), false, false, currentQuantity: 0.4m);
        var freshBatchId = await InsertBatchAsync(connectionFactory, stockItemId, "BAT-NEW", DateTimeOffset.UtcNow.AddDays(10), false, false, currentQuantity: 12m);

        var repository = new WarehouseCommandRepository(connectionFactory);

        var results = await repository.ApplyBatchInventoryAsync(
            new[] { new BatchInventoryAdjustmentCommand(expiredBatchId, 0m, "Przeterminowana partia zutylizowana") },
            "IntegrationTest");

        results.Should().ContainSingle()
            .Which.Difference.Should().Be(-0.4m);

        using var db = connectionFactory.CreateConnection();
        var expiredBatch = await db.QuerySingleAsync<Batch>(
            "SELECT * FROM [Batches] WHERE [Id] = @expiredBatchId;",
            new { expiredBatchId });
        var freshQuantity = await db.ExecuteScalarAsync<decimal>(
            "SELECT [CurrentQuantity] FROM [Batches] WHERE [Id] = @freshBatchId;",
            new { freshBatchId });

        expiredBatch.CurrentQuantity.Should().Be(0m);
        expiredBatch.IsDepleted.Should().BeTrue();
        freshQuantity.Should().Be(12m);

        var transaction = await db.QuerySingleAsync<InventoryTransaction>(
            """
            SELECT TOP 1 *
            FROM [InventoryTransactions]
            WHERE [BatchId] = @expiredBatchId
            ORDER BY [Id] DESC;
            """,
            new { expiredBatchId });

        transaction.StockItemId.Should().Be(stockItemId);
        transaction.QuantityChanged.Should().Be(-0.4m);
        transaction.TransactionType.Should().Be(InventoryTransactionType.Adjustment);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WarehouseCommandRepository_ApplyBatchInventoryAsync_ShouldRejectNegativePhysicalStock()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Inventory-Batch-Neg");
        var batchId = await InsertBatchAsync(connectionFactory, stockItemId, "BAT-NEG", DateTimeOffset.UtcNow.AddDays(10), false, false, currentQuantity: 3m);

        var repository = new WarehouseCommandRepository(connectionFactory);

        var act = async () => await repository.ApplyBatchInventoryAsync(
            new[] { new BatchInventoryAdjustmentCommand(batchId, -1m, "Błąd") },
            "IntegrationTest");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nie może być ujemny*");

        using var db = connectionFactory.CreateConnection();
        var quantity = await db.ExecuteScalarAsync<decimal>(
            "SELECT [CurrentQuantity] FROM [Batches] WHERE [Id] = @batchId;",
            new { batchId });
        quantity.Should().Be(3m);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WarehouseCommandRepository_ApplyInventoryAsync_ShouldSkipUnchangedInventory()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Inventory-Unchanged");
        await InsertBatchAsync(connectionFactory, stockItemId, "INV-UNCHANGED", DateTimeOffset.UtcNow.AddDays(10), false, false, currentQuantity: 100m);

        var repository = new WarehouseCommandRepository(connectionFactory);

        var results = await repository.ApplyInventoryAsync(
            new[] { new InventoryAdjustmentCommand(stockItemId, 100m, "Bez zmian") },
            "IntegrationTest");

        results.Should().BeEmpty();

        using var db = connectionFactory.CreateConnection();
        var adjustmentCount = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM [InventoryAdjustments] WHERE [StockItemId] = @stockItemId;",
            new { stockItemId });
        adjustmentCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WarehouseCommandRepository_ApplyInventoryAsync_ShouldRejectNegativePhysicalStock()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Inventory-Negative");
        await InsertBatchAsync(connectionFactory, stockItemId, "INV-NEG", DateTimeOffset.UtcNow.AddDays(10), false, false, currentQuantity: 12m);

        var repository = new WarehouseCommandRepository(connectionFactory);

        var act = async () => await repository.ApplyInventoryAsync(
            new[] { new InventoryAdjustmentCommand(stockItemId, -1m, "Błąd") },
            "IntegrationTest");

        await act.Should().ThrowAsync<InvalidOperationException>();

        using var db = connectionFactory.CreateConnection();
        var quantity = await db.ExecuteScalarAsync<decimal>(
            "SELECT [CurrentQuantity] FROM [Batches] WHERE [StockItemId] = @stockItemId;",
            new { stockItemId });
        quantity.Should().Be(12m);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WarehouseCommandRepository_ApplyInventoryAsync_ShouldRollbackWhenLaterAdjustmentFails()
    {
        var connectionFactory = CreateConnectionFactory();
        var stockItemId = await CreateStockItemAsync(connectionFactory, "Inventory-Rollback");
        await InsertBatchAsync(connectionFactory, stockItemId, "INV-ROLLBACK", DateTimeOffset.UtcNow.AddDays(10), false, false, currentQuantity: 10m);

        var repository = new WarehouseCommandRepository(connectionFactory);

        var act = async () => await repository.ApplyInventoryAsync(
            new[]
            {
                new InventoryAdjustmentCommand(stockItemId, 15m, "Pierwsza korekta"),
                new InventoryAdjustmentCommand(987654321, 1m, "Nieistniejący składnik"),
            },
            "IntegrationTest");

        await act.Should().ThrowAsync<InvalidOperationException>();

        using var db = connectionFactory.CreateConnection();
        var totalQuantity = await db.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM([CurrentQuantity]), 0)
            FROM [Batches]
            WHERE [StockItemId] = @stockItemId
              AND [IsDeleted] = 0;
            """,
            new { stockItemId });
        totalQuantity.Should().Be(10m);

        var adjustmentCount = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM [InventoryAdjustments] WHERE [StockItemId] = @stockItemId;",
            new { stockItemId });
        adjustmentCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InventoryTransactionRepository_GetTransactionHistoryPageAsync_ShouldFilterByStockItemIdColumn()
    {
        var connectionFactory = CreateConnectionFactory();
        var firstStockItemId = await CreateStockItemAsync(connectionFactory, "History-Filter-A");
        var secondStockItemId = await CreateStockItemAsync(connectionFactory, "History-Filter-B");
        var firstBatchId = await InsertBatchAsync(connectionFactory, firstStockItemId, "HIST-FLT-A", DateTimeOffset.UtcNow.AddDays(10), false, false);
        var secondBatchId = await InsertBatchAsync(connectionFactory, secondStockItemId, "HIST-FLT-B", DateTimeOffset.UtcNow.AddDays(10), false, false);

        await InsertInventoryTransactionAsync(connectionFactory, firstBatchId, DateTimeOffset.UtcNow.AddMinutes(-2), "FILTER-A", firstStockItemId);
        await InsertInventoryTransactionAsync(connectionFactory, secondBatchId, DateTimeOffset.UtcNow.AddMinutes(-1), "FILTER-B", secondStockItemId);

        var repository = new InventoryTransactionRepository(connectionFactory);
        var query = new TransactionHistoryQuery(firstStockItemId, From: null, To: null, TransactionType: null, Page: 1, PageSize: 25);

        var (items, _) = await repository.GetTransactionHistoryPageAsync(query);

        items.Should().Contain(x => x.ReferenceDocument == "FILTER-A" && x.StockItemId == firstStockItemId);
        items.Should().NotContain(x => x.ReferenceDocument == "FILTER-B");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StockItemRepository_GetSmartInventoryAlertRowsAsync_ShouldReturnAggregatedAlerts()
    {
        var connectionFactory = CreateConnectionFactory();
        var noStockId = await CreateStockItemAsync(connectionFactory, "Smart-NoStock");
        var expiringId = await CreateStockItemAsync(connectionFactory, "Smart-Expiry");
        await InsertBatchAsync(connectionFactory, expiringId, "SMART-EXP", DateTimeOffset.UtcNow.AddDays(2), false, false, currentQuantity: 100m);

        var repository = new StockItemRepository(connectionFactory);

        var rows = (await repository.GetSmartInventoryAlertRowsAsync(DateTimeOffset.UtcNow)).ToList();

        rows.Should().Contain(x => x.StockItemId == noStockId && x.AlertCode == "NoStock");
        rows.Should().Contain(x => x.StockItemId == expiringId && x.AlertCode == "ExpiringWithin3Days" && x.SupplierBatchNumber == "SMART-EXP");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProductionPlanRepository_InsertAsync_ShouldReturnGeneratedIdentity()
    {
        var connectionFactory = CreateConnectionFactory();
        var repository = new ProductionPlanRepository(connectionFactory);
        var productionDate = new DateOnly(2035, 5, 25);

        var planId = await repository.InsertAsync(new ProductionPlan
        {
            ProductionDate = productionDate,
            Status = ProductionPlanStatus.Draft,
            CreatedBy = "IntegrationTest",
        });

        planId.Should().BeGreaterThan(0);

        var persisted = await repository.GetByIdAsync(planId);

        persisted.Should().NotBeNull();
        persisted!.ProductionDate.Should().Be(productionDate);
        persisted.Status.Should().Be(ProductionPlanStatus.Draft);
    }

    private SqlServerConnectionFactory CreateConnectionFactory()
    {
        return new SqlServerConnectionFactory(fixture.AppConnectionString);
    }

    private static PackingService CreatePackingService(IDbConnectionFactory connectionFactory)
    {
        var mapperConfiguration = new MapperConfiguration(
            cfg => cfg.AddProfile<ProductionProfile>(),
            NullLoggerFactory.Instance);
        var mapper = mapperConfiguration.CreateMapper();

        return new PackingService(
            new PackingSessionRepository(connectionFactory),
            new BaseRepository<PackingItem>(connectionFactory),
            new BaseRepository<PackingLabel>(connectionFactory),
            new BaseRepository<PackingManifest>(connectionFactory),
            new M1OrderDataProvider(connectionFactory),
            new TestDietDataProvider(),
            new MockDeliveryManifestProvider(),
            mapper,
            NullLogger<PackingService>.Instance);
    }

    private static LoadingService CreateLoadingService(IDbConnectionFactory connectionFactory, IPackingService packingService)
    {
        var mapperConfiguration = new MapperConfiguration(
            cfg => cfg.AddProfile<ProductionProfile>(),
            NullLoggerFactory.Instance);
        var mapper = mapperConfiguration.CreateMapper();

        return new LoadingService(
            new PackingSessionRepository(connectionFactory),
            new BaseRepository<PackingLabel>(connectionFactory),
            new BaseRepository<PackingManifest>(connectionFactory),
            packingService,
            mapper,
            NullLogger<LoadingService>.Instance);
    }

    private static async Task<int> CreateStockItemAsync(
        IDbConnectionFactory connectionFactory,
        string nameSuffix,
        int warehouseCategoryId = 4)
    {
        using var db = connectionFactory.CreateConnection();

        var unit = new UnitOfMeasure
        {
            Symbol = $"kg-{Guid.NewGuid():N}"[..8],
            Name = $"Kilogram {nameSuffix}",
            Description = "Jednostka testowa",
        };

        var unitId = await db.QuerySingleAsync<int>(
            """
            INSERT INTO [UnitsOfMeasure] ([Symbol], [Name], [Description], [CreatedAt], [UpdatedAt])
            VALUES (@Symbol, @Name, @Description, @CreatedAt, @UpdatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            unit);

        var stockItem = new StockItem
        {
            Name = $"Skladnik {nameSuffix}",
            BaseIngredientId = 9000,
            DefaultUnitOfMeasureId = unitId,
            MinimumLevel = 5.25m,
            LeadTimeDays = 2,
            WarehouseCategoryId = warehouseCategoryId,
            CreatedBy = "IntegrationTest",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [StockItems]
                ([Name], [BaseIngredientId], [DefaultUnitOfMeasureId], [MinimumLevel], [LeadTimeDays],
                 [WarehouseCategoryId], [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            VALUES
                (@Name, @BaseIngredientId, @DefaultUnitOfMeasureId, @MinimumLevel, @LeadTimeDays,
                 @WarehouseCategoryId, @CreatedBy, @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy, @CreatedAt, @UpdatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            stockItem);
    }

    private static async Task<int> InsertBatchAsync(
        IDbConnectionFactory connectionFactory,
        int stockItemId,
        string supplierBatchNumber,
        DateTimeOffset? expiryDate,
        bool isDepleted,
        bool isDeleted,
        decimal currentQuantity = 100m)
    {
        using var db = connectionFactory.CreateConnection();

        var batch = new Batch
        {
            StockItemId = stockItemId,
            SupplierBatchNumber = supplierBatchNumber,
            CurrentQuantity = currentQuantity,
            ReceivedDate = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiryDate = expiryDate,
            IsDepleted = isDepleted,
            IsDeleted = isDeleted,
            CreatedBy = "IntegrationTest",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [Batches]
                ([StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [ReceivedDate], [IsDepleted],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            VALUES
                (@StockItemId, @SupplierBatchNumber, @CurrentQuantity, @ExpiryDate, @ReceivedDate, @IsDepleted,
                 @CreatedBy, @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy, @CreatedAt, @UpdatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            batch);
    }

    private static async Task InsertInventoryTransactionAsync(
        IDbConnectionFactory connectionFactory,
        int batchId,
        DateTimeOffset createdAt,
        string referenceDocument,
        int? stockItemId = null)
    {
        using var db = connectionFactory.CreateConnection();

        var row = new InventoryTransactionInsertRow
        {
            BatchId = batchId,
            StockItemId = stockItemId,
            TransactionType = (int)InventoryTransactionType.Receipt,
            QuantityChanged = 10m,
            Reason = "Test przedzialu dat",
            ReferenceDocument = referenceDocument,
            CreatedAt = createdAt,
        };

        await db.ExecuteAsync(
            """
            INSERT INTO [InventoryTransactions]
                ([BatchId], [StockItemId], [TransactionType], [QuantityChanged], [Reason], [ReferenceDocument], [CreatedAt])
            VALUES
                (@BatchId, @StockItemId, @TransactionType, @QuantityChanged, @Reason, @ReferenceDocument, @CreatedAt);
            """,
            row);
    }

    private sealed class InventoryTransactionInsertRow
    {
        public int BatchId { get; set; }

        public int? StockItemId { get; set; }

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

        await InsertPackingSessionAsync(db, date, PackingStatus.Pending, false);
        await InsertPackingSessionAsync(db, date, PackingStatus.Dispatched, false);
        await InsertPackingSessionAsync(db, date.AddDays(1), PackingStatus.Pending, false);
        await InsertPackingSessionAsync(db, date, PackingStatus.Pending, true);

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

        await InsertTemperatureLogAsync(db, "Cold room A", DateTimeOffset.UtcNow, false);
        await InsertTemperatureLogAsync(db, "Cold room B", DateTimeOffset.UtcNow, false);
        await InsertTemperatureLogAsync(db, "Cold room A", DateTimeOffset.UtcNow, true);

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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PackingService_GetPackingBoardAsync_ShouldCreateOneBagPerOrder_WithoutDuplicates()
    {
        var connectionFactory = CreateConnectionFactory();
        var deliveryDate = new DateOnly(2036, 4, 11);
        var seededOne = await SeedM1OrderAsync(connectionFactory, deliveryDate);
        var seededTwo = await SeedM1OrderAsync(connectionFactory, deliveryDate);
        var service = CreatePackingService(connectionFactory);

        var firstBoard = await service.GetPackingBoardAsync(deliveryDate);
        var secondBoard = await service.GetPackingBoardAsync(deliveryDate);

        var bags = secondBoard.Routes.SelectMany(r => r.Bags).ToList();
        bags.Should().ContainSingle(b => b.OrderId == seededOne.OrderId);
        bags.Should().ContainSingle(b => b.OrderId == seededTwo.OrderId);
        secondBoard.TotalBags.Should().Be(firstBoard.TotalBags);

        using var db = connectionFactory.CreateConnection();
        var sessionCount = await db.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM PackingSessions
            WHERE PackingDate = @deliveryDate
              AND OrderId IN @orderIds
              AND IsDeleted = 0;
            """,
            new
            {
                deliveryDate = deliveryDate.ToDateTime(TimeOnly.MinValue),
                orderIds = new[] { seededOne.OrderId, seededTwo.OrderId },
            });

        sessionCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PackingService_PackingOneBag_ShouldNotHideOtherOrders()
    {
        var connectionFactory = CreateConnectionFactory();
        var deliveryDate = new DateOnly(2036, 4, 12);
        var seededOne = await SeedM1OrderAsync(connectionFactory, deliveryDate);
        var seededTwo = await SeedM1OrderAsync(connectionFactory, deliveryDate);
        var service = CreatePackingService(connectionFactory);

        var board = await service.GetPackingBoardAsync(deliveryDate);
        var firstBag = board.Routes.SelectMany(r => r.Bags).Single(b => b.OrderId == seededOne.OrderId);
        var secondBag = board.Routes.SelectMany(r => r.Bags).Single(b => b.OrderId == seededTwo.OrderId);

        var boxes = (await service.PrepareOrderBoxesAsync(firstBag.PackingSessionId)).ToList();
        foreach (var box in boxes)
        {
            await service.MarkBoxPackedAsync(box.Id, "IntegrationTest");
        }

        await service.PackOrderBagAsync(firstBag.PackingSessionId, "IntegrationTest");
        await service.GenerateTransportLabelsAsync(firstBag.PackingSessionId);

        var refreshed = await service.GetPackingBoardAsync(deliveryDate);
        var refreshedBags = refreshed.Routes.SelectMany(r => r.Bags).ToList();

        refreshedBags.Should().ContainSingle(b =>
            b.OrderId == seededOne.OrderId &&
            b.Status == PackingStatus.Labeled.ToString());
        refreshedBags.Should().ContainSingle(b =>
            b.OrderId == seededTwo.OrderId &&
            b.PackingSessionId == secondBag.PackingSessionId &&
            b.Status != PackingStatus.Labeled.ToString() &&
            b.Status != PackingStatus.Packed.ToString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PackingService_GenerateTransportLabelsAsync_ShouldBeIdempotent_AndNotCreateFoilLabels()
    {
        var connectionFactory = CreateConnectionFactory();
        var deliveryDate = new DateOnly(2036, 4, 13);
        var seeded = await SeedM1OrderAsync(connectionFactory, deliveryDate);
        var service = CreatePackingService(connectionFactory);
        var board = await service.GetPackingBoardAsync(deliveryDate);
        var bag = board.Routes.SelectMany(r => r.Bags).Single(b => b.OrderId == seeded.OrderId);

        var boxes = (await service.PrepareOrderBoxesAsync(bag.PackingSessionId)).ToList();
        foreach (var box in boxes)
        {
            await service.MarkBoxPackedAsync(box.Id, "IntegrationTest");
        }
        await service.PackOrderBagAsync(bag.PackingSessionId, "IntegrationTest");

        var labelsOne = (await service.GenerateTransportLabelsAsync(bag.PackingSessionId)).ToList();
        var labelsTwo = (await service.GenerateTransportLabelsAsync(bag.PackingSessionId)).ToList();

        labelsTwo.Select(l => l.Id).Should().BeEquivalentTo(labelsOne.Select(l => l.Id));
        labelsTwo.Should().ContainSingle(l => l.LabelType == LabelType.Shipping.ToString());
        labelsTwo.Should().NotContain(l => l.LabelType == LabelType.Product.ToString());

        using var db = connectionFactory.CreateConnection();
        var labelCount = await db.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM PackingLabels l
            LEFT JOIN PackingItems i ON i.Id = l.PackingItemId
            WHERE l.PackingSessionId = @sessionId
               OR i.PackingSessionId = @sessionId;
            """,
            new { sessionId = bag.PackingSessionId });

        labelCount.Should().Be(labelsOne.Count);

        var foilPrintedCount = await db.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM PackingItems
            WHERE PackingSessionId = @sessionId
              AND Status = @foilPrintedStatus;
            """,
            new
            {
                sessionId = bag.PackingSessionId,
                foilPrintedStatus = (int)PackingItemStatus.FoilPrinted,
            });

        foilPrintedCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoadingService_GenerateManifestAsync_ShouldPersistJsonSnapshot()
    {
        var connectionFactory = CreateConnectionFactory();
        var deliveryDate = new DateOnly(2036, 4, 14);
        var seededOne = await SeedM1OrderAsync(connectionFactory, deliveryDate);
        var seededTwo = await SeedM1OrderAsync(connectionFactory, deliveryDate);
        var packingService = CreatePackingService(connectionFactory);
        var loadingService = CreateLoadingService(connectionFactory, packingService);

        var board = await packingService.GetPackingBoardAsync(deliveryDate);
        var route = board.Routes.Single(r => r.Bags.Any(b => b.OrderId == seededOne.OrderId));

        foreach (var bag in route.Bags)
        {
            var boxes = (await packingService.PrepareOrderBoxesAsync(bag.PackingSessionId)).ToList();
            foreach (var box in boxes)
            {
                await packingService.MarkBoxPackedAsync(box.Id, "IntegrationTest");
            }

            await packingService.PackOrderBagAsync(bag.PackingSessionId, "IntegrationTest");
        }

        var manifest = await loadingService.GenerateManifestAsync(deliveryDate, route.RouteId, "IntegrationTest");
        var latest = await loadingService.GetManifestAsync(deliveryDate, route.RouteId);

        latest.Should().NotBeNull();
        latest!.Id.Should().Be(manifest.Id);
        latest.RouteId.Should().Be(route.RouteId);
        latest.VehicleRegistration.Should().Be(route.VehicleRegistration);
        latest.BagCount.Should().Be(route.TotalBags);
        latest.PayloadJson.Should().Contain(seededOne.OrderId.ToString());
        latest.PayloadJson.Should().Contain(seededTwo.OrderId.ToString());

        using var document = JsonDocument.Parse(latest.PayloadJson);
        document.RootElement.GetProperty("manifestNumber").GetString().Should().Be(manifest.ManifestNumber);
        document.RootElement.GetProperty("route").GetProperty("RouteId").GetInt32().Should().Be(route.RouteId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoadingService_LoadOrderBagAsync_ShouldRequireVerifiedRouteManifest()
    {
        var connectionFactory = CreateConnectionFactory();
        var deliveryDate = new DateOnly(2036, 4, 15);
        var seeded = await SeedM1OrderAsync(connectionFactory, deliveryDate);
        var packingService = CreatePackingService(connectionFactory);
        var loadingService = CreateLoadingService(connectionFactory, packingService);
        var board = await packingService.GetPackingBoardAsync(deliveryDate);
        var route = board.Routes.Single(r => r.Bags.Any(b => b.OrderId == seeded.OrderId));
        var bag = route.Bags.Single(b => b.OrderId == seeded.OrderId);

        var boxes = (await packingService.PrepareOrderBoxesAsync(bag.PackingSessionId)).ToList();
        foreach (var box in boxes)
        {
            await packingService.MarkBoxPackedAsync(box.Id, "IntegrationTest");
        }

        await packingService.PackOrderBagAsync(bag.PackingSessionId, "IntegrationTest");

        var loadBeforeManifest = async () => await loadingService.LoadOrderBagAsync(bag.PackingSessionId);
        await loadBeforeManifest.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*manifestu*");

        await loadingService.GenerateManifestAsync(deliveryDate, route.RouteId, "IntegrationTest");
        await loadingService.VerifyManifestAsync(deliveryDate, route.RouteId, "IntegrationTest");
        await loadingService.LoadOrderBagAsync(bag.PackingSessionId);

        var refreshed = await packingService.GetSessionByIdAsync(bag.PackingSessionId);
        refreshed!.Status.Should().Be(PackingStatus.Loaded.ToString());
    }

    private static async Task InsertTemperatureLogAsync(
        System.Data.IDbConnection db,
        string location,
        DateTimeOffset recordedAt,
        bool isDeleted)
    {
        await db.ExecuteAsync(
            """
            INSERT INTO [TemperatureLogs]
                ([DeviceNameOrLocation], [RecordedTemperatureCelsius], [RecordedAt], [Remarks],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            VALUES
                (@location, @temperature, @recordedAt, NULL,
                 NULL, NULL, @isDeleted, NULL, NULL, @createdAt, NULL);
            """,
            new
            {
                location,
                temperature = 4.0m,
                recordedAt,
                isDeleted,
                createdAt = DateTimeOffset.UtcNow,
            });
    }

    private static async Task<int> InsertPackingSessionAsync(
        System.Data.IDbConnection db,
        DateOnly packingDate,
        PackingStatus status,
        bool isDeleted)
    {
        return await db.QuerySingleAsync<int>(
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
        await db.ExecuteAsync(
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

        var userId = await db.QuerySingleAsync<int>(
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

        var addressId = await db.QuerySingleAsync<int>(
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

        var orderId = await db.QuerySingleAsync<int>(
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
        await db.ExecuteAsync(
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

        await db.ExecuteAsync(
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

    private sealed class TestDietDataProvider : IDietDataProvider
    {
        public Task<IEnumerable<DietPlanEntry>> Get7DayPlanAsync(DateOnly startDate)
        {
            IEnumerable<DietPlanEntry> entries = new[]
            {
                new DietPlanEntry
                {
                    PlanDate = startDate,
                    PlanStatus = "Published",
                    MealId = 801,
                    MealName = "Test breakfast",
                    DietVariantId = 501,
                    MealSlot = "Breakfast",
                    SortOrder = 1,
                    ServingMultiplier = 1.0m,
                    ServingWeightGrams = 300m,
                },
                new DietPlanEntry
                {
                    PlanDate = startDate,
                    PlanStatus = "Published",
                    MealId = 802,
                    MealName = "Test dinner",
                    DietVariantId = 501,
                    MealSlot = "Dinner",
                    SortOrder = 2,
                    ServingMultiplier = 1.0m,
                    ServingWeightGrams = 450m,
                },
            };

            return Task.FromResult(entries);
        }

        public Task<IEnumerable<DietPlanEntry>> GetPlanForDateAsync(DateOnly date)
        {
            return Get7DayPlanAsync(date);
        }

        public Task<IEnumerable<RecipeIngredientEntry>> GetRecipeForMealAsync(int mealId)
        {
            return Task.FromResult(Enumerable.Empty<RecipeIngredientEntry>());
        }

        public Task<MealCookingDetailsEntry?> GetMealCookingDetailsAsync(int mealId)
        {
            return Task.FromResult<MealCookingDetailsEntry?>(new MealCookingDetailsEntry
            {
                MealId = mealId,
                MealName = $"Meal {mealId}",
            });
        }
    }
}
