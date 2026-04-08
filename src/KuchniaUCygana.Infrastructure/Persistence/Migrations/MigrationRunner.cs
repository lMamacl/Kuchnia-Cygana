using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

public static class MigrationRunner
{
    public static void RunMigrations(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
