using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Controllers;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Web;

public sealed class ProductionControllerTests
{
    [Fact]
    public async Task Index_ShouldUsePagedKitchenDashboardFilter()
    {
        var filter = new KitchenDashboardFilterDto
        {
            Date = new DateOnly(2026, 6, 4),
            Search = "ryz",
            Status = "Cooking",
            Page = 2,
            PageSize = 50,
        };
        var dashboard = new KitchenDashboardDto
        {
            Filter = filter,
            Plan = new ProductionPlanDto { Id = 12, ProductionDate = filter.Date },
        };
        var productionService = new Mock<IProductionService>();
        productionService
            .Setup(service => service.GetKitchenDashboardAsync(It.Is<KitchenDashboardFilterDto>(f =>
                f.Date == filter.Date &&
                f.Search == filter.Search &&
                f.Status == filter.Status &&
                f.Page == filter.Page &&
                f.PageSize == filter.PageSize)))
            .ReturnsAsync(dashboard);
        var controller = CreateController(productionService);

        var result = await controller.Index(filter);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductionDashboardViewModel>().Subject;
        model.SelectedDate.Should().Be(filter.Date);
        model.Dashboard.Should().BeSameAs(dashboard);
        model.DailyPlan.Should().BeSameAs(dashboard.Plan);
    }

    [Fact]
    public async Task FoilPrinting_ShouldUsePagedFoilDashboardFilter()
    {
        var filter = new FoilLabelFilterDto
        {
            Date = new DateOnly(2026, 6, 4),
            Search = "box",
            LabelState = "missing",
            Page = 3,
            PageSize = 25,
        };
        var dashboard = new FoilLabelDashboardDto { Filter = filter };
        var packingService = new Mock<IPackingService>();
        packingService
            .Setup(service => service.EnsureFoilBoxesForDateAsync(filter.Date))
            .ReturnsAsync(new FoilLabelPreparationResultDto());
        packingService
            .Setup(service => service.GetFoilLabelDashboardAsync(It.Is<FoilLabelFilterDto>(f =>
                f.Date == filter.Date &&
                f.Search == filter.Search &&
                f.LabelState == filter.LabelState &&
                f.Page == filter.Page &&
                f.PageSize == filter.PageSize)))
            .ReturnsAsync(dashboard);
        var synchronizationService = new Mock<IPackingSynchronizationService>();
        synchronizationService
            .Setup(service => service.EnsureSessionsForDateAsync(filter.Date, It.IsAny<string>()))
            .ReturnsAsync(new PackingSynchronizationResultDto { Date = filter.Date });
        var controller = CreateController(
            new Mock<IProductionService>(),
            packingService: packingService,
            packingSynchronizationService: synchronizationService);

        var result = await controller.FoilPrinting(filter);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeSameAs(dashboard);
        synchronizationService.Verify(
            service => service.EnsureSessionsForDateAsync(filter.Date, It.IsAny<string>()),
            Times.Once);
        packingService.Verify(
            service => service.EnsureFoilBoxesForDateAsync(filter.Date),
            Times.Once);
    }

