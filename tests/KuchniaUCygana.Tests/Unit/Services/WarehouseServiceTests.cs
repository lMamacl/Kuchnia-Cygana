using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Mappings;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class WarehouseServiceTests
{
    [Fact]
    public async Task IssueManualAsync_ShouldUseOperationKeyAsIdempotentReference()
    {
        var commandRepository = new Mock<IWarehouseCommandRepository>();
        commandRepository
            .Setup(repository => repository.DeductStockAsync(It.IsAny<WarehouseDeductionCommand>()))
            .ReturnsAsync(Array.Empty<InventoryTransaction>());
        var service = CreateService(commandRepository: commandRepository);

        await service.IssueManualAsync(new ManualIssueRequest
        {
            StockItemId = 10,
            Quantity = 2m,
            Reason = "Test",
            IssuedTo = "Kuchnia",
            OperationKey = "WH-ISS-test",
        });

        commandRepository.Verify(repository => repository.DeductStockAsync(
            It.Is<WarehouseDeductionCommand>(command =>
                command.ReferenceDocument == "WH-ISS-test" &&
                command.SkipIfReferenceDocumentExists &&
                command.TransactionType == InventoryTransactionType.ManualIssue)),
            Times.Once);
    }

    [Fact]
    public async Task RegisterWasteAsync_ShouldUseOperationKeyAsIdempotentReference()
    {
        var commandRepository = new Mock<IWarehouseCommandRepository>();
        commandRepository
            .Setup(repository => repository.DeductStockAsync(It.IsAny<WarehouseDeductionCommand>()))
            .ReturnsAsync(Array.Empty<InventoryTransaction>());
        var service = CreateService(commandRepository: commandRepository);

        await service.RegisterWasteAsync(new RegisterWasteRequest
        {
            StockItemId = 10,
            Quantity = 1m,
            Reason = "Przeterminowanie",
            OperationKey = "WH-WST-test",
        });

        commandRepository.Verify(repository => repository.DeductStockAsync(
            It.Is<WarehouseDeductionCommand>(command =>
                command.ReferenceDocument == "WH-WST-test" &&
                command.SkipIfReferenceDocumentExists &&
                command.TransactionType == InventoryTransactionType.Waste)),
            Times.Once);
    }

    [Fact]
    public async Task ReceiveDeliveryAsync_ShouldUseOperationKeyAsIdempotentReference()
    {
        var commandRepository = new Mock<IWarehouseCommandRepository>();
        commandRepository
            .Setup(repository => repository.ReceiveDeliveryAsync(
                It.IsAny<Batch>(),
                It.IsAny<InventoryTransaction>(),
                It.IsAny<bool>()))
            .ReturnsAsync((Batch batch, InventoryTransaction transaction, bool skip) =>
            {
                _ = transaction;
                _ = skip;
                batch.Id = 55;
                return batch;
            });
        var service = CreateService(commandRepository: commandRepository);

        var batch = await service.ReceiveDeliveryAsync(new ReceiveDeliveryRequest
        {
            StockItemId = 10,
            SupplierBatchNumber = "BATCH-1",
            Quantity = 5m,
            OperationKey = "WH-RCV-test",
            InvoiceNumber = "FV-1",
        });

        batch.Id.Should().Be(55);
        commandRepository.Verify(repository => repository.ReceiveDeliveryAsync(
            It.IsAny<Batch>(),
            It.Is<InventoryTransaction>(transaction => transaction.ReferenceDocument == "WH-RCV-test"),
            true),
            Times.Once);
    }

    private static WarehouseService CreateService(Mock<IWarehouseCommandRepository> commandRepository)
    {
        var stockItemRepository = new Mock<IStockItemRepository>();
        stockItemRepository
            .Setup(repository => repository.GetByIdAsync(10))
            .ReturnsAsync(new StockItem { Id = 10, Name = "Ryż" });
        var batchRepository = new Mock<IBatchRepository>();
        var mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<WarehouseProfile>(),
            NullLoggerFactory.Instance).CreateMapper();

        return new WarehouseService(
            batchRepository.Object,
            stockItemRepository.Object,
            Mock.Of<IWarehouseCategoryRepository>(),
            Mock.Of<IInventoryTransactionRepository>(),
            commandRepository.Object,
            Mock.Of<IRepository<UnitOfMeasure>>(),
            Mock.Of<IBatchExpiryChangeLogRepository>(),
            Mock.Of<ICurrentUserService>(service => service.GetUserName() == "Tester"),
            new FefoService(batchRepository.Object, commandRepository.Object),
            new SmartInventoryAnalyzer(stockItemRepository.Object),
            mapper,
            NullLogger<WarehouseService>.Instance);
    }
}
