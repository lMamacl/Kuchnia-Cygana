using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(507)]
public sealed class AddSbdSqlObjectsAndIndexes : Migration
{
    public override void Up()
    {
        CreateSystemLogsArchiveTable();
        CreateOrReplaceBatchDepletedTrigger();
        CreateOrReplaceSystemLogsArchiveProcedure();
        CreateOrReplaceMealNutritionCostFunction();
        CreateSbdIndexes();
    }

    public override void Down()
    {
        Execute.Sql("DROP FUNCTION IF EXISTS [dbo].[fn_MealNutritionCost];");
        Execute.Sql("DROP PROCEDURE IF EXISTS [dbo].[usp_ArchiveSystemLogs];");
        Execute.Sql("DROP TRIGGER IF EXISTS [dbo].[tr_Batches_UpdateIsDepleted];");

        DeleteIndexIfExists("IX_SystemLogs_Timestamp_Action_TargetEntity_UserId", "SystemLogs");
        DeleteIndexIfExists("IX_Addresses_User_IsDeleted_IsDefault", "Addresses");
        DeleteIndexIfExists("UX_Payments_StripePaymentIntentId", "Payments");
        DeleteIndexIfExists("IX_Payments_OrderId", "Payments");
        DeleteIndexIfExists("IX_DeliveryCalendar_Date_Status_Skipped_IsDeleted", "DeliveryCalendar");
        DeleteIndexIfExists("IX_DeliveryCalendar_Order_IsDeleted_DeliveryDate", "DeliveryCalendar");
        DeleteIndexIfExists("IX_OrderItems_Order_IsDeleted", "OrderItems");
        DeleteIndexIfExists("IX_Orders_Customer_IsDeleted_CreatedAt", "Orders");

        DeleteIndexIfExists("IX_Batches_StockItem_Active_Expiry", "Batches");
        Execute.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_StockItem_Active_Expiry' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
                ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [ExpiryDate])
                INCLUDE ([CurrentQuantity], [SupplierBatchNumber]);
            """);

        if (Schema.Table("SystemLogsArchive").Exists())
        {
            Delete.Table("SystemLogsArchive");
        }
    }

    private void CreateSystemLogsArchiveTable()
    {
        if (Schema.Table("SystemLogsArchive").Exists())
        {
            return;
        }

        Create.Table("SystemLogsArchive")
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("UserId").AsInt32().NotNullable()
            .WithColumn("Action").AsString(100).NotNullable()
            .WithColumn("TargetEntity").AsString(50).NotNullable()
            .WithColumn("TargetId").AsString(100).NotNullable()
            .WithColumn("OldValue").AsString(int.MaxValue).Nullable()
            .WithColumn("NewValue").AsString(int.MaxValue).Nullable()
            .WithColumn("Timestamp").AsDateTimeOffset().NotNullable()
            .WithColumn("IPAddress").AsString(45).Nullable()
            .WithColumn("ArchivedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);
    }

    private void CreateOrReplaceBatchDepletedTrigger()
    {
        Execute.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[tr_Batches_UpdateIsDepleted]
            ON [dbo].[Batches]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                UPDATE b
                SET [IsDepleted] = CASE WHEN b.[CurrentQuantity] <= 0 THEN 1 ELSE 0 END
                FROM [dbo].[Batches] b
                INNER JOIN inserted i ON i.[Id] = b.[Id]
                WHERE b.[IsDepleted] <> CASE WHEN b.[CurrentQuantity] <= 0 THEN 1 ELSE 0 END;
            END;
            """);
    }

    private void CreateOrReplaceSystemLogsArchiveProcedure()
    {
        Execute.Sql(
            """
            CREATE OR ALTER PROCEDURE [dbo].[usp_ArchiveSystemLogs]
                @OlderThanDays int = 180,
                @BatchSize int = 1000
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF @OlderThanDays < 0
                    THROW 51010, '@OlderThanDays cannot be negative.', 1;

                IF @BatchSize <= 0
                    THROW 51011, '@BatchSize must be greater than zero.', 1;

                DECLARE @Cutoff datetimeoffset = DATEADD(DAY, -@OlderThanDays, SYSDATETIMEOFFSET());
                DECLARE @ArchivedIds TABLE ([Id] int NOT NULL PRIMARY KEY);

                INSERT INTO @ArchivedIds ([Id])
                SELECT TOP (@BatchSize) sl.[Id]
                FROM [dbo].[SystemLogs] sl WITH (READPAST, UPDLOCK)
                WHERE sl.[Timestamp] < @Cutoff
                ORDER BY sl.[Timestamp], sl.[Id];

                BEGIN TRANSACTION;

                INSERT INTO [dbo].[SystemLogsArchive]
                    ([Id], [UserId], [Action], [TargetEntity], [TargetId], [OldValue], [NewValue], [Timestamp], [IPAddress], [ArchivedAt])
                SELECT
                    sl.[Id],
                    sl.[UserId],
                    sl.[Action],
                    sl.[TargetEntity],
                    sl.[TargetId],
                    sl.[OldValue],
                    sl.[NewValue],
                    sl.[Timestamp],
                    sl.[IPAddress],
                    SYSDATETIMEOFFSET()
                FROM [dbo].[SystemLogs] sl
                INNER JOIN @ArchivedIds ids ON ids.[Id] = sl.[Id]
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[SystemLogsArchive] archived
                    WHERE archived.[Id] = sl.[Id]);

                DELETE sl
                FROM [dbo].[SystemLogs] sl
                INNER JOIN @ArchivedIds ids ON ids.[Id] = sl.[Id];

                DECLARE @ArchivedCount int = @@ROWCOUNT;

                COMMIT TRANSACTION;

                SELECT @ArchivedCount AS [ArchivedCount];
            END;
            """);
    }

    private void CreateOrReplaceMealNutritionCostFunction()
    {
        Execute.Sql(
            """
            CREATE OR ALTER FUNCTION [dbo].[fn_MealNutritionCost] (@MealId int)
            RETURNS TABLE
            AS
            RETURN
            (
                SELECT
                    @MealId AS [MealId],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * i.[CostPerUnit]), 0) AS decimal(18, 4)) AS [EstimatedCost],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[CaloriesPer100g]), 0) AS decimal(18, 2)) AS [Calories],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[ProteinPer100g]), 0) AS decimal(18, 2)) AS [Protein],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[CarbohydratesPer100g]), 0) AS decimal(18, 2)) AS [Carbohydrates],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[FatPer100g]), 0) AS decimal(18, 2)) AS [Fat],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[FiberPer100g]), 0) AS decimal(18, 2)) AS [Fiber]
                FROM [dbo].[Recipes] r
                INNER JOIN [dbo].[Ingredients] i ON i.[Id] = r.[IngredientId]
                LEFT JOIN [dbo].[NutritionFacts] nf ON nf.[IngredientId] = r.[IngredientId]
                WHERE r.[MealId] = @MealId
                  AND i.[IsDeleted] = 0
                  AND i.[IsActive] = 1
            );
            """);
    }

    private void CreateSbdIndexes()
    {
        CreateIndexIfMissing("IX_Orders_Customer_IsDeleted_CreatedAt", "Orders", "CustomerId", "IsDeleted", "CreatedAt");
        CreateIndexIfMissing("IX_OrderItems_Order_IsDeleted", "OrderItems", "OrderId", "IsDeleted");
        CreateIndexIfMissing("IX_DeliveryCalendar_Order_IsDeleted_DeliveryDate", "DeliveryCalendar", "OrderId", "IsDeleted", "DeliveryDate");
        CreateIndexIfMissing("IX_DeliveryCalendar_Date_Status_Skipped_IsDeleted", "DeliveryCalendar", "DeliveryDate", "Status", "IsSkipped", "IsDeleted");
        CreateIndexIfMissing("IX_Payments_OrderId", "Payments", "OrderId");
        CreateIndexIfMissing("UX_Payments_StripePaymentIntentId", "Payments", true, "StripePaymentIntentId");
        CreateIndexIfMissing("IX_Addresses_User_IsDeleted_IsDefault", "Addresses", "UserId", "IsDeleted", "IsDefault");
        CreateIndexIfMissing("IX_SystemLogs_Timestamp_Action_TargetEntity_UserId", "SystemLogs", "Timestamp", "Action", "TargetEntity", "UserId");

        DeleteIndexIfExists("IX_Batches_StockItem_Active_Expiry", "Batches");
        Execute.Sql(
            """
            CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
            ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [CurrentQuantity], [ExpiryDate]);
            """);
    }

    private void CreateIndexIfMissing(string indexName, string tableName, params string[] columns)
        => CreateIndexIfMissing(indexName, tableName, false, columns);

    private void CreateIndexIfMissing(string indexName, string tableName, bool unique, params string[] columns)
    {
        if (Schema.Table(tableName).Index(indexName).Exists())
        {
            return;
        }

        var index = Create.Index(indexName).OnTable(tableName);
        if (unique)
        {
            index.WithOptions().Unique();
        }

        foreach (var column in columns)
        {
            index.OnColumn(column).Ascending();
        }
    }

    private void DeleteIndexIfExists(string indexName, string tableName)
    {
        if (Schema.Table(tableName).Index(indexName).Exists())
        {
            Delete.Index(indexName).OnTable(tableName);
        }
    }
}
