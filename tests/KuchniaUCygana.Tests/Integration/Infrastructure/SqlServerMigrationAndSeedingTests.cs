using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace KuchniaUCygana.Tests.Integration.Infrastructure;

[CollectionDefinition("SqlServerIntegration")]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationFixture>
{
}

[Collection("SqlServerIntegration")]
public sealed class SqlServerMigrationAndSeedingTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public SqlServerMigrationAndSeedingTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Migrations_Should_Create_CoreTables_WithExpectedTypes()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var checks = new (string TableName, string ColumnName, string ExpectedType)[]
        {
            ("Users", "CreatedAt", "datetime"),
            ("Users", "Role", "nvarchar"),
            ("Users", "Email", "nvarchar"),
        };

        foreach (var check in checks)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName
                """;
            command.Parameters.AddWithValue("@tableName", check.TableName);
            command.Parameters.AddWithValue("@columnName", check.ColumnName);

            var actualType = (string?)await command.ExecuteScalarAsync();
            actualType.Should().Be(check.ExpectedType);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Seeding_Should_Insert_MinimalRealistic_Dataset()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        await using var usersCountCommand = connection.CreateCommand();
        usersCountCommand.CommandText = "SELECT COUNT(1) FROM [Users]";
        var usersCount = (int)(await usersCountCommand.ExecuteScalarAsync() ?? 0);
        usersCount.Should().BeGreaterThan(0);

        await using var rolesCommand = connection.CreateCommand();
        rolesCommand.CommandText = "SELECT COUNT(1) FROM [Users] WHERE [Role] IN (N'Admin', N'Kitchen', N'Driver')";
        var knownRolesCount = (int)(await rolesCommand.ExecuteScalarAsync() ?? 0);
        knownRolesCount.Should().BeGreaterThanOrEqualTo(3);
    }
}
