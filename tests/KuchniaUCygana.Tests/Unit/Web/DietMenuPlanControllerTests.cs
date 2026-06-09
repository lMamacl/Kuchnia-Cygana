using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Web;

public sealed class DietMenuPlanControllerTests
{
    [Fact]
    public async Task Index_ClampsDaysBeforeCallingService()
    {
        var menuPlanService = new Mock<IDietMenuPlanManagementService>();
        var startDate = DateOnly.FromDateTime(DateTime.Today);
        menuPlanService
            .Setup(s => s.GetWeekAsync(startDate, 31))
            .ReturnsAsync(new DietMenuWeekDto
            {
                StartDate = startDate,
                EndDate = startDate.AddDays(30),
                DaysCount = 31,
            });
        var controller = new DietMenuPlanController(
            menuPlanService.Object,
            Mock.Of<IMealManagementService>());

        var result = await controller.Index(startDate, 999);

        result.Should().BeOfType<ViewResult>();
        menuPlanService.Verify(s => s.GetWeekAsync(startDate, 31), Times.Once);
    }
}
