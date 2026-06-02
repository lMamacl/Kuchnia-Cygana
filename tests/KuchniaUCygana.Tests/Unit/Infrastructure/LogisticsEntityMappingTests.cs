using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using KuchniaUCygana.Domain.Entities.Logistics;

namespace KuchniaUCygana.Tests.Unit.Infrastructure;

public sealed class LogisticsEntityMappingTests
{
    [Theory]
    [InlineData(typeof(Vehicle), "Vehicles")]
    [InlineData(typeof(Driver), "Drivers")]
    [InlineData(typeof(Dispatcher), "Dispatchers")]
    [InlineData(typeof(DeliveryRoute), "DeliveryRoutes")]
    [InlineData(typeof(DeliveryRouteStop), "DeliveryRouteStops")]
    [InlineData(typeof(ThermalBag), "ThermalBags")]
    [InlineData(typeof(BagMovementLog), "BagMovementLogs")]
    public void LogisticsEntities_ShouldMapToExistingTables(Type entityType, string expectedTableName)
    {
        var attribute = entityType
            .GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull();
        attribute!.Name.Should().Be(expectedTableName);
    }
}
