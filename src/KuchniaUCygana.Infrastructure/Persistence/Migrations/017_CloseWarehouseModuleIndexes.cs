using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(17)]
public sealed class CloseWarehouseModuleIndexes : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            UPDATE it
            SET it.[StockItemId] = b.[StockItemId]
            FROM [dbo].[InventoryTransactions] it
            INNER JOIN [dbo].[Batches] b ON b.[Id] = it.[BatchId]
            WHERE it.[StockItemId] IS NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemperatureLogs_IsDeleted_RecordedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[TemperatureLogs]'))
                CREATE INDEX [IX_TemperatureLogs_IsDeleted_RecordedAt_Id]
                ON [dbo].[TemperatureLogs] ([IsDeleted], [RecordedAt] DESC, [Id] DESC)
                INCLUDE ([DeviceNameOrLocation], [RecordedTemperatureCelsius], [Remarks]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemperatureLogs_Device_IsDeleted_RecordedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[TemperatureLogs]'))
                CREATE INDEX [IX_TemperatureLogs_Device_IsDeleted_RecordedAt_Id]
                ON [dbo].[TemperatureLogs] ([DeviceNameOrLocation], [IsDeleted], [RecordedAt] DESC, [Id] DESC)
                INCLUDE ([RecordedTemperatureCelsius], [Remarks]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryAdjustments_StockItem_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]'))
                CREATE INDEX [IX_InventoryAdjustments_StockItem_CreatedAt]
                ON [dbo].[InventoryAdjustments] ([StockItemId], [CreatedAt] DESC)
                INCLUDE ([QuantityBefore], [QuantityAfter], [Difference]);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryAdjustments_StockItem_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]'))
                DROP INDEX [IX_InventoryAdjustments_StockItem_CreatedAt] ON [dbo].[InventoryAdjustments];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemperatureLogs_Device_IsDeleted_RecordedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[TemperatureLogs]'))
                DROP INDEX [IX_TemperatureLogs_Device_IsDeleted_RecordedAt_Id] ON [dbo].[TemperatureLogs];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemperatureLogs_IsDeleted_RecordedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[TemperatureLogs]'))
                DROP INDEX [IX_TemperatureLogs_IsDeleted_RecordedAt_Id] ON [dbo].[TemperatureLogs];
            """);
    }
}
