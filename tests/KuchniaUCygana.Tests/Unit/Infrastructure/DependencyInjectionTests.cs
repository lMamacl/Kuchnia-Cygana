using FluentAssertions;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure;
using KuchniaUCygana.Infrastructure.Adapters;
using KuchniaUCygana.Infrastructure.Mocks;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_Registers_SqlConnectionFactory_AsSingleton()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=KuchniaUCygana_Test;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=True;TrustServerCertificate=True;",
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddInfrastructure(config);
        var provider = services.BuildServiceProvider();

        var dbFactory1 = provider.GetRequiredService<IDbConnectionFactory>();
        var dbFactory2 = provider.GetRequiredService<IDbConnectionFactory>();

        // Assert
        dbFactory1.Should().BeSameAs(dbFactory2);
        dbFactory1.Should().BeOfType<SqlServerConnectionFactory>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Mock")]
    public void AddInfrastructure_Registers_MockOrderProvider_ByDefault(string? orderProvider)
    {
        var config = CreateConfiguration(orderProvider);
        var services = new ServiceCollection();

        services.AddInfrastructure(config);
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOrderDataProvider>().Should().BeOfType<MockOrderDataProvider>();
    }

    [Fact]
    public void AddInfrastructure_Registers_M1OrderProvider_WhenConfigured()
    {
        var config = CreateConfiguration("M1");
        var services = new ServiceCollection();

        services.AddInfrastructure(config);
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOrderDataProvider>().Should().BeOfType<M1OrderDataProvider>();
    }

    [Fact]
    public void AddInfrastructure_Registers_Module3RepositoryContracts()
    {
        var config = CreateConfiguration();
        var services = new ServiceCollection();

        services.AddInfrastructure(config);
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IPackingSessionRepository>().Should().NotBeNull();
        provider.GetRequiredService<ITemperatureLogRepository>().Should().NotBeNull();
    }

    private static IConfiguration CreateConfiguration(string? orderProvider = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=KuchniaUCygana_Test;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=True;TrustServerCertificate=True;",
        };

        if (orderProvider is not null)
        {
            values["OrderProvider"] = orderProvider;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
