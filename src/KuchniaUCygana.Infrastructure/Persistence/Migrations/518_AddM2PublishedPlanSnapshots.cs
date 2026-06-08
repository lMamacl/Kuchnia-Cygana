using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(518)]
public sealed class AddM2PublishedPlanSnapshots : Migration
{
    public override void Up()
    {
        if (!Schema.Table("DietMenuPlanItems").Column("PublishedSnapshotJson").Exists())
        {
            Alter.Table("DietMenuPlanItems")
                .AddColumn("PublishedSnapshotJson").AsString(int.MaxValue).Nullable();
        }

        if (!Schema.Table("DietMenuPlanItems").Column("PublishedSnapshotHash").Exists())
        {
            Alter.Table("DietMenuPlanItems")
                .AddColumn("PublishedSnapshotHash").AsString(64).Nullable();
        }

        if (!Schema.Table("DietMenuPlanItems").Column("PublishedSnapshotCreatedAt").Exists())
        {
            Alter.Table("DietMenuPlanItems")
                .AddColumn("PublishedSnapshotCreatedAt").AsDateTimeOffset().Nullable();
        }

        if (!Schema.Table("DietMenuPlans").Column("PublishedSnapshotHash").Exists())
        {
            Alter.Table("DietMenuPlans")
                .AddColumn("PublishedSnapshotHash").AsString(64).Nullable();
        }

        if (!Schema.Table("DietMenuPlans").Column("PublishedSnapshotItemCount").Exists())
        {
            Alter.Table("DietMenuPlans")
                .AddColumn("PublishedSnapshotItemCount").AsInt32().Nullable();
        }

        CreateIndexIfMissing(
            "IX_DietMenuPlanItems_PublishedSnapshotHash",
            "DietMenuPlanItems",
            "PublishedSnapshotHash");
    }

    public override void Down()
    {
        DeleteIndexIfExists("IX_DietMenuPlanItems_PublishedSnapshotHash", "DietMenuPlanItems");

        DeleteColumnIfExists("PublishedSnapshotItemCount", "DietMenuPlans");
        DeleteColumnIfExists("PublishedSnapshotHash", "DietMenuPlans");
        DeleteColumnIfExists("PublishedSnapshotCreatedAt", "DietMenuPlanItems");
        DeleteColumnIfExists("PublishedSnapshotHash", "DietMenuPlanItems");
        DeleteColumnIfExists("PublishedSnapshotJson", "DietMenuPlanItems");
    }

    private void CreateIndexIfMissing(string indexName, string tableName, params string[] columns)
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

    private void DeleteColumnIfExists(string columnName, string tableName)
    {
        if (Schema.Table(tableName).Column(columnName).Exists())
        {
            Delete.Column(columnName).FromTable(tableName);
        }
    }
}
