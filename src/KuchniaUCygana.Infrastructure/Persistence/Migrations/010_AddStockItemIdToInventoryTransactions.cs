using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(10)]
public sealed class AddStockItemIdToInventoryTransactions : Migration
{
    public override void Up()
    {
        Alter.Table("InventoryTransactions")
            .AddColumn("StockItemId").AsInt32().Nullable().ForeignKey("FK_InventoryTransactions_StockItems", "StockItems", "Id");

        // Backfill data
        Execute.Sql(@"
            UPDATE IT
            SET IT.StockItemId = B.StockItemId
            FROM InventoryTransactions IT
            INNER JOIN Batches B ON B.Id = IT.BatchId
        ");

        // Create index for performance
        Create.Index("IX_InventoryTransactions_StockItemId")
            .OnTable("InventoryTransactions")
            .OnColumn("StockItemId");
    }

    public override void Down()
    {
        Delete.Index("IX_InventoryTransactions_StockItemId").OnTable("InventoryTransactions");
        Delete.ForeignKey("FK_InventoryTransactions_StockItems").OnTable("InventoryTransactions");
        Delete.Column("StockItemId").FromTable("InventoryTransactions");
    }
}
