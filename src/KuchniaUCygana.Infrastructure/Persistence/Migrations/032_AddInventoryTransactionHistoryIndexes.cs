using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(32)]
public sealed class AddInventoryTransactionHistoryIndexes : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            IF COL_LENGTH(N'dbo.InventoryTransactions', N'CreatedBy') IS NULL
                ALTER TABLE [dbo].[InventoryTransactions] ADD [CreatedBy] NVARCHAR(50) NULL;

            IF COL_LENGTH(N'dbo.InventoryTransactions', N'UpdatedBy') IS NULL
                ALTER TABLE [dbo].[InventoryTransactions] ADD [UpdatedBy] NVARCHAR(50) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryTransactions_ReferenceDocument' AND object_id = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
                CREATE INDEX [IX_InventoryTransactions_ReferenceDocument]
                    ON [dbo].[InventoryTransactions] ([ReferenceDocument])
                    INCLUDE ([Id], [StockItemId], [BatchId], [CreatedAt]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryTransactions_Stock_Type_Created' AND object_id = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
                CREATE INDEX [IX_InventoryTransactions_Stock_Type_Created]
                    ON [dbo].[InventoryTransactions] ([StockItemId], [TransactionType], [CreatedAt] DESC, [Id] DESC)
                    INCLUDE ([BatchId], [QuantityChanged], [Reason], [ReferenceDocument], [CreatedBy], [UpdatedBy]);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryTransactions_Stock_Type_Created' AND object_id = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
                DROP INDEX [IX_InventoryTransactions_Stock_Type_Created] ON [dbo].[InventoryTransactions];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryTransactions_ReferenceDocument' AND object_id = OBJECT_ID(N'[dbo].[InventoryTransactions]'))
                DROP INDEX [IX_InventoryTransactions_ReferenceDocument] ON [dbo].[InventoryTransactions];

            IF COL_LENGTH(N'dbo.InventoryTransactions', N'UpdatedBy') IS NOT NULL
                ALTER TABLE [dbo].[InventoryTransactions] DROP COLUMN [UpdatedBy];

            IF COL_LENGTH(N'dbo.InventoryTransactions', N'CreatedBy') IS NOT NULL
                ALTER TABLE [dbo].[InventoryTransactions] DROP COLUMN [CreatedBy];
            """);
    }
}
