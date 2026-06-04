using KuchniaUCygana.Infrastructure;
using KuchniaUCygana.Infrastructure.Persistence.Migrations;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace KuchniaUCygana.Tests.Integration.Infrastructure;

public sealed class SqlServerIntegrationFixture : IAsyncLifetime
{
    private const string AdminPassword = "Admin123!Integration";
    private const string ClientPassword = "Klient123!Integration";
    private const string RuntimePassword = "Pracownik123!Integration";

    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string DatabaseName { get; } = $"KuchniaUCygana_Int_{Guid.NewGuid():N}";

    public string ClientConnectionString { get; private set; } = string.Empty;

    public string AppConnectionString { get; private set; } = string.Empty;

    public string MigrationConnectionString { get; private set; } = string.Empty;

    public string RuntimeConnectionString { get; private set; } = string.Empty;

    public string SaConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        AppConnectionString = BuildAppConnectionString(container.GetConnectionString(), DatabaseName);
        SaConnectionString = AppConnectionString;
        MigrationConnectionString = BuildLoginConnectionString(container.GetConnectionString(), DatabaseName, "admin", AdminPassword);
        RuntimeConnectionString = BuildLoginConnectionString(container.GetConnectionString(), DatabaseName, "pracownik", RuntimePassword);
        ClientConnectionString = BuildLoginConnectionString(container.GetConnectionString(), DatabaseName, "klient", ClientPassword);

        await BootstrapDatabaseLoginsAsync(container.GetConnectionString());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = AppConnectionString,
                ["ConnectionStrings:MigrationConnection"] = MigrationConnectionString,
                ["DatabaseSeeding:Enabled"] = "true",
                ["DatabaseSeeding:Mode"] = "Both",
                ["DatabaseSeeding:Profile"] = "MinimalRealistic",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        MigrationRunner.RunMigrations(provider, NullLogger.Instance);

