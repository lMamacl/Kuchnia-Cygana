using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(28)]
public sealed class HardenPackingQueryIndexes : Migration
{
    public override void Up()
    {
        CreateIndexIfMissing(
            "IX_PackingLabels_Shipping_Bag_Print",
            "PackingLabels",
            new[] { "LabelType", "PackingBagId", "PrintNumber", "Id" });
        CreateIndexIfMissing(
            "IX_PackingManifests_Date_Route_Current",
            "PackingManifests",
            new[] { "PackingDate", "RouteId", "IsSuperseded", "GeneratedAt", "Id" });
        CreateIndexIfMissing(
            "IX_PackingSessions_Date_Calendar",
            "PackingSessions",
            new[] { "PackingDate", "IsDeleted", "DeliveryCalendarId" });
        CreateIndexIfMissing(
            "IX_PackingItems_Session_Status",
            "PackingItems",
            new[] { "PackingSessionId", "IsDeleted", "Status" });
        CreateIndexIfMissing(
            "IX_PackingItems_Bag_Status",
            "PackingItems",
            new[] { "PackingBagId", "IsDeleted", "Status" });
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
        CreateIndexIfMissing(
            "IX_PackingIncidents_Reported_Status_Type",
            "PackingIncidents",
            new[] { "ReportedAt", "Status", "Type" });
    }

    public override void Down()
    {
        DeleteIndexIfExists("IX_PackingIncidents_Reported_Status_Type", "PackingIncidents");
        DeleteIndexIfExists("IX_DeliveryRouteStops_Calendar", "DeliveryRouteStops");
        DeleteIndexIfExists("IX_DeliveryRouteStops_Route_Sequence", "DeliveryRouteStops");
        DeleteIndexIfExists("IX_DeliveryRoutes_Date_Deleted", "DeliveryRoutes");
        DeleteIndexIfExists("IX_PackingItems_Bag_Status", "PackingItems");
        DeleteIndexIfExists("IX_PackingItems_Session_Status", "PackingItems");
        DeleteIndexIfExists("IX_PackingSessions_Date_Calendar", "PackingSessions");
        DeleteIndexIfExists("IX_PackingManifests_Date_Route_Current", "PackingManifests");
        DeleteIndexIfExists("IX_PackingLabels_Shipping_Bag_Print", "PackingLabels");
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
