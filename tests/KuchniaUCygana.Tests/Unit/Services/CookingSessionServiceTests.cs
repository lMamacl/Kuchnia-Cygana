using System.Text.Json;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class CookingSessionServiceTests
{
    [Fact]
    public async Task StartComponentSessionAsync_ShouldRequireFefoDeduction()
    {
        var harness = CreateHarness(new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            M2SnapshotJson = CreateSnapshotJson(),
        });

        var act = () => harness.Service.StartComponentSessionAsync(21, 501, "Chef");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FEFO*");
        harness.SessionRepository.Verify(repository => repository.InsertAsync(It.IsAny<CookingSession>()), Times.Never);
    }

    [Fact]
    public async Task StartComponentSessionAsync_ShouldCreateSession_AndMovePlanItemToCooking()
    {
        var item = new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            Status = ProductionItemStatus.Planned,
            FefoDeductedAt = DateTimeOffset.UtcNow,
            M2SnapshotJson = CreateSnapshotJson(),
        };
        var harness = CreateHarness(item);
        CookingSession? inserted = null;
        harness.SessionRepository
            .Setup(repository => repository.InsertAsync(It.IsAny<CookingSession>()))
            .Callback<CookingSession>(session => inserted = session)
            .ReturnsAsync(7);

        var session = await harness.Service.StartComponentSessionAsync(21, 501, "Chef");

        session.Status.Should().Be("InProgress");
        inserted.Should().NotBeNull();
        inserted!.ProductionPlanItemId.Should().Be(21);
        inserted.RecipeComponentVersionId.Should().Be(501);
        inserted.StartedBy.Should().Be("Chef");
        item.Status.Should().Be(ProductionItemStatus.Cooking);
        harness.ItemRepository.Verify(repository => repository.UpdateAsync(item), Times.Once);
    }

    [Fact]
    public async Task ToggleStepAsync_ShouldPersistCheckedStepWithControlValue()
    {
        var harness = CreateHarness(new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            FefoDeductedAt = DateTimeOffset.UtcNow,
            M2SnapshotJson = CreateSnapshotJson(),
        });
        harness.SessionRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new CookingSession
                {
                    Id = 7,
                    ProductionPlanItemId = 21,
                    RecipeComponentVersionId = 501,
                    Status = "InProgress",
                },
            });
        CookingSessionStepCheck? inserted = null;
        harness.StepCheckRepository
            .Setup(repository => repository.InsertAsync(It.IsAny<CookingSessionStepCheck>()))
            .Callback<CookingSessionStepCheck>(check => inserted = check)
            .ReturnsAsync(13);

        await harness.Service.ToggleStepAsync(new ToggleCookingStepRequest
        {
            ProductionPlanItemId = 21,
            RecipeComponentVersionId = 501,
            StepId = 711,
            IsChecked = true,
            OperatorName = "Chef",
            ActualValue = 76m,
            ActualUnit = "C",
            Notes = "OK",
        });

        inserted.Should().NotBeNull();
        inserted!.CookingSessionId.Should().Be(7);
        inserted.RecipeComponentInstructionStepId.Should().Be(711);
        inserted.Status.Should().Be("Checked");
        inserted.CheckedAt.Should().NotBeNull();
        inserted.CheckedBy.Should().Be("Chef");
        inserted.ActualValue.Should().Be(76m);
        inserted.ActualUnit.Should().Be("C");
        inserted.Notes.Should().Be("OK");
    }

    [Fact]
    public async Task GetComponentSessionAsync_ShouldReturnCheckedSteps_AfterReload()
    {
        var harness = CreateHarness(new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            FefoDeductedAt = DateTimeOffset.UtcNow,
            M2SnapshotJson = CreateSnapshotJson(),
        });
        harness.SessionRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new CookingSession
                {
                    Id = 7,
                    ProductionPlanItemId = 21,
                    RecipeComponentVersionId = 501,
                    Status = "InProgress",
                    StartedBy = "Chef",
                    StartedAt = DateTimeOffset.UtcNow,
                },
            });
        harness.StepCheckRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new CookingSessionStepCheck
                {
                    Id = 13,
                    CookingSessionId = 7,
                    RecipeComponentInstructionStepId = 711,
                    Status = "Checked",
                    CheckedBy = "Chef",
                    CheckedAt = DateTimeOffset.UtcNow,
                    ActualValue = 76m,
                    ActualUnit = "C",
                    Notes = "OK",
                },
            });

        var session = await harness.Service.GetComponentSessionAsync(21, 501);

        session.Status.Should().Be("InProgress");
        session.StepChecksByStepId.Should().ContainKey(711);
        session.StepChecksByStepId[711].IsChecked.Should().BeTrue();
        session.StepChecksByStepId[711].ActualValue.Should().Be(76m);
    }

    [Fact]
    public async Task ToggleStepAsync_ShouldClearCheckedStep_WhenUnchecked()
    {
        var existingCheck = new CookingSessionStepCheck
        {
            Id = 13,
            CookingSessionId = 7,
            RecipeComponentInstructionStepId = 711,
            Status = "Checked",
            CheckedAt = DateTimeOffset.UtcNow,
            CheckedBy = "Chef",
            ActualValue = 76m,
            ActualUnit = "C",
            Notes = "OK",
        };
        var harness = CreateHarness(new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            FefoDeductedAt = DateTimeOffset.UtcNow,
            M2SnapshotJson = CreateSnapshotJson(),
        });
        harness.SessionRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new CookingSession
                {
                    Id = 7,
                    ProductionPlanItemId = 21,
                    RecipeComponentVersionId = 501,
                    Status = "InProgress",
                },
            });
        harness.StepCheckRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[] { existingCheck });

        await harness.Service.ToggleStepAsync(new ToggleCookingStepRequest
        {
            ProductionPlanItemId = 21,
            RecipeComponentVersionId = 501,
            StepId = 711,
            IsChecked = false,
            OperatorName = "Chef",
        });

        existingCheck.Status.Should().Be("Pending");
        existingCheck.CheckedAt.Should().BeNull();
        existingCheck.CheckedBy.Should().BeNull();
        existingCheck.ActualValue.Should().BeNull();
        existingCheck.ActualUnit.Should().BeNull();
        existingCheck.Notes.Should().BeNull();
        harness.StepCheckRepository.Verify(repository => repository.UpdateAsync(existingCheck), Times.Once);
    }

    [Fact]
    public async Task CompleteComponentSessionAsync_ShouldRequireCriticalAndControlSteps()
    {
        var harness = CreateHarness(new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            FefoDeductedAt = DateTimeOffset.UtcNow,
            M2SnapshotJson = CreateSnapshotJson(),
        });
        harness.SessionRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new CookingSession
                {
                    Id = 7,
                    ProductionPlanItemId = 21,
                    RecipeComponentVersionId = 501,
                    Status = "InProgress",
                },
            });
        harness.StepCheckRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(Array.Empty<CookingSessionStepCheck>());

        var act = () => harness.Service.CompleteComponentSessionAsync(21, 501, "Chef");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*wymagane kroki*");
    }

    private static Harness CreateHarness(ProductionPlanItem item)
    {
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(item.Id))
            .ReturnsAsync(item);

        var sessionRepository = new Mock<IRepository<CookingSession>>();
        sessionRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(Array.Empty<CookingSession>());

        var stepCheckRepository = new Mock<IRepository<CookingSessionStepCheck>>();
        stepCheckRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(Array.Empty<CookingSessionStepCheck>());

        return new Harness(
            new CookingSessionService(
                itemRepository.Object,
                sessionRepository.Object,
                stepCheckRepository.Object),
            itemRepository,
            sessionRepository,
            stepCheckRepository);
    }

    private static string CreateSnapshotJson()
        => JsonSerializer.Serialize(new PublishedDietPlanItemDto
        {
            DietMenuPlanItemId = 1001,
            PlanDate = new DateOnly(2026, 6, 5),
            MealId = 10,
            MealName = "Makaron standard",
            DietVariantId = 1,
            Components = new[]
            {
                new MealComponentVersionDto
                {
                    RecipeComponentId = 501,
                    RecipeComponentVersionId = 501,
                    ComponentName = "Sos",
                    VersionNumber = 1,
                    VersionStatus = "Published",
                    QuantityPerServing = 1m,
                    Unit = "portion",
                    InstructionSections = new[]
                    {
                        new ComponentInstructionSectionDto
                        {
                            SectionId = 71,
                            Title = "Kontrola",
                            SortOrder = 1,
                            Steps = new[]
                            {
                                new ComponentInstructionStepDto
                                {
                                    StepId = 711,
                                    StepText = "Sprawdz temperature rdzenia.",
                                    SortOrder = 1,
                                    RequiresControl = true,
                                    ControlType = "CoreTemperature",
                                    ExpectedValue = 75m,
                                    ExpectedUnit = "C",
                                    IsCritical = true,
                                },
                            },
                        },
                    },
                },
            },
        });

    private sealed record Harness(
        CookingSessionService Service,
        Mock<IRepository<ProductionPlanItem>> ItemRepository,
        Mock<IRepository<CookingSession>> SessionRepository,
        Mock<IRepository<CookingSessionStepCheck>> StepCheckRepository);
}
