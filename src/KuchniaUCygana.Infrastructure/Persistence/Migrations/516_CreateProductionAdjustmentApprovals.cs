using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(516)]
public sealed class CreateProductionAdjustmentApprovals : Migration
{
    public override void Up()
    {
        if (Schema.Table("ProductionAdjustmentApprovals").Exists())
        {
            return;
        }

        Create.Table("ProductionAdjustmentApprovals")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProductionPlanItemId").AsInt32().NotNullable()
            .WithColumn("AdjustmentType").AsString(50).NotNullable().WithDefaultValue("CookedQuantity")
            .WithColumn("Status").AsString(30).NotNullable().WithDefaultValue("Pending")
            .WithColumn("PlannedValue").AsDecimal(18, 3).NotNullable()
            .WithColumn("RequestedValue").AsDecimal(18, 3).NotNullable()
            .WithColumn("Unit").AsString(20).NotNullable().WithDefaultValue("portion")
            .WithColumn("Reason").AsString(500).NotNullable()
            .WithColumn("RequestedBy").AsString(120).NotNullable()
            .WithColumn("RequestedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("ApprovedBy").AsString(120).Nullable()
            .WithColumn("ApprovedAt").AsDateTimeOffset().Nullable()
            .WithColumn("ApprovalNote").AsString(500).Nullable()
            .WithColumn("AppliedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.Index("IX_ProductionAdjustmentApprovals_PlanItem_Status")
            .OnTable("ProductionAdjustmentApprovals")
            .OnColumn("ProductionPlanItemId").Ascending()
            .OnColumn("Status").Ascending()
            .WithOptions().NonClustered();
    }

    public override void Down()
    {
        if (Schema.Table("ProductionAdjustmentApprovals").Exists())
        {
            Delete.Table("ProductionAdjustmentApprovals");
        }
    }
}
