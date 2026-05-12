using FluentAssertions;
using KuchniaUCygana.Infrastructure;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack.OrmLite;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_Registers_OrmLiteFactory_AsSingleton()
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

        var ormLiteFactory1 = provider.GetRequiredService<OrmLiteConnectionFactory>();
        var ormLiteFactory2 = provider.GetRequiredService<OrmLiteConnectionFactory>();
        var dbFactory1 = provider.GetRequiredService<IDbConnectionFactory>();
        var dbFactory2 = provider.GetRequiredService<IDbConnectionFactory>();

        // Assert
        ormLiteFactory1.Should().BeSameAs(ormLiteFactory2);
        dbFactory1.Should().BeSameAs(dbFactory2);
        dbFactory1.Should().BeOfType<SqlServerConnectionFactory>();
    }
}
