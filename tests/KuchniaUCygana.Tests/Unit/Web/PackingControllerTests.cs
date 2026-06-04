using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Web;

public sealed class PackingControllerTests
{
    [Fact]
    public async Task PrintSelectedLabels_ShouldGenerateMissingBagLabels_AndOpenPrintView()
    {
        var date = new DateOnly(2026, 6, 3);
        var generatedLabel = new PackingLabelDto
        {
            Id = 77,
            PackingSessionId = 12,
            PackingBagId = 501,
            BagCode = "BAG-000017-01",
            StopNumber = 2,
        };
        var packingService = new Mock<IPackingService>();
        packingService
            .Setup(service => service.GenerateTransportLabelsForBagAsync(501, null, false))
            .ReturnsAsync(new[] { generatedLabel });
        var controller = CreateController(packingService);

        var result = await controller.PrintSelectedLabels(
            date,
            routeId: 4,
            selectedLabelIds: null,
            selectedBagIds: "501",
            labelIds: new List<int>(),
            packingBagIds: new List<int>());

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("Labels");
        view.ViewData["AutoPrint"].Should().Be(true);
        view.ViewData["RouteId"].Should().Be(4);
        view.Model.Should().BeAssignableTo<IEnumerable<PackingLabelDto>>()
            .Which.Should().ContainSingle(label => label.Id == generatedLabel.Id);
        packingService.Verify(
            service => service.GenerateTransportLabelsForBagAsync(501, null, false),
            Times.Once);
    }

    private static PackingController CreateController(Mock<IPackingService> packingService)
    {
        var httpContext = new DefaultHttpContext();
        return new PackingController(
            packingService.Object,
            Mock.Of<ILoadingService>(),
            Mock.Of<IPackingIncidentService>(),
            Mock.Of<IPackingSynchronizationService>(),
            Mock.Of<IPackingBagService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
        };
    }
}
