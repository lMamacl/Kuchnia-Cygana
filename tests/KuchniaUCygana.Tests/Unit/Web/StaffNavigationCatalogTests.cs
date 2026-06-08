using System.Security.Claims;
using FluentAssertions;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Web.Models;

namespace KuchniaUCygana.Tests.Unit.Web;

public sealed class StaffNavigationCatalogTests
{
    [Fact]
    public void VisibleSections_ShouldShowM2PlanAndDemandInWarehouse_ForWarehouseRole()
    {
        var user = CreateUser(UserRoles.Warehouse);

        var sections = StaffNavigationCatalog.VisibleSections(user);
        var warehouse = sections.Single(section => section.Key == "warehouse");

        sections.Should().NotContain(section => section.Key == "kitchen");
        warehouse.Items.Should().Contain(item => item.Matches("Production", "M2Plan"));
        warehouse.Items.Should().Contain(item => item.Matches("Production", "WarehouseDemand"));
        StaffNavigationCatalog.FindSection("Production", "M2Plan", user)?.Key.Should().Be("warehouse");
        StaffNavigationCatalog.FindSection("Production", "WarehouseDemand", user)?.Key.Should().Be("warehouse");
    }

    [Fact]
    public void VisibleSections_ShouldKeepM2PlanInKitchen_WithoutWarehouseDemand_ForKitchenRole()
    {
        var user = CreateUser(UserRoles.Kitchen);

        var sections = StaffNavigationCatalog.VisibleSections(user);
        var kitchen = sections.Single(section => section.Key == "kitchen");

        kitchen.Items.Should().Contain(item => item.Matches("Production", "M2Plan"));
        kitchen.Items.Should().NotContain(item => item.Matches("Production", "WarehouseDemand"));
        StaffNavigationCatalog.FindSection("Production", "M2Plan", user)?.Key.Should().Be("kitchen");
    }

    [Fact]
    public void VisibleSections_ShouldAvoidDuplicateProductionShortcuts_ForAdminRole()
    {
        var user = CreateUser(UserRoles.Admin);
        var sections = StaffNavigationCatalog.VisibleSections(user);
        var routeKeys = sections
            .SelectMany(section => section.Items)
            .Select(item => $"{item.Controller}:{item.Action}")
            .ToList();

        routeKeys.Should().OnlyHaveUniqueItems();
        StaffNavigationCatalog.FindSection("Production", "M2Plan", user)?.Key.Should().Be("kitchen");
        StaffNavigationCatalog.FindSection("Production", "WarehouseDemand", user)?.Key.Should().Be("warehouse");
    }

    private static ClaimsPrincipal CreateUser(params string[] roles)
        => new(new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)),
            authenticationType: "TestAuth"));
}
