using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(29)]
public sealed class HardenWarehouseQueryIndexes : Migration
{
    public override void Up()
    {
        CreateIndexIfMissing(
            "IX_Batches_StockItem_Active_Expiry",
            "Batches",
            new[] { "StockItemId", "IsDeleted", "IsDepleted", "CurrentQuantity", "ExpiryDate" });
        CreateIndexIfMissing(
            "IX_Batches_Expiry_Active",
            "Batches",
            new[] { "ExpiryDate", "IsDeleted", "IsDepleted", "CurrentQuantity" });
        CreateIndexIfMissing(
            "IX_InventoryTransactions_Created_Stock_Type",
            "InventoryTransactions",
            new[] { "CreatedAt", "StockItemId", "TransactionType" });
    }

    public override void Down()
    {
        DeleteIndexIfExists("IX_InventoryTransactions_Created_Stock_Type", "InventoryTransactions");
        DeleteIndexIfExists("IX_Batches_Expiry_Active", "Batches");
        DeleteIndexIfExists("IX_Batches_StockItem_Active_Expiry", "Batches");
    }

    private void CreateIndexIfMissing(string indexName, string tableName, IReadOnlyList<string> columns)
    {
        if (Schema.Table(tableName).Index(indexName).Exists())
        {
            return;
        }

        var index = Create.Index(indexName).OnTable(tableName);
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
