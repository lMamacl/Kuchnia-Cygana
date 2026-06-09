using Dapper;
using FluentAssertions;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;

namespace KuchniaUCygana.Tests.Integration.Infrastructure;

[Collection("SqlServerIntegration")]
public sealed class LogisticsRepositoriesSqlServerTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public LogisticsRepositoriesSqlServerTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ThermalBagRepository_BaseCrud_ShouldUseMappedPluralTable()
    {
        var repository = new ThermalBagRepository(CreateConnectionFactory());
        var bag = new ThermalBag
        {
            SerialNumber = $"BAG-{Guid.NewGuid():N}",
            Status = BagStatus.Available,
        };

        var bagId = await repository.InsertAsync(bag);
        var stored = await repository.GetByIdAsync(bagId);

        stored.Should().NotBeNull();
        stored!.SerialNumber.Should().Be(bag.SerialNumber);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeliveryRouteRepository_InsertManyWithStopsAsync_ShouldRollbackAllRoutesWhenOneStopFails()
    {
        var connectionFactory = CreateConnectionFactory();
        var repository = new DeliveryRouteRepository(connectionFactory);
        var routeNamePrefix = $"Txn-{Guid.NewGuid():N}";
        var routes = new[]
        {
            CreateRoute($"{routeNamePrefix}-1", 1001),
            CreateRoute($"{routeNamePrefix}-2", 1002, new string('x', 257)),
        };

        var act = () => repository.InsertManyWithStopsAsync(routes);

        await act.Should().ThrowAsync<Exception>();

        using var db = connectionFactory.CreateConnection();
        var routeCount = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM [DeliveryRoutes] WHERE [Name] LIKE @Prefix;",
            new { Prefix = $"{routeNamePrefix}%" });

        routeCount.Should().Be(0);
    }

    private SqlServerConnectionFactory CreateConnectionFactory()
    {
        return new SqlServerConnectionFactory(this.fixture.AppConnectionString);
    }

    private static DeliveryRoute CreateRoute(string name, int deliveryCalendarId, string? stopCreatedBy = null)
    {
        return new DeliveryRoute
        {
            RouteDate = DateTimeOffset.UtcNow.Date,
            Name = name,
            Status = RouteStatus.Assigned,
            Stops =
            {
                new DeliveryRouteStop
                {
                    DeliveryCalendarId = deliveryCalendarId,
                    SequenceNumber = 1,
                    Status = StopStatus.Assigned,
                    CreatedBy = stopCreatedBy,
                },
            },
        };
    }
}
