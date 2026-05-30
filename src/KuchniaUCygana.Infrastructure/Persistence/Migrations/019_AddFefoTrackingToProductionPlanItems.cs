using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(19)]
public sealed class AddFefoTrackingToProductionPlanItems : Migration
{
    public override void Up()
    {
        Alter.Table("ProductionPlanItems")
            .AddColumn("FefoDeductedAt").AsDateTimeOffset().Nullable()
            .AddColumn("FefoReferenceDocument").AsString(50).Nullable();

        Create.Index("IX_ProductionPlanItems_FefoDeductedAt")
            .OnTable("ProductionPlanItems")
            .OnColumn("FefoDeductedAt");
    }

    public override void Down()
    {
        Delete.Index("IX_ProductionPlanItems_FefoDeductedAt").OnTable("ProductionPlanItems");
        Delete.Column("FefoReferenceDocument").FromTable("ProductionPlanItems");
        Delete.Column("FefoDeductedAt").FromTable("ProductionPlanItems");
    }
}
