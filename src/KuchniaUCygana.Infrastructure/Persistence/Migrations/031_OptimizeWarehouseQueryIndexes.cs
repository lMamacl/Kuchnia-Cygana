using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(31)]
public sealed class OptimizeWarehouseQueryIndexes : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_StockItem_Active_Expiry' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                DROP INDEX [IX_Batches_StockItem_Active_Expiry] ON [dbo].[Batches];

            CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
                ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [CurrentQuantity], [ExpiryDate])
                INCLUDE ([SupplierBatchNumber], [ReceivedDate]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_Category_IsDeleted_Name_Covering' AND object_id = OBJECT_ID(N'[dbo].[StockItems]'))
                CREATE INDEX [IX_StockItems_Category_IsDeleted_Name_Covering]
                    ON [dbo].[StockItems] ([WarehouseCategoryId], [IsDeleted], [Name], [Id])
                    INCLUDE ([BaseIngredientId], [DefaultUnitOfMeasureId], [MinimumLevel], [LeadTimeDays]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_IsDeleted_Name_Covering' AND object_id = OBJECT_ID(N'[dbo].[StockItems]'))
                CREATE INDEX [IX_StockItems_IsDeleted_Name_Covering]
                    ON [dbo].[StockItems] ([IsDeleted], [Name], [Id])
                    INCLUDE ([WarehouseCategoryId], [BaseIngredientId], [DefaultUnitOfMeasureId], [MinimumLevel], [LeadTimeDays]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_StockItem_Active_BatchNumber' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                CREATE INDEX [IX_Batches_StockItem_Active_BatchNumber]
                    ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [SupplierBatchNumber])
                    INCLUDE ([CurrentQuantity], [ExpiryDate], [ReceivedDate]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_BatchNumber_Active_StockItem' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                CREATE INDEX [IX_Batches_BatchNumber_Active_StockItem]
                    ON [dbo].[Batches] ([SupplierBatchNumber], [IsDeleted], [IsDepleted], [StockItemId])
                    INCLUDE ([CurrentQuantity], [ExpiryDate], [ReceivedDate]);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_BatchNumber_Active_StockItem' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                DROP INDEX [IX_Batches_BatchNumber_Active_StockItem] ON [dbo].[Batches];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_StockItem_Active_BatchNumber' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                DROP INDEX [IX_Batches_StockItem_Active_BatchNumber] ON [dbo].[Batches];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_IsDeleted_Name_Covering' AND object_id = OBJECT_ID(N'[dbo].[StockItems]'))
                DROP INDEX [IX_StockItems_IsDeleted_Name_Covering] ON [dbo].[StockItems];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_Category_IsDeleted_Name_Covering' AND object_id = OBJECT_ID(N'[dbo].[StockItems]'))
                DROP INDEX [IX_StockItems_Category_IsDeleted_Name_Covering] ON [dbo].[StockItems];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_StockItem_Active_Expiry' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                DROP INDEX [IX_Batches_StockItem_Active_Expiry] ON [dbo].[Batches];

            CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
                ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [ExpiryDate])
                INCLUDE ([CurrentQuantity], [SupplierBatchNumber]);
            """);
    }
}
