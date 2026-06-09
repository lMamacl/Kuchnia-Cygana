using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(401, "Add logistics query indexes")]
public sealed class AddLogisticsQueryIndexes : Migration
{
    public override void Up()
    {
        CreateIndexIfMissing(
            "IX_DeliveryRoutes_Date_Deleted",
            "DeliveryRoutes",
            new[] { "RouteDate", "IsDeleted" });
        CreateIndexIfMissing(
            "IX_DeliveryRouteStops_Route_Sequence",
            "DeliveryRouteStops",
            new[] { "RouteId", "SequenceNumber", "IsDeleted" });
        CreateIndexIfMissing(
            "IX_DeliveryRouteStops_Calendar",
            "DeliveryRouteStops",
            new[] { "DeliveryCalendarId", "IsDeleted" });
    }

    public override void Down()
    {
        DeleteIndexIfExists("IX_DeliveryRouteStops_Calendar", "DeliveryRouteStops");
        DeleteIndexIfExists("IX_DeliveryRouteStops_Route_Sequence", "DeliveryRouteStops");
        DeleteIndexIfExists("IX_DeliveryRoutes_Date_Deleted", "DeliveryRoutes");
    }

    private void CreateIndexIfMissing(string indexName, string tableName, IReadOnlyList<string> columns)
    {
        if (!Schema.Table(tableName).Exists())
        {
            return;
        }

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
        if (!Schema.Table(tableName).Exists())
        {
            return;
        }

        if (Schema.Table(tableName).Index(indexName).Exists())
        {
            Delete.Index(indexName).OnTable(tableName);
        }
    }
}
