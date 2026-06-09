using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migracja 006: Tabela korekt inwentaryzacyjnych dla Modułu 3.
/// </summary>
[Migration(6)]
public class CreateInventoryAdjustments : Migration
{
    public override void Up()
    {
        Create.Table("InventoryAdjustments")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("StockItemId").AsInt32().NotNullable().ForeignKey("StockItems", "Id")
            .WithColumn("QuantityBefore").AsDecimal().NotNullable()
            .WithColumn("QuantityAfter").AsDecimal().NotNullable()
            .WithColumn("Difference").AsDecimal().NotNullable()
            .WithColumn("Reason").AsString(500).NotNullable()
            .WithColumn("AdjustedBy").AsString(50).Nullable()
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
        Delete.Table("InventoryAdjustments");
    }
}
