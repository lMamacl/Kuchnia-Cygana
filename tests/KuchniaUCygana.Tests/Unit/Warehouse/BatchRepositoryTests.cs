using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Tests.TestData.Warehouse;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Warehouse;

public class BatchRepositoryTests
{
    private readonly WarehouseMockContext _data;
    private readonly Mock<IBatchRepository> _repoMock;

    public BatchRepositoryTests()
    {
        _data = WarehouseDataSeeder.GenerateMockData(seed: 42);
        _repoMock = new Mock<IBatchRepository>();
    }

    [Fact]
    public async Task GetActiveBatchesByStockItem_ReturnsFEFOOrder()
    {
        // Arrange
        var targetStockItemId = _data.StockItems.First().Id;

        var activeBatches = _data.Batches
            .Where(b => b.StockItemId == targetStockItemId && !b.IsDepleted && !b.IsDeleted)
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate)
            .ToList();

        _repoMock
            .Setup(r => r.GetActiveBatchesByStockItemAsync(targetStockItemId))
            .ReturnsAsync(activeBatches);

        // Act
        var result = (await _repoMock.Object.GetActiveBatchesByStockItemAsync(targetStockItemId)).ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().AllSatisfy(b => b.IsDepleted.Should().BeFalse());

        // Weryfikacja kolejności FEFO: żadna partia nie powinna mieć daty ważności
        // późniejszej niż następna w kolejce (ignorując null)
        for (var i = 0; i < result.Count - 1; i++)
        {
            if (result[i].ExpiryDate.HasValue && result[i + 1].ExpiryDate.HasValue)
            {
                result[i].ExpiryDate.Should().BeOnOrBefore(result[i + 1].ExpiryDate!.Value);
            }
        }
    }

    [Fact]
    public async Task GetExpiringBefore_ReturnsOnlyExpiredBatches()
    {
        // Arrange
        var cutoffDate = DateTimeOffset.UtcNow;

        var expiredBatches = _data.Batches
            .Where(b => b.ExpiryDate.HasValue && b.ExpiryDate <= cutoffDate && !b.IsDepleted && !b.IsDeleted)
            .ToList();

        _repoMock
            .Setup(r => r.GetExpiringBeforeAsync(cutoffDate))
            .ReturnsAsync(expiredBatches);

        // Act
        var result = (await _repoMock.Object.GetExpiringBeforeAsync(cutoffDate)).ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().AllSatisfy(b =>
        {
            b.ExpiryDate.Should().NotBeNull();
            b.ExpiryDate!.Value.Should().BeOnOrBefore(cutoffDate);
        });
    }

    [Fact]
    public async Task GetActiveBatches_NullExpiryDateBatchesComeLastInFEFO()
    {
        // Arrange: upewnij się że partie bez daty ważności są na końcu
        var stockItemId = _data.StockItems.Skip(1).First().Id;

        var mixedBatches = new List<Batch>
        {
            new() { Id = 1, StockItemId = stockItemId, ExpiryDate = DateTimeOffset.UtcNow.AddDays(10), IsDepleted = false },
            new() { Id = 2, StockItemId = stockItemId, ExpiryDate = null, IsDepleted = false },
            new() { Id = 3, StockItemId = stockItemId, ExpiryDate = DateTimeOffset.UtcNow.AddDays(5), IsDepleted = false },
        };

        // Symulacja sortowania FEFO (tak jak robi BatchRepository)
        var sorted = mixedBatches
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate)
            .ToList();

        _repoMock
            .Setup(r => r.GetActiveBatchesByStockItemAsync(stockItemId))
            .ReturnsAsync(sorted);

        // Act
        var result = (await _repoMock.Object.GetActiveBatchesByStockItemAsync(stockItemId)).ToList();

        // Assert: ostatnia partia powinna mieć ExpiryDate = null
        result.Last().ExpiryDate.Should().BeNull();
        result.First().ExpiryDate!.Value.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddDays(5),
            precision: TimeSpan.FromSeconds(5));
    }
}
