using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(16)]
public sealed class AddWarehouseSearchIndexes : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_IsDeleted_Name' AND object_id = OBJECT_ID(N'[dbo].[StockItems]'))
                CREATE INDEX [IX_StockItems_IsDeleted_Name]
                ON [dbo].[StockItems] ([IsDeleted], [Name]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_StockItem_Active_Expiry' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
                ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [ExpiryDate])
                INCLUDE ([CurrentQuantity], [SupplierBatchNumber]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_Active_Expiry' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                CREATE INDEX [IX_Batches_Active_Expiry]
                ON [dbo].[Batches] ([IsDeleted], [IsDepleted], [ExpiryDate])
                INCLUDE ([StockItemId], [CurrentQuantity], [SupplierBatchNumber]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_SupplierBatchNumber' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                CREATE INDEX [IX_Batches_SupplierBatchNumber]
                ON [dbo].[Batches] ([SupplierBatchNumber]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryTransactions_StockItem_CreatedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
                CREATE INDEX [IX_InventoryTransactions_StockItem_CreatedAt_Id]
                ON [dbo].[InventoryTransactions] ([StockItemId], [CreatedAt] DESC, [Id] DESC);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryTransactions_CreatedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
                CREATE INDEX [IX_InventoryTransactions_CreatedAt_Id]
                ON [dbo].[InventoryTransactions] ([CreatedAt] DESC, [Id] DESC);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryTransactions_CreatedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
                DROP INDEX [IX_InventoryTransactions_CreatedAt_Id] ON [dbo].[InventoryTransactions];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryTransactions_StockItem_CreatedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
                DROP INDEX [IX_InventoryTransactions_StockItem_CreatedAt_Id] ON [dbo].[InventoryTransactions];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_SupplierBatchNumber' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                DROP INDEX [IX_Batches_SupplierBatchNumber] ON [dbo].[Batches];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_Active_Expiry' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                DROP INDEX [IX_Batches_Active_Expiry] ON [dbo].[Batches];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_StockItem_Active_Expiry' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
                DROP INDEX [IX_Batches_StockItem_Active_Expiry] ON [dbo].[Batches];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_IsDeleted_Name' AND object_id = OBJECT_ID(N'[dbo].[StockItems]'))
                DROP INDEX [IX_StockItems_IsDeleted_Name] ON [dbo].[StockItems];
            """);
    }
}
