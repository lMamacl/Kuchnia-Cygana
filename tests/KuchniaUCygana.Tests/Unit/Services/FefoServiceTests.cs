using FluentAssertions;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Domain.Services;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class FefoServiceTests
{
    [Fact]
    public async Task DeductByFefoAsync_ShouldUseTransactionalWarehouseCommand_WithIdempotentReference()
    {
        WarehouseDeductionCommand? captured = null;
        var warehouse = new Mock<IWarehouseCommandRepository>();
        warehouse
            .Setup(repository => repository.DeductStockAsync(It.IsAny<WarehouseDeductionCommand>()))
            .Callback<WarehouseDeductionCommand>(command => captured = command)
            .ReturnsAsync(new[]
            {
                new InventoryTransaction
                {
                    BatchId = 11,
                    StockItemId = 5,
                    QuantityChanged = -3m,
                    TransactionType = InventoryTransactionType.ProductionIssue,
                },
            });
        var service = new FefoService(Mock.Of<IBatchRepository>(), warehouse.Object);

        var result = await service.DeductByFefoAsync(
            stockItemId: 5,
            requiredQuantity: 3m,
            reason: "Produkcja",
            referenceDocument: "PLAN-1-ITEM-2");

        result.IsFullyDeducted.Should().BeTrue();
        result.TotalDeducted.Should().Be(3m);
        captured.Should().NotBeNull();
        captured!.StockItemId.Should().Be(5);
        captured.Quantity.Should().Be(3m);
        captured.TransactionType.Should().Be(InventoryTransactionType.ProductionIssue);
        captured.ReferenceDocument.Should().Be("PLAN-1-ITEM-2");
        captured.PerformedBy.Should().Be("Production");
        captured.SkipIfReferenceDocumentExists.Should().BeTrue();
        captured.RequireFullQuantity.Should().BeTrue();
    }

    [Fact]
    public async Task DeductByFefoCategoryAsync_ShouldUseCategoryWarehouseCommand()
    {
        WarehouseCategoryDeductionCommand? captured = null;
        var warehouse = new Mock<IWarehouseCommandRepository>();
        warehouse
            .Setup(repository => repository.DeductStockByCategoryAsync(It.IsAny<WarehouseCategoryDeductionCommand>()))
            .Callback<WarehouseCategoryDeductionCommand>(command => captured = command)
            .ReturnsAsync(new[]
            {
                new InventoryTransaction
                {
                    BatchId = 22,
                    StockItemId = 8,
                    QuantityChanged = -2m,
                    TransactionType = InventoryTransactionType.ProductionIssue,
                },
            });
        var service = new FefoService(Mock.Of<IBatchRepository>(), warehouse.Object);

        var result = await service.DeductByFefoCategoryAsync(
            warehouseCategoryId: 4,
            requiredQuantity: 2m,
            reason: "Produkcja kategorii",
            referenceDocument: "PLAN-1-ITEM-3");

        result.IsFullyDeducted.Should().BeTrue();
        result.TotalDeducted.Should().Be(2m);
        captured.Should().NotBeNull();
        captured!.WarehouseCategoryId.Should().Be(4);
        captured.Quantity.Should().Be(2m);
        captured.ReferenceDocument.Should().Be("PLAN-1-ITEM-3");
        captured.PerformedBy.Should().Be("Production");
        captured.SkipIfReferenceDocumentExists.Should().BeTrue();
    }

    [Fact]
    public async Task DeductByFefoAsync_ShouldTreatEmptyResultWithReference_AsIdempotentSkip()
    {
        var warehouse = new Mock<IWarehouseCommandRepository>();
        warehouse
            .Setup(repository => repository.DeductStockAsync(It.IsAny<WarehouseDeductionCommand>()))
            .ReturnsAsync(Array.Empty<InventoryTransaction>());
        var service = new FefoService(Mock.Of<IBatchRepository>(), warehouse.Object);

        var result = await service.DeductByFefoAsync(
            stockItemId: 5,
            requiredQuantity: 7m,
            reason: "Ponowienie",
            referenceDocument: "PLAN-1-ITEM-2");

        result.IsFullyDeducted.Should().BeTrue();
        result.TotalDeducted.Should().Be(7m);
        result.Shortage.Should().Be(0m);
        result.Deductions.Should().BeEmpty();
    }
}
