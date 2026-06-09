using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(20)]
public sealed class AddPhysicalPackingBagsAndBoxLabels : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingBags").Exists())
        {
            Create.Table("PackingBags")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("PackingSessionId").AsInt32().NotNullable().ForeignKey("PackingSessions", "Id")
                .WithColumn("BagNumber").AsInt32().NotNullable()
                .WithColumn("BagCode").AsString(100).NotNullable()
                .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("PackedAt").AsDateTimeOffset().Nullable()
                .WithColumn("PackedBy").AsString(50).Nullable()
                .WithColumn("LabeledAt").AsDateTimeOffset().Nullable()
                .WithColumn("ManifestedAt").AsDateTimeOffset().Nullable()
                .WithColumn("LoadedAt").AsDateTimeOffset().Nullable()
                .WithColumn("LoadedBy").AsString(50).Nullable()
                .WithColumn("DispatchedAt").AsDateTimeOffset().Nullable()
                .WithColumn("CreatedBy").AsString(50).Nullable()
                .WithColumn("UpdatedBy").AsString(50).Nullable()
                .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
                .WithColumn("DeletedBy").AsString(50).Nullable()
                .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
                .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

            Create.Index("IX_PackingBags_Session_Number")
                .OnTable("PackingBags")
                .OnColumn("PackingSessionId").Ascending()
                .OnColumn("BagNumber").Ascending()
                .WithOptions().Unique();

            Create.Index("IX_PackingBags_BagCode")
                .OnTable("PackingBags")
                .OnColumn("BagCode").Ascending()
                .WithOptions().Unique();
        }

        if (!Schema.Table("BoxLabels").Exists())
        {
            Create.Table("BoxLabels")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("PackingItemId").AsInt32().NotNullable().ForeignKey("PackingItems", "Id")
                .WithColumn("QrCode").AsString(100).NotNullable()
                .WithColumn("LabelDataJson").AsCustom("NVARCHAR(MAX)").NotNullable()
                .WithColumn("PrintNumber").AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn("ReprintReason").AsString(250).Nullable()
                .WithColumn("PrintedAt").AsDateTimeOffset().NotNullable()
                .WithColumn("PrintedBy").AsString(100).Nullable()
                .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
                .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

            Create.Index("IX_BoxLabels_QrCode")
                .OnTable("BoxLabels")
                .OnColumn("QrCode").Ascending();

            Create.Index("IX_BoxLabels_Item_Print")
                .OnTable("BoxLabels")
                .OnColumn("PackingItemId").Ascending()
                .OnColumn("PrintNumber").Ascending()
                .WithOptions().Unique();
        }

        if (!Schema.Table("PackingItems").Column("PackingBagId").Exists())
        {
            Alter.Table("PackingItems")
                .AddColumn("PackingBagId").AsInt32().Nullable();

            Create.Index("IX_PackingItems_PackingBagId")
                .OnTable("PackingItems")
                .OnColumn("PackingBagId").Ascending();
        }

        if (!Schema.Table("PackingItems").Column("ProductionPlanItemId").Exists())
        {
            Alter.Table("PackingItems")
                .AddColumn("ProductionPlanItemId").AsInt32().Nullable();

            Create.Index("IX_PackingItems_ProductionPlanItemId")
                .OnTable("PackingItems")
                .OnColumn("ProductionPlanItemId").Ascending();
        }

        if (!Schema.Table("PackingLabels").Column("PackingBagId").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("PackingBagId").AsInt32().Nullable();

            Create.Index("IX_PackingLabels_PackingBagId")
                .OnTable("PackingLabels")
                .OnColumn("PackingBagId").Ascending();
        }

        if (!Schema.Table("PackingLabels").Column("PrintNumber").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("PrintNumber").AsInt32().NotNullable().WithDefaultValue(1);
        }

        if (!Schema.Table("PackingLabels").Column("LabelDataJson").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("LabelDataJson").AsCustom("NVARCHAR(MAX)").Nullable();
        }

        if (!Schema.Table("PackingLabels").Column("PrintedAt").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("PrintedAt").AsDateTimeOffset().Nullable();
        }

        if (!Schema.Table("PackingLabels").Column("PrintedBy").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("PrintedBy").AsString(100).Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("ManifestVersion").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("ManifestVersion").AsInt32().NotNullable().WithDefaultValue(1)
                .AddColumn("SupersedesManifestId").AsInt32().Nullable()
                .AddColumn("ChangeReason").AsString(250).Nullable()
                .AddColumn("IsSuperseded").AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingManifests").Column("IsSuperseded").Exists())
        {
            Delete.Column("IsSuperseded").FromTable("PackingManifests");
            Delete.Column("ChangeReason").FromTable("PackingManifests");
            Delete.Column("SupersedesManifestId").FromTable("PackingManifests");
            Delete.Column("ManifestVersion").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingLabels").Column("PrintedBy").Exists())
        {
            Delete.Column("PrintedBy").FromTable("PackingLabels");
        }

        if (Schema.Table("PackingLabels").Column("PrintedAt").Exists())
        {
            Delete.Column("PrintedAt").FromTable("PackingLabels");
        }

        if (Schema.Table("PackingLabels").Column("LabelDataJson").Exists())
        {
            Delete.Column("LabelDataJson").FromTable("PackingLabels");
        }

        if (Schema.Table("PackingLabels").Column("PrintNumber").Exists())
        {
            Delete.Column("PrintNumber").FromTable("PackingLabels");
        }

        if (Schema.Table("PackingLabels").Column("PackingBagId").Exists())
        {
            if (Schema.Table("PackingLabels").Index("IX_PackingLabels_PackingBagId").Exists())
            {
                Delete.Index("IX_PackingLabels_PackingBagId").OnTable("PackingLabels");
            }

            Delete.Column("PackingBagId").FromTable("PackingLabels");
        }

        if (Schema.Table("PackingItems").Column("ProductionPlanItemId").Exists())
        {
            if (Schema.Table("PackingItems").Index("IX_PackingItems_ProductionPlanItemId").Exists())
            {
                Delete.Index("IX_PackingItems_ProductionPlanItemId").OnTable("PackingItems");
            }

            Delete.Column("ProductionPlanItemId").FromTable("PackingItems");
        }

        if (Schema.Table("PackingItems").Column("PackingBagId").Exists())
        {
            if (Schema.Table("PackingItems").Index("IX_PackingItems_PackingBagId").Exists())
            {
                Delete.Index("IX_PackingItems_PackingBagId").OnTable("PackingItems");
            }

            Delete.Column("PackingBagId").FromTable("PackingItems");
        }

        if (Schema.Table("BoxLabels").Exists())
        {
            Delete.Table("BoxLabels");
        }

        if (Schema.Table("PackingBags").Exists())
        {
            Delete.Table("PackingBags");
        }
    }
}
