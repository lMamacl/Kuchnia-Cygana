using FluentAssertions;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Web;

public sealed class ProductionControllerTests
{
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

    private static ProductionController CreateController(Mock<IProductionService> productionService)
    {
        var httpContext = new DefaultHttpContext();
        var controller = new ProductionController(
            productionService.Object,
            Mock.Of<IPackingService>(),
            Mock.Of<IPackingIncidentService>())
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
