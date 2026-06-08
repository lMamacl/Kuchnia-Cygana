using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Infrastructure.Pdf;
using KuchniaUCygana.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Web;

public sealed class WarehouseControllerTests
{
    [Fact]
    public async Task LogTemperature_ShouldRedirectToHaccpReportWithActiveTab_WhenSaveSucceeds()
    {
        var request = new LogTemperatureRequest
        {
            DeviceNameOrLocation = "Lodowka #1 - Nabial",
            TemperatureCelsius = 4.5m,
            Remarks = "OK"
        };

        var temperatureService = new Mock<ITemperatureService>();
        temperatureService
            .Setup(s => s.LogTemperatureAsync(It.Is<LogTemperatureRequest>(r => r == request)))
            .ReturnsAsync(new TemperatureLogDto
            {
                DeviceNameOrLocation = request.DeviceNameOrLocation,
                RecordedTemperatureCelsius = request.TemperatureCelsius,
                RecordedAt = DateTimeOffset.UtcNow
            });

        var controller = CreateController(temperatureService);

        var result = await controller.LogTemperature(request);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(WarehouseController.HaccpReport));
        redirect.RouteValues.Should().ContainKey("activeTab").WhoseValue.Should().Be(request.DeviceNameOrLocation);
        controller.TempData["Success"].Should().NotBeNull();
        temperatureService.Verify(s => s.LogTemperatureAsync(request), Times.Once);
    }

    [Fact]
    public async Task LogTemperature_ShouldReturnHaccpReportAndSkipSave_WhenModelStateIsInvalid()
    {
        var request = new LogTemperatureRequest
        {
            DeviceNameOrLocation = "Lodowka #1 - Nabial",
            TemperatureCelsius = 99m
        };
        var report = new HaccpReportDto();
        var temperatureService = new Mock<ITemperatureService>();
        temperatureService
            .Setup(s => s.GetHaccpReportAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(report);

        var controller = CreateController(temperatureService);
        controller.ModelState.AddModelError(nameof(LogTemperatureRequest.TemperatureCelsius), "Poza zakresem.");

        var result = await controller.LogTemperature(request);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("HaccpReport");
        view.Model.Should().BeSameAs(report);
        controller.ViewData["LogRequest"].Should().BeSameAs(request);
        temperatureService.Verify(s => s.LogTemperatureAsync(It.IsAny<LogTemperatureRequest>()), Times.Never);
    }

    [Fact]
    public async Task LogTemperature_ShouldReturnHaccpReportWithError_WhenSaveThrows()
    {
        var request = new LogTemperatureRequest
        {
            DeviceNameOrLocation = "Lodowka #1 - Nabial",
            TemperatureCelsius = 4.5m
        };
        var report = new HaccpReportDto();
        var temperatureService = new Mock<ITemperatureService>();
        temperatureService
            .Setup(s => s.LogTemperatureAsync(request))
            .ThrowsAsync(new InvalidOperationException("Database unavailable."));
        temperatureService
            .Setup(s => s.GetHaccpReportAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(report);

        var controller = CreateController(temperatureService);

        var result = await controller.LogTemperature(request);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("HaccpReport");
        view.Model.Should().BeSameAs(report);
        controller.ModelState[string.Empty]?.Errors.Should().Contain(e => e.ErrorMessage == "Database unavailable.");
        controller.ViewData["LogRequest"].Should().BeSameAs(request);
    }

    [Fact]
    public async Task StockLookup_ShouldReturnPromptAndSkipService_WhenQueryIsShort()
    {
        var warehouseService = new Mock<IWarehouseService>();
        var controller = CreateController(warehouseService: warehouseService);

        var result = await controller.StockLookup("a");

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.ViewName.Should().Be("_StockLookupPartial");
        partial.Model.Should().BeAssignableTo<IEnumerable<StockItemDto>>()
            .Which.Should().BeEmpty();
        controller.ViewData["LookupMessage"].Should().Be("Wpisz co najmniej 2 znaki");
        warehouseService.Verify(
            s => s.SearchStockLookupAsync(It.IsAny<StockLookupFilterDto>()),
            Times.Never);
    }

    [Fact]
    public async Task StockLookup_ShouldUseLimitedSearch_WhenQueryHasMinimumLength()
    {
        var warehouseService = new Mock<IWarehouseService>();
        warehouseService
            .Setup(s => s.SearchStockLookupAsync(It.IsAny<StockLookupFilterDto>()))
            .ReturnsAsync(Array.Empty<StockItemDto>());
        var controller = CreateController(warehouseService: warehouseService);

        var result = await controller.StockLookup("mą", onlyAvailable: true);

        result.Should().BeOfType<PartialViewResult>()
            .Subject.ViewName.Should().Be("_StockLookupPartial");
        warehouseService.Verify(
            s => s.SearchStockLookupAsync(It.Is<StockLookupFilterDto>(f =>
                f.Query == "mą" &&
                f.Limit == 20 &&
                f.OnlyAvailable)),
            Times.Once);
    }

    [Fact]
    public void Receive_ShouldCreateOperationKey()
    {
        var controller = CreateController();

        var result = controller.Receive();

        var model = result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<ReceiveDeliveryRequest>().Subject;
        model.OperationKey.Should().StartWith("WH-RCV-");
        model.OperationKey.Should().HaveLength(39);
    }

    [Fact]
    public void Issue_ShouldCreateOperationKey()
    {
        var controller = CreateController();

        var result = controller.Issue();

        var model = result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<ManualIssueRequest>().Subject;
        model.OperationKey.Should().StartWith("WH-ISS-");
        model.OperationKey.Should().HaveLength(39);
    }

    [Fact]
    public void Waste_ShouldCreateOperationKey()
    {
        var controller = CreateController();

        var result = controller.Waste();

        var model = result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<RegisterWasteRequest>().Subject;
        model.OperationKey.Should().StartWith("WH-WST-");
        model.OperationKey.Should().HaveLength(39);
    }

    [Fact]
    public async Task FefoReport_ShouldReturnPartial_WhenRequestComesFromHtmx()
    {
        var warehouseService = new Mock<IWarehouseService>();
        var page = new PagedResultDto<FefoReportItemDto>
        {
            Items = Array.Empty<FefoReportItemDto>(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0,
        };
        warehouseService
            .Setup(s => s.GetFefoReportPageAsync(It.IsAny<FefoReportFilterDto>()))
            .ReturnsAsync(page);
        var controller = CreateController(warehouseService: warehouseService);
        controller.ControllerContext.HttpContext.Request.Headers["HX-Request"] = "true";

        var result = await controller.FefoReport(new FefoReportFilterDto { Search = "partia" });

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.ViewName.Should().Be("_FefoReportTablePartial");
        partial.Model.Should().BeSameAs(page);
    }

    [Fact]
    public async Task Waste_ShouldShowServiceErrorAndKeepSelectedBatch_WhenSelectedBatchHasNotEnoughStock()
    {
        var request = new RegisterWasteRequest
        {
            StockItemId = 10,
            BatchId = 19,
            Quantity = 2m,
            Reason = "Przeterminowanie",
        };
        var warehouseService = new Mock<IWarehouseService>();
        warehouseService
            .Setup(s => s.RegisterWasteAsync(request))
            .ThrowsAsync(new InvalidOperationException("Niewystarczająca ilość składnika w wybranej partii."));
        warehouseService
            .Setup(s => s.GetStockLookupByIdAsync(request.StockItemId))
            .ReturnsAsync(new StockItemDto { Id = request.StockItemId, Name = "Bataty" });
        warehouseService
            .Setup(s => s.GetActiveBatchesForStockItemAsync(request.StockItemId))
            .ReturnsAsync(new[]
            {
                new BatchDto
                {
                    Id = 19,
                    StockItemId = request.StockItemId,
                    SupplierBatchNumber = "Bat-19",
                    CurrentQuantity = 0.4m,
                },
            });
        var controller = CreateController(warehouseService: warehouseService);

        var result = await controller.Waste(request);

        result.Should().BeOfType<ViewResult>()
            .Subject.Model.Should().BeSameAs(request);
        controller.ModelState[string.Empty]?.Errors.Should()
            .Contain(e => e.ErrorMessage.Contains("wybranej partii"));
        controller.ViewData["SelectedStockItem"].Should().BeOfType<StockItemDto>()
            .Which.Name.Should().Be("Bataty");
        controller.ViewData["SelectedBatchId"].Should().Be(request.BatchId);
        controller.ViewData["SelectedBatches"].Should().BeAssignableTo<IReadOnlyList<BatchDto>>()
            .Which.Should().ContainSingle(b => b.Id == 19);
    }

    private static WarehouseController CreateController(
        Mock<ITemperatureService>? temperatureService = null,
        Mock<IWarehouseService>? warehouseService = null)
    {
        var controller = new WarehouseController(
            warehouseService?.Object ?? Mock.Of<IWarehouseService>(),
            temperatureService?.Object ?? Mock.Of<ITemperatureService>(),
            Mock.Of<IWarehouseCategoryService>(),
            Mock.Of<IHaccpLocationService>(),
            Mock.Of<IPdfGenerator>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        return controller;
    }
}
