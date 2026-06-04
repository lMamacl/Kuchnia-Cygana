using FluentMigrator.Runner;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

public static class MigrationRunner
{
    /// <summary>
    /// Executes database migrations, ensuring the target database exists and retrying on failure with increasing delays.
    /// </summary>
    /// <param name="services">The application's service provider used to create scopes and resolve migration services.</param>
    /// <param name="logger">Logger used for informational and warning messages during migration attempts.</param>
    /// <param name="maxRetries">Maximum number of migration attempts (including the final attempt).</param>
    /// <param name="baseDelaySeconds">Base delay in seconds multiplied by the attempt number to compute the retry delay.</param>
    public static void RunMigrations(IServiceProvider services, ILogger logger, int maxRetries = 5, int baseDelaySeconds = 2)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                EnsureDatabaseExists(scope.ServiceProvider, logger);
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                runner.MigrateUp();
                logger.LogInformation("Database migrations completed successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(baseDelaySeconds * attempt);
                logger.LogWarning(
                    ex,
                    "Migration attempt {Attempt} failed. Retrying in {DelaySeconds} seconds.",
                    attempt,
                    delay.TotalSeconds);
                Thread.Sleep(delay);
            }
        }

        using var finalScope = services.CreateScope();
        EnsureDatabaseExists(finalScope.ServiceProvider, logger);
        var finalRunner = finalScope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        finalRunner.MigrateUp();
    }

    /// <summary>
    /// Ensures the SQL Server database specified by the "DefaultConnection" connection string exists; creates the database if it does not.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the "DefaultConnection" connection string is missing or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the connection string does not contain a target database name (Initial Catalog).</exception>
    private static void EnsureDatabaseExists(IServiceProvider serviceProvider, ILogger logger)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var targetConnectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(targetConnectionString))
        {
            throw new InvalidOperationException("Migration connection string is missing.");
        }

        var targetBuilder = new SqlConnectionStringBuilder(targetConnectionString);
        var databaseName = targetBuilder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Target database name is missing in connection string.");
        }

        var adminBuilder = new SqlConnectionStringBuilder(targetConnectionString)
        {
            InitialCatalog = "master",
        };

        using var connection = new SqlConnection(adminBuilder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NULL CREATE DATABASE [{databaseName.Replace("]", "]]")}]";
        command.ExecuteNonQuery();

        logger.LogInformation("Database '{DatabaseName}' is ready.", databaseName);
    }
}
