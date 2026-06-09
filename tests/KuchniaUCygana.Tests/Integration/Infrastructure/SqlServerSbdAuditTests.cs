using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace KuchniaUCygana.Tests.Integration.Infrastructure;

[Collection("SqlServerIntegration")]
public sealed class SqlServerSbdAuditTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public SqlServerSbdAuditTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Migrations_Should_Create_Sbd_TSqlObjects_And_Indexes()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var objects = new (string Name, string Type)[]
        {
            ("SystemLogsArchive", "U"),
            ("tr_Batches_UpdateIsDepleted", "TR"),
            ("usp_ArchiveSystemLogs", "P"),
            ("fn_MealNutritionCost", "IF"),
        };

        foreach (var dbObject in objects)
        {
            var exists = await CountAsync(
                connection,
                "SELECT COUNT(1) FROM sys.objects WHERE [name] = @name AND [type] = @type;",
                ("@name", dbObject.Name),
                ("@type", dbObject.Type));

            exists.Should().Be(1, $"{dbObject.Type} {dbObject.Name} should exist");
        }

        var indexes = new (string TableName, string IndexName)[]
        {
            ("Orders", "IX_Orders_Customer_IsDeleted_CreatedAt"),
            ("OrderItems", "IX_OrderItems_Order_IsDeleted"),
            ("DeliveryCalendar", "IX_DeliveryCalendar_Order_IsDeleted_DeliveryDate"),
            ("DeliveryCalendar", "IX_DeliveryCalendar_Date_Status_Skipped_IsDeleted"),
            ("Payments", "IX_Payments_OrderId"),
            ("Payments", "UX_Payments_StripePaymentIntentId"),
            ("Addresses", "IX_Addresses_User_IsDeleted_IsDefault"),
            ("SystemLogs", "IX_SystemLogs_Timestamp_Action_TargetEntity_UserId"),
            ("SystemLogs", "IX_SystemLogs_Window"),
            ("SystemLogs", "IX_SystemLogs_User_Window"),
            ("SystemLogs", "IX_SystemLogs_Action_Window"),
            ("SystemLogs", "IX_SystemLogs_Target_Window"),
            ("SystemLogsArchive", "IX_SystemLogsArchive_Window"),
            ("SystemLogsArchive", "IX_SystemLogsArchive_User_Window"),
            ("SystemLogsArchive", "IX_SystemLogsArchive_Action_Window"),
            ("SystemLogsArchive", "IX_SystemLogsArchive_Target_Window"),
            ("Batches", "IX_Batches_StockItem_Active_Expiry"),
        };

        foreach (var index in indexes)
        {
            var exists = await CountAsync(
                connection,
                """
                SELECT COUNT(1)
                FROM sys.indexes i
                WHERE i.[name] = @indexName
                  AND i.[object_id] = OBJECT_ID(@tableName);
                """,
                ("@indexName", index.IndexName),
                ("@tableName", $"dbo.{index.TableName}"));

            exists.Should().Be(1, $"{index.IndexName} should exist on {index.TableName}");
        }

        var batchIndexColumns = await ReadStringListAsync(
            connection,
            """
            SELECT c.[name]
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id]
            INNER JOIN sys.columns c ON c.[object_id] = ic.[object_id] AND c.[column_id] = ic.[column_id]
            WHERE i.[name] = N'IX_Batches_StockItem_Active_Expiry'
              AND i.[object_id] = OBJECT_ID(N'dbo.Batches')
              AND ic.[key_ordinal] > 0
            ORDER BY ic.[key_ordinal];
            """);

        batchIndexColumns.Should().Equal("StockItemId", "IsDeleted", "IsDepleted", "CurrentQuantity", "ExpiryDate");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BatchDepletedTrigger_Should_Synchronize_IsDepleted()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var unique = Guid.NewGuid().ToString("N")[..8];
        var unitId = await ScalarIntAsync(
            connection,
            """
            INSERT INTO [UnitsOfMeasure] ([Symbol], [Name], [Description])
            VALUES (@symbol, @name, N'SBD trigger test');
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@symbol", $"s{unique}"),
            ("@name", $"SBD Unit {unique}"));

        var stockItemId = await ScalarIntAsync(
            connection,
            """
            INSERT INTO [StockItems]
                ([Name], [DefaultUnitOfMeasureId], [MinimumLevel], [LeadTimeDays], [WarehouseCategoryId], [CreatedBy])
            VALUES
                (@name, @unitId, 1, 1, 4, N'SBD');
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@name", $"SBD Stock {unique}"),
            ("@unitId", unitId));

        var batchId = await ScalarIntAsync(
            connection,
            """
            INSERT INTO [Batches]
                ([StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ReceivedDate], [IsDepleted], [CreatedBy])
            VALUES
                (@stockItemId, @batchNumber, 5, SYSDATETIMEOFFSET(), 1, N'SBD');
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@stockItemId", stockItemId),
            ("@batchNumber", $"SBD-{unique}"));

        var afterInsert = await ScalarBoolAsync(
            connection,
            "SELECT [IsDepleted] FROM [Batches] WHERE [Id] = @batchId;",
            ("@batchId", batchId));

        afterInsert.Should().BeFalse();

        await ExecuteAsync(
            connection,
            "UPDATE [Batches] SET [CurrentQuantity] = 0 WHERE [Id] = @batchId;",
            ("@batchId", batchId));

        var afterUpdate = await ScalarBoolAsync(
            connection,
            "SELECT [IsDepleted] FROM [Batches] WHERE [Id] = @batchId;",
            ("@batchId", batchId));

        afterUpdate.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ArchiveSystemLogsProcedure_Should_Move_Old_Logs_To_Archive()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var userId = await InsertAuditUserAsync(connection);
        var oldLogId = await InsertSystemLogAsync(connection, userId, DateTimeOffset.UtcNow.AddDays(-220), "Old");
        var recentLogId = await InsertSystemLogAsync(connection, userId, DateTimeOffset.UtcNow.AddDays(-5), "Recent");

        var archivedCount = await ScalarIntAsync(
            connection,
            "EXEC [dbo].[usp_ArchiveSystemLogs] @OlderThanDays = 180, @BatchSize = 10;");

        archivedCount.Should().BeGreaterThanOrEqualTo(1);

        var oldInSource = await CountAsync(
            connection,
            "SELECT COUNT(1) FROM [SystemLogs] WHERE [Id] = @id;",
            ("@id", oldLogId));
        var oldInArchive = await CountAsync(
            connection,
            "SELECT COUNT(1) FROM [SystemLogsArchive] WHERE [Id] = @id;",
            ("@id", oldLogId));
        var recentInSource = await CountAsync(
            connection,
            "SELECT COUNT(1) FROM [SystemLogs] WHERE [Id] = @id;",
            ("@id", recentLogId));

        oldInSource.Should().Be(0);
        oldInArchive.Should().Be(1);
        recentInSource.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MealNutritionCostFunction_Should_Return_Cost_And_Macros_For_Simple_Meal()
    {
        await using var connection = new SqlConnection(this.fixture.AppConnectionString);
        await connection.OpenAsync();

        var unique = Guid.NewGuid().ToString("N")[..8];
        var categoryId = await ScalarIntAsync(
            connection,
            """
            INSERT INTO [Categories] ([Name], [Description], [SortOrder], [CreatedAt])
            VALUES (@name, N'SBD category', 1, SYSDATETIMEOFFSET());
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@name", $"SBD Category {unique}"));

        var mealId = await ScalarIntAsync(
            connection,
            """
            INSERT INTO [Meals] ([CategoryId], [Name], [Description], [CreatedAt])
            VALUES (@categoryId, @name, N'SBD meal', SYSDATETIMEOFFSET());
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@categoryId", categoryId),
            ("@name", $"SBD Meal {unique}"));

        var ingredientId = await ScalarIntAsync(
            connection,
            """
            INSERT INTO [Ingredients] ([Name], [Unit], [CostPerUnit], [CreatedAt])
            VALUES (@name, N'100g', 4.0000, SYSDATETIMEOFFSET());
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@name", $"SBD Ingredient {unique}"));

        await ExecuteAsync(
            connection,
            """
            INSERT INTO [NutritionFacts]
                ([IngredientId], [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [CreatedAt])
            VALUES
                (@ingredientId, 100, 10, 20, 5, 2, SYSDATETIMEOFFSET());
            """,
            ("@ingredientId", ingredientId));

        await ExecuteAsync(
            connection,
            """
            INSERT INTO [Recipes] ([MealId], [IngredientId], [WeightInGrams], [IsOptional])
            VALUES (@mealId, @ingredientId, 250, 0);
            """,
            ("@mealId", mealId),
            ("@ingredientId", ingredientId));

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [EstimatedCost], [Calories], [Protein], [Carbohydrates], [Fat], [Fiber]
            FROM [dbo].[fn_MealNutritionCost](@mealId);
            """;
        command.Parameters.AddWithValue("@mealId", mealId);

        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetDecimal(0).Should().Be(10.0000m);
        reader.GetDecimal(1).Should().Be(250.00m);
        reader.GetDecimal(2).Should().Be(25.00m);
        reader.GetDecimal(3).Should().Be(50.00m);
        reader.GetDecimal(4).Should().Be(12.50m);
        reader.GetDecimal(5).Should().Be(5.00m);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqlLogins_Should_Have_Expected_LeastPrivilege_Roles()
    {
        await using var connection = new SqlConnection(this.fixture.SaConnectionString);
        await connection.OpenAsync();

        foreach (var login in new[] { "admin", "pracownik", "klient" })
        {
            var loginExists = await CountAsync(
                connection,
                "SELECT COUNT(1) FROM sys.server_principals WHERE [name] = @name;",
                ("@name", login));

            loginExists.Should().Be(1, $"login {login} should exist");
        }

        (await IsRoleMemberAsync(connection, "admin", "db_owner")).Should().BeTrue();
        (await IsRoleMemberAsync(connection, "pracownik", "db_datareader")).Should().BeTrue();
        (await IsRoleMemberAsync(connection, "pracownik", "db_datawriter")).Should().BeTrue();
        (await IsRoleMemberAsync(connection, "pracownik", "db_owner")).Should().BeFalse();
        (await IsRoleMemberAsync(connection, "klient", "db_datareader")).Should().BeTrue();
        (await IsRoleMemberAsync(connection, "klient", "db_datawriter")).Should().BeTrue();
        (await IsRoleMemberAsync(connection, "klient", "db_owner")).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RuntimeLogin_Should_ReadWrite_But_Not_Execute_Ddl()
    {
        await using var runtimeConnection = new SqlConnection(this.fixture.RuntimeConnectionString);
        await runtimeConnection.OpenAsync();

        var unique = Guid.NewGuid().ToString("N");
        var userId = await ScalarIntAsync(
            runtimeConnection,
            """
            INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt])
            VALUES (@email, N'hash', N'SBD', N'Runtime', N'Client', SYSDATETIMEOFFSET());
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@email", $"sbd-runtime-{unique}@example.test"));

        var readBack = await ScalarIntAsync(
            runtimeConnection,
            "SELECT COUNT(1) FROM [Users] WHERE [Id] = @id;",
            ("@id", userId));
        readBack.Should().Be(1);

        await ExecuteAsync(
            runtimeConnection,
            "DELETE FROM [Users] WHERE [Id] = @id;",
            ("@id", userId));

        var createDenied = await DdlFailsAsync(runtimeConnection, $"CREATE TABLE [dbo].[RuntimeCreateDenied_{unique}] ([Id] int NOT NULL);");
        createDenied.Should().BeTrue();

        var probeTable = $"RuntimeDdlProbe_{unique}";
        await using var setupConnection = new SqlConnection(this.fixture.SaConnectionString);
        await setupConnection.OpenAsync();
        await ExecuteAsync(setupConnection, $"CREATE TABLE [dbo].[{probeTable}] ([Id] int NOT NULL);");

        try
        {
            var alterDenied = await DdlFailsAsync(runtimeConnection, $"ALTER TABLE [dbo].[{probeTable}] ADD [Name] nvarchar(20) NULL;");
            var dropDenied = await DdlFailsAsync(runtimeConnection, $"DROP TABLE [dbo].[{probeTable}];");

            alterDenied.Should().BeTrue();
            dropDenied.Should().BeTrue();
        }
        finally
        {
            await ExecuteAsync(setupConnection, $"DROP TABLE IF EXISTS [dbo].[{probeTable}];");
        }
    }

    private static async Task<int> InsertAuditUserAsync(SqlConnection connection)
    {
        var unique = Guid.NewGuid().ToString("N");
        return await ScalarIntAsync(
            connection,
            """
            INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt])
            VALUES (@email, N'hash', N'SBD', N'Audit', N'Admin', SYSDATETIMEOFFSET());
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@email", $"sbd-audit-{unique}@example.test"));
    }

    private static Task<int> InsertSystemLogAsync(SqlConnection connection, int userId, DateTimeOffset timestamp, string targetId)
        => ScalarIntAsync(
            connection,
            """
            INSERT INTO [SystemLogs] ([UserId], [Action], [TargetEntity], [TargetId], [OldValue], [NewValue], [Timestamp], [IPAddress])
            VALUES (@userId, N'Update', N'SbdAudit', @targetId, N'{}', N'{}', @timestamp, N'127.0.0.1');
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ("@userId", userId),
            ("@targetId", targetId),
            ("@timestamp", timestamp));

    private static async Task<bool> IsRoleMemberAsync(SqlConnection connection, string userName, string roleName)
    {
        var result = await CountAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM sys.database_role_members drm
            INNER JOIN sys.database_principals roles ON roles.[principal_id] = drm.[role_principal_id]
            INNER JOIN sys.database_principals members ON members.[principal_id] = drm.[member_principal_id]
            WHERE roles.[name] = @roleName
              AND members.[name] = @userName;
            """,
            ("@roleName", roleName),
            ("@userName", userName));

        return result == 1;
    }

    private static async Task<bool> DdlFailsAsync(SqlConnection connection, string commandText)
    {
        try
        {
            await ExecuteAsync(connection, commandText);
            return false;
        }
        catch (SqlException)
        {
            return true;
        }
    }

    private static Task<int> CountAsync(SqlConnection connection, string commandText, params (string Name, object Value)[] parameters)
        => ScalarIntAsync(connection, commandText, parameters);

    private static async Task<bool> ScalarBoolAsync(SqlConnection connection, string commandText, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, commandText, parameters);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<int> ScalarIntAsync(SqlConnection connection, string commandText, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, commandText, parameters);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<string>> ReadStringListAsync(SqlConnection connection, string commandText, params (string Name, object Value)[] parameters)
    {
        var results = new List<string>();
        await using var command = CreateCommand(connection, commandText, parameters);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string commandText, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, commandText, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string commandText, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }
}
