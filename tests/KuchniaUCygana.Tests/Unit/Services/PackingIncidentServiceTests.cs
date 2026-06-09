using FluentAssertions;
using KuchniaUCygana.Application.Configuration;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class PackingIncidentServiceTests
{
    [Fact]
    public async Task CreateItemIssueAsync_ShouldRegisterWasteForContainerAndRecipeIngredients_WhenItemIsDamaged()
    {
        // Arrange
        var incidentRepository = new Mock<IPackingIncidentRepository>();
        var sessionRepository = new Mock<IPackingSessionRepository>();
        var bagRepository = new Mock<IPackingBagRepository>();
        var itemRepository = new Mock<IRepository<PackingItem>>();
        var packingService = new Mock<IPackingService>();
        var warehouseService = new Mock<IWarehouseService>();
        var notificationService = new Mock<INotificationService>();
        var currentUserService = new Mock<ICurrentUserService>();
        var dietDataProvider = new Mock<IDietDataProvider>();

        var options = new PackingResourcesOptions
        {
            BoxContainerStockItemId = 99,
            BoxContainerWasteQuantity = 1m
        };
        var optionsMock = new Mock<IOptions<PackingResourcesOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        var service = new PackingIncidentService(
            incidentRepository.Object,
            sessionRepository.Object,
            bagRepository.Object,
            itemRepository.Object,
            packingService.Object,
            warehouseService.Object,
            notificationService.Object,
            currentUserService.Object,
            dietDataProvider.Object,
            optionsMock.Object,
            NullLogger<PackingIncidentService>.Instance);

        var packingItem = new PackingItem
        {
            Id = 1,
            PackingSessionId = 100,
            MealId = 10,
            MealName = "Kurczak z ryżem",
            BoxCode = "BOX-001"
        };

        var packingSession = new PackingSession
        {
            Id = 100,
            PackingDate = DateOnly.FromDateTime(DateTime.Today)
        };

        var replacementItem = new PackingItemDto
        {
            Id = 2,
            BoxCode = "BOX-002"
        };

        itemRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(packingItem);
        sessionRepository.Setup(r => r.GetWithItemsAsync(100)).ReturnsAsync(packingSession);
        packingService.Setup(r => r.ReportPackingItemIssueAsync(It.IsAny<ReportPackingItemIssueRequest>()))
            .ReturnsAsync(replacementItem);

        var recipeIngredients = new List<RecipeIngredientEntry>
        {
            new() { StockItemId = 20, WeightInGrams = 150m }, // Kurczak, stock unit kg
            new() { StockItemId = 30, WeightInGrams = 50m }   // Ryż, stock unit kg
        };
        dietDataProvider.Setup(d => d.GetRecipeForMealAsync(10)).ReturnsAsync(recipeIngredients);

        warehouseService.Setup(w => w.GetStockLookupByIdAsync(20))
            .ReturnsAsync(new StockItemDto { Id = 20, UnitSymbol = "kg" });
        warehouseService.Setup(w => w.GetStockLookupByIdAsync(30))
            .ReturnsAsync(new StockItemDto { Id = 30, UnitSymbol = "kg" });

        // Act
        var result = await service.CreateItemIssueAsync(new CreatePackingItemIssueRequest
        {
            PackingItemId = 1,
            ReasonFlags = new[] { PackingIncidentReasonFlag.BoxDamaged },
            Description = "Pęknięte pudełko"
        });

        // Assert
        result.Should().NotBeNull();
        
        // Verify container waste registration
        warehouseService.Verify(w => w.RegisterWasteAsync(It.Is<RegisterWasteRequest>(r =>
            r.StockItemId == 99 && r.Quantity == 1m)), Times.Once);

        // Verify ingredient 1 waste registration (150g = 0.15kg)
        warehouseService.Verify(w => w.RegisterWasteAsync(It.Is<RegisterWasteRequest>(r =>
            r.StockItemId == 20 && r.Quantity == 0.15m)), Times.Once);

        // Verify ingredient 2 waste registration (50g = 0.05kg)
        warehouseService.Verify(w => w.RegisterWasteAsync(It.Is<RegisterWasteRequest>(r =>
            r.StockItemId == 30 && r.Quantity == 0.05m)), Times.Once);
    }
}
