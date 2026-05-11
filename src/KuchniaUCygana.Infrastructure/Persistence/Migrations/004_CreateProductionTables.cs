using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migracja 004: Tabele produkcyjne dla Modułu 3.
/// Tabele: ProductionPlans, ProductionPlanItems, ProductionBatches
/// </summary>
[Migration(4)]
public class CreateProductionTables : Migration
{
    public override void Up()
    {
        // 1. ProductionPlans
        Create.Table("ProductionPlans")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProductionDate").AsDate().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Notes").AsString(500).Nullable()
            // Model B-lite: etapowy rozwóz
            .WithColumn("IsSharedWithLogistics").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("SharedAt").AsDateTime().Nullable()
            // AuditableEntity fields
            .WithColumn("CreatedBy").AsString(50).Nullable()
            .WithColumn("UpdatedBy").AsString(50).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime().Nullable()
            .WithColumn("DeletedBy").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        Create.Index("IX_ProductionPlans_ProductionDate")
            .OnTable("ProductionPlans")
            .OnColumn("ProductionDate")
            .Ascending();

        // 2. ProductionPlanItems
        Create.Table("ProductionPlanItems")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProductionPlanId").AsInt32().NotNullable().ForeignKey("ProductionPlans", "Id")
            .WithColumn("MealId").AsInt32().NotNullable() // Bridge do Modułu 2
            .WithColumn("MealName").AsString(200).NotNullable().WithDefaultValue("")
            .WithColumn("DietVariantId").AsInt32().NotNullable() // Bridge do Modułu 2
            .WithColumn("PlannedQuantity").AsInt32().NotNullable()
            .WithColumn("CookedQuantity").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            // Model B-lite: grupy produkcyjne i ETA
            .WithColumn("ProductionGroup").AsInt32().Nullable()
            .WithColumn("EstimatedReadyTime").AsTime().Nullable()
            .WithColumn("ActualReadyTime").AsTime().Nullable()
            // AuditableEntity fields
            .WithColumn("CreatedBy").AsString(50).Nullable()
            .WithColumn("UpdatedBy").AsString(50).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime().Nullable()
            .WithColumn("DeletedBy").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        // 3. ProductionBatches (półprodukty)
        Create.Table("ProductionBatches")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProductionPlanId").AsInt32().NotNullable().ForeignKey("ProductionPlans", "Id")
            .WithColumn("MealId").AsInt32().NotNullable() // Bridge do Modułu 2
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("PlannedQuantity").AsDecimal().NotNullable()
            .WithColumn("ProducedQuantity").AsDecimal().NotNullable().WithDefaultValue(0)
            // AuditableEntity fields
            .WithColumn("CreatedBy").AsString(50).Nullable()
            .WithColumn("UpdatedBy").AsString(50).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime().Nullable()
            .WithColumn("DeletedBy").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Table("ProductionBatches");
        Delete.Table("ProductionPlanItems");
        Delete.Table("ProductionPlans");
    }
}
