using FluentAssertions;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Interfaces.External;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class DietOrderingServiceTests
{
    [Fact]
    public async Task CreateCartItemAsync_BuildsCartItemFromDietCatalogProvider()
    {
        var provider = new Mock<IDietCatalogProvider>();
        provider.Setup(p => p.GetVariantAsync(10)).ReturnsAsync(new DietCatalogVariantDto
        {
            DietVariantId = 10,
            DietId = 2,
            Name = "2200 kcal",
            TargetCalories = 2200,
            PriceMultiplier = 1.10m,
            IsAvailable = true,
        });
        provider.Setup(p => p.GetDietAsync(2)).ReturnsAsync(new DietCatalogItemDto
        {
            DietId = 2,
            Name = "Dieta Sport",
            Status = "Published",
            IsActive = true,
        });

        var service = new DietOrderingService(provider.Object);

        var item = await service.CreateCartItemAsync(10, 7);

        item.DietId.Should().Be(2);
        item.DietVariantId.Should().Be(10);
        item.DietName.Should().Be("Dieta Sport");
        item.VariantName.Should().Be("2200 kcal");
        item.CaloriesPerDay.Should().Be(2200);
        item.PricePerDay.Should().Be(65.99m);
        item.TotalDays.Should().Be(7);
    }

    [Fact]
    public async Task CreateCartItemAsync_RejectsUnavailableVariant()
    {
        var provider = new Mock<IDietCatalogProvider>();
        provider.Setup(p => p.GetVariantAsync(10)).ReturnsAsync(new DietCatalogVariantDto
        {
            DietVariantId = 10,
            DietId = 2,
            Name = "2200 kcal",
            TargetCalories = 2200,
            PriceMultiplier = 1.10m,
            IsAvailable = false,
        });

        var service = new DietOrderingService(provider.Object);

        var act = () => service.CreateCartItemAsync(10, 7);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Wybrany wariant diety nie jest aktualnie dostepny.");
    }
}
