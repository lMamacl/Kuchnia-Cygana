using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.Services.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class DietManagementServiceTests
{
    [Fact]
    public async Task AssignMealToVariantAsync_SavesAssignment_WhenVariantBelongsToDiet()
    {
        var variantRepository = new Mock<IDietVariantRepository>();
        variantRepository.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(new DietVariant
        {
            Id = 2,
            DietId = 1,
        });
        var assignmentRepository = new Mock<IDietVariantMealRepository>();
        var service = CreateService(variantRepository, assignmentRepository);

        await service.AssignMealToVariantAsync(1, 2, 5, 1.25m, 3);

        assignmentRepository.Verify(r => r.UpsertAsync(It.Is<DietVariantMeal>(assignment =>
            assignment.DietVariantId == 2
            && assignment.MealId == 5
            && assignment.ServingSizeMultiplier == 1.25m
            && assignment.SortOrder == 3)), Times.Once);
    }

    [Fact]
    public async Task AssignMealToVariantAsync_RejectsVariantFromDifferentDiet()
    {
        var variantRepository = new Mock<IDietVariantRepository>();
        variantRepository.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(new DietVariant
        {
            Id = 2,
            DietId = 99,
        });
        var assignmentRepository = new Mock<IDietVariantMealRepository>();
        var service = CreateService(variantRepository, assignmentRepository);

        var act = async () => await service.AssignMealToVariantAsync(1, 2, 5, 1m, 3);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nie nalezy do wskazanej diety*");
        assignmentRepository.Verify(r => r.UpsertAsync(It.IsAny<DietVariantMeal>()), Times.Never);
    }

    private static DietManagementService CreateService(
        Mock<IDietVariantRepository> variantRepository,
        Mock<IDietVariantMealRepository> assignmentRepository)
    {
        return new DietManagementService(
            Mock.Of<IDietRepository>(),
            variantRepository.Object,
            assignmentRepository.Object,
            Mock.Of<IMapper>());
    }
}
