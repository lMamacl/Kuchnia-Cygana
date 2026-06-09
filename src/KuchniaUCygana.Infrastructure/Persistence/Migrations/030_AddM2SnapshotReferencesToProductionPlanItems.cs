using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(30)]
public sealed class AddM2SnapshotReferencesToProductionPlanItems : Migration
{
    public override void Up()
    {
        Alter.Table("ProductionPlanItems")
            .AddColumn("DietMenuPlanItemId").AsInt32().Nullable()
            .AddColumn("RecipeComponentVersionIds").AsString(500).Nullable()
            .AddColumn("M2SnapshotHash").AsString(64).Nullable()
            .AddColumn("M2SnapshotJson").AsCustom("nvarchar(max)").Nullable()
            .AddColumn("PackagingDeductedAt").AsDateTimeOffset().Nullable()
            .AddColumn("PackagingReferenceDocument").AsString(50).Nullable();

        Create.Index("IX_ProductionPlanItems_DietMenuPlanItemId")
            .OnTable("ProductionPlanItems")
            .OnColumn("DietMenuPlanItemId");
    }

    public override void Down()
    {
        Delete.Index("IX_ProductionPlanItems_DietMenuPlanItemId").OnTable("ProductionPlanItems");
        Delete.Column("PackagingReferenceDocument").FromTable("ProductionPlanItems");
        Delete.Column("PackagingDeductedAt").FromTable("ProductionPlanItems");
        Delete.Column("M2SnapshotJson").FromTable("ProductionPlanItems");
        Delete.Column("M2SnapshotHash").FromTable("ProductionPlanItems");
        Delete.Column("RecipeComponentVersionIds").FromTable("ProductionPlanItems");
        Delete.Column("DietMenuPlanItemId").FromTable("ProductionPlanItems");
    }
}