    [Fact]
    public async Task ApproveCooking_ShouldRedirectWithError_WhenServiceReportsShortage()
    {
        const int PlanItemId = 42;
        const decimal ActualQuantity = 5m;
        const string ErrorMessage = "Nie mozna zatwierdzic gotowania. Braki opakowan.";

        var productionService = new Mock<IProductionService>();
        productionService
            .Setup(service => service.ApproveCookingAsync(PlanItemId, ActualQuantity))
            .ThrowsAsync(new InvalidOperationException(ErrorMessage));
        var controller = CreateController(productionService);

        var result = await controller.ApproveCooking(PlanItemId, ActualQuantity);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ProductionController.CookingCard));
        redirect.RouteValues.Should().ContainKey("planItemId").WhoseValue.Should().Be(PlanItemId);
        controller.TempData["Error"].Should().Be(ErrorMessage);
        controller.TempData["Success"].Should().BeNull();
    }

    [Fact]
    public async Task RefreshM2PlanDay_ShouldCallProductionRefreshAndReturnToM2Plan()
    {
        var date = new DateOnly(2026, 6, 8);
        var productionService = new Mock<IProductionService>();
        productionService
            .Setup(service => service.RefreshProductionPlanFromM2Async(date, It.IsAny<string>()))
            .ReturnsAsync(new ProductionPlanRefreshResultDto
            {
                ProductionDate = date,
                ProductionPlanId = 700,
                Status = "Refreshed",
                ItemCount = 2,
            });
        var controller = CreateController(productionService);

        var result = await controller.RefreshM2PlanDay(date, date, 7);

        productionService.Verify(
            service => service.RefreshProductionPlanFromM2Async(date, It.IsAny<string>()),
            Times.Once);
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ProductionController.M2Plan));
        redirect.RouteValues.Should().ContainKey("startDate").WhoseValue.Should().Be(date);
        redirect.RouteValues.Should().ContainKey("days").WhoseValue.Should().Be(7);
        controller.TempData["Success"].Should().Be("Odświeżono plan produkcji na 08.06.2026: Refreshed, pozycje: 2.");
    }

    [Fact]
    public async Task RefreshM2PlanRange_ShouldCallRangeRefreshAndReturnSummary()
    {
        var startDate = new DateOnly(2026, 6, 8);
        var productionService = new Mock<IProductionService>();
        productionService
            .Setup(service => service.RefreshProductionPlansFromM2Async(startDate, 7, It.IsAny<string>()))
            .ReturnsAsync(new ProductionPlanRefreshRangeResultDto
            {
                StartDate = startDate,
                Days = 7,
                CreatedCount = 1,
                RefreshedCount = 2,
                BlockedCount = 1,
            });
        var controller = CreateController(productionService);

        var result = await controller.RefreshM2PlanRange(startDate, 7);

        productionService.Verify(
            service => service.RefreshProductionPlansFromM2Async(startDate, 7, It.IsAny<string>()),
            Times.Once);
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ProductionController.M2Plan));
        redirect.RouteValues.Should().ContainKey("startDate").WhoseValue.Should().Be(startDate);
        redirect.RouteValues.Should().ContainKey("days").WhoseValue.Should().Be(7);
        controller.TempData["Success"].Should().Be("Refresh zakresu M2: utworzone 1, odświeżone 2, pominięte 0, zablokowane 1.");
    }

    [Fact]
    public async Task CookingComponent_ShouldLoadSnapshotCardAndPersistentSession()
    {
        const int PlanItemId = 42;
        const int RecipeComponentVersionId = 501;
        var card = new CookingComponentCardDto
        {
            PlanItemId = PlanItemId,
            RecipeComponentVersionId = RecipeComponentVersionId,
            ComponentName = "Ryż jaśminowy",
        };
        var session = new CookingComponentSessionDto
        {
            SessionId = 12,
            Status = "InProgress",
        };
        var productionService = new Mock<IProductionService>();
        productionService
            .Setup(service => service.GetCookingComponentCardAsync(PlanItemId, RecipeComponentVersionId))
            .ReturnsAsync(card);
        var cookingSessionService = new Mock<ICookingSessionService>();
        cookingSessionService
            .Setup(service => service.GetComponentSessionAsync(PlanItemId, RecipeComponentVersionId))
            .ReturnsAsync(session);
        var controller = CreateController(productionService, cookingSessionService: cookingSessionService);

        var result = await controller.CookingComponent(PlanItemId, RecipeComponentVersionId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<CookingComponentCardDto>().Subject;
        model.Should().BeSameAs(card);
        model.Session.Should().BeSameAs(session);
    }

    [Fact]
    public async Task ToggleCookingComponentStep_ShouldPersistStepAndRedirectBack()
    {
        const int PlanItemId = 42;
        const int RecipeComponentVersionId = 501;
        const int StepId = 711;
        var cookingSessionService = new Mock<ICookingSessionService>();
        var controller = CreateController(
            new Mock<IProductionService>(),
            cookingSessionService: cookingSessionService);

        var result = await controller.ToggleCookingComponentStep(
            PlanItemId,
            RecipeComponentVersionId,
            StepId,
            isChecked: true,
            actualValue: 76m,
            actualUnit: "C",
            notes: "OK");

        cookingSessionService.Verify(
            service => service.ToggleStepAsync(It.Is<ToggleCookingStepRequest>(request =>
                request.ProductionPlanItemId == PlanItemId &&
                request.RecipeComponentVersionId == RecipeComponentVersionId &&
                request.StepId == StepId &&
                request.IsChecked &&
                request.ActualValue == 76m &&
                request.ActualUnit == "C" &&
                request.Notes == "OK")),
            Times.Once);
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ProductionController.CookingComponent));
        redirect.RouteValues.Should().ContainKey("planItemId").WhoseValue.Should().Be(PlanItemId);
        redirect.RouteValues.Should().ContainKey("recipeComponentVersionId").WhoseValue.Should().Be(RecipeComponentVersionId);
    }

    private static ProductionController CreateController(
        Mock<IProductionService> productionService,
        Mock<ICookingSessionService>? cookingSessionService = null,
        Mock<IWarehouseDemandService>? warehouseDemandService = null,
        Mock<IPackingService>? packingService = null,
        Mock<IPackingSynchronizationService>? packingSynchronizationService = null)
    {
        var httpContext = new DefaultHttpContext();
        var controller = new ProductionController(
            productionService.Object,
            cookingSessionService?.Object ?? Mock.Of<ICookingSessionService>(),
            warehouseDemandService?.Object ?? Mock.Of<IWarehouseDemandService>(),
            packingService?.Object ?? Mock.Of<IPackingService>(),
            Mock.Of<IPackingIncidentService>(),
            packingSynchronizationService?.Object ?? Mock.Of<IPackingSynchronizationService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
        };

        return controller;
    }
}