        using var scope = provider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync(DatabaseSeedingProfile.MinimalRealistic);
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    private static string BuildAppConnectionString(string baseConnectionString, string databaseName)
    {
        var normalized = baseConnectionString
            .Replace("Trust Server Certificate=", "TrustServerCertificate=", StringComparison.OrdinalIgnoreCase);

        var parts = normalized.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        var databaseSet = false;

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = $"Database={databaseName}";
                databaseSet = true;
            }
        }

        if (!databaseSet)
        {
            parts.Add($"Database={databaseName}");
        }

        parts.RemoveAll(p => p.StartsWith("Encrypt=", StringComparison.OrdinalIgnoreCase));
        parts.Add("Encrypt=False");
        parts.RemoveAll(p => p.StartsWith("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase));
        parts.Add("TrustServerCertificate=True");

        return string.Join(';', parts) + ";";
    }

    private static string BuildLoginConnectionString(string baseConnectionString, string databaseName, string userId, string password)
    {
        var builder = new SqlConnectionStringBuilder(BuildAppConnectionString(baseConnectionString, databaseName))
        {
            UserID = userId,
            Password = password,
        };

        return builder.ConnectionString;
    }

    private async Task BootstrapDatabaseLoginsAsync(string baseConnectionString)
    {
        await using var connection = new SqlConnection(BuildAppConnectionString(baseConnectionString, "master"));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @targetDbName sysname = @BootstrapDbName;
            DECLARE @adminPwd nvarchar(256) = @BootstrapAdminPassword;
            DECLARE @runtimePwd nvarchar(256) = @BootstrapRuntimePassword;
            DECLARE @clientPwd nvarchar(256) = @BootstrapClientPassword;

            IF DB_ID(@targetDbName) IS NULL
            BEGIN
                DECLARE @createDatabaseSql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@targetDbName) + N';';
                EXEC(@createDatabaseSql);
            END;

            DECLARE @adminPasswordSql nvarchar(512) = REPLACE(@adminPwd, N'''', N'''''');
            DECLARE @runtimePasswordSql nvarchar(512) = REPLACE(@runtimePwd, N'''', N'''''');
            DECLARE @clientPasswordSql nvarchar(512) = REPLACE(@clientPwd, N'''', N'''''');
            DECLARE @loginSql nvarchar(max);

            IF SUSER_ID(N'admin') IS NULL
                SET @loginSql = N'CREATE LOGIN [admin] WITH PASSWORD = N''' + @adminPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@targetDbName) + N';';
            ELSE
                SET @loginSql = N'ALTER LOGIN [admin] WITH PASSWORD = N''' + @adminPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@targetDbName) + N';';
            EXEC(@loginSql);

            IF SUSER_ID(N'pracownik') IS NULL
                SET @loginSql = N'CREATE LOGIN [pracownik] WITH PASSWORD = N''' + @runtimePasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@targetDbName) + N';';
            ELSE
                SET @loginSql = N'ALTER LOGIN [pracownik] WITH PASSWORD = N''' + @runtimePasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@targetDbName) + N';';
            EXEC(@loginSql);

            IF SUSER_ID(N'klient') IS NULL
                SET @loginSql = N'CREATE LOGIN [klient] WITH PASSWORD = N''' + @clientPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@targetDbName) + N';';
            ELSE
                SET @loginSql = N'ALTER LOGIN [klient] WITH PASSWORD = N''' + @clientPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@targetDbName) + N';';
            EXEC(@loginSql);

            DECLARE @grantDatabaseAccessSql nvarchar(max) = N'
            USE ' + QUOTENAME(@targetDbName) + N';

            IF USER_ID(N''admin'') IS NULL
                CREATE USER [admin] FOR LOGIN [admin];
            ELSE
                ALTER USER [admin] WITH LOGIN = [admin];

            IF USER_ID(N''pracownik'') IS NULL
                CREATE USER [pracownik] FOR LOGIN [pracownik];
            ELSE
                ALTER USER [pracownik] WITH LOGIN = [pracownik];

            IF USER_ID(N''klient'') IS NULL
                CREATE USER [klient] FOR LOGIN [klient];
            ELSE
                ALTER USER [klient] WITH LOGIN = [klient];

            IF ISNULL(IS_ROLEMEMBER(N''db_owner'', N''admin''), 0) <> 1
                ALTER ROLE [db_owner] ADD MEMBER [admin];

            IF ISNULL(IS_ROLEMEMBER(N''db_owner'', N''pracownik''), 0) = 1
                ALTER ROLE [db_owner] DROP MEMBER [pracownik];

            IF ISNULL(IS_ROLEMEMBER(N''db_owner'', N''klient''), 0) = 1
                ALTER ROLE [db_owner] DROP MEMBER [klient];

            IF ISNULL(IS_ROLEMEMBER(N''db_datareader'', N''pracownik''), 0) <> 1
                ALTER ROLE [db_datareader] ADD MEMBER [pracownik];

            IF ISNULL(IS_ROLEMEMBER(N''db_datawriter'', N''pracownik''), 0) <> 1
                ALTER ROLE [db_datawriter] ADD MEMBER [pracownik];

            IF ISNULL(IS_ROLEMEMBER(N''db_datareader'', N''klient''), 0) <> 1
                ALTER ROLE [db_datareader] ADD MEMBER [klient];

            IF ISNULL(IS_ROLEMEMBER(N''db_datawriter'', N''klient''), 0) <> 1
                ALTER ROLE [db_datawriter] ADD MEMBER [klient];
            ';

            EXEC(@grantDatabaseAccessSql);
            """;
        command.Parameters.AddWithValue("@BootstrapDbName", DatabaseName);
        command.Parameters.AddWithValue("@BootstrapAdminPassword", AdminPassword);
        command.Parameters.AddWithValue("@BootstrapRuntimePassword", RuntimePassword);
        command.Parameters.AddWithValue("@BootstrapClientPassword", ClientPassword);

        await command.ExecuteNonQueryAsync();
    }
}
