using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(509)]
public sealed class AddM2ReferencesToOrderItems : Migration
{
    public override void Up()
    {
        if (!Schema.Table("OrderItems").Column("MealId").Exists())
        {
            Alter.Table("OrderItems").AddColumn("MealId").AsInt32().Nullable();
            Create.Index("IX_OrderItems_MealId")
                .OnTable("OrderItems")
                .OnColumn("MealId");
        }

        if (!Schema.Table("OrderItems").Column("MealVariantId").Exists())
        {
            Alter.Table("OrderItems").AddColumn("MealVariantId").AsInt32().Nullable();
            Create.Index("IX_OrderItems_MealVariantId")
                .OnTable("OrderItems")
                .OnColumn("MealVariantId");
        }

        if (!Schema.Table("OrderItems").Column("DietMenuPlanItemId").Exists())
        {
            Alter.Table("OrderItems").AddColumn("DietMenuPlanItemId").AsInt32().Nullable();
            Create.Index("IX_OrderItems_DietMenuPlanItemId")
                .OnTable("OrderItems")
                .OnColumn("DietMenuPlanItemId");
        }

        if (!Schema.Table("OrderItems").Column("MealSlot").Exists())
        {
            Alter.Table("OrderItems").AddColumn("MealSlot").AsString(50).Nullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("OrderItems").Column("DietMenuPlanItemId").Exists())
        {
            Delete.Index("IX_OrderItems_DietMenuPlanItemId").OnTable("OrderItems");
            Delete.Column("DietMenuPlanItemId").FromTable("OrderItems");
        }

        if (Schema.Table("OrderItems").Column("MealVariantId").Exists())
        {
            Delete.Index("IX_OrderItems_MealVariantId").OnTable("OrderItems");
            Delete.Column("MealVariantId").FromTable("OrderItems");
        }

        if (Schema.Table("OrderItems").Column("MealId").Exists())
        {
            Delete.Index("IX_OrderItems_MealId").OnTable("OrderItems");
            Delete.Column("MealId").FromTable("OrderItems");
        }

        if (Schema.Table("OrderItems").Column("MealSlot").Exists())
        {
            Delete.Column("MealSlot").FromTable("OrderItems");
        }
    }
}
