using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(2)]
public class CreateWarehouseTables : Migration
{
    public override void Up()
    {
        // 1. UnitsOfMeasure
        Create.Table("UnitsOfMeasure")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Symbol").AsString(10).NotNullable()
            .WithColumn("Name").AsString(50).NotNullable()
            .WithColumn("Description").AsString(255).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        // 2. StockItems
        Create.Table("StockItems")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(150).NotNullable()
            .WithColumn("BaseIngredientId").AsInt32().Nullable() // Relacja do mocka Moduł 2
            .WithColumn("DefaultUnitOfMeasureId").AsInt32().NotNullable().ForeignKey("UnitsOfMeasure", "Id")
            .WithColumn("MinimumLevel").AsDecimal().NotNullable()
            .WithColumn("LeadTimeDays").AsInt32().NotNullable()
            // AuditableEntity fields
            .WithColumn("CreatedBy").AsString(50).Nullable()
            .WithColumn("UpdatedBy").AsString(50).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime().Nullable()
            .WithColumn("DeletedBy").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        // 3. Batches
        Create.Table("Batches")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("StockItemId").AsInt32().NotNullable().ForeignKey("StockItems", "Id")
            .WithColumn("SupplierBatchNumber").AsString(50).NotNullable()
            .WithColumn("CurrentQuantity").AsDecimal().NotNullable()
            .WithColumn("ExpiryDate").AsDateTime().Nullable()
            .WithColumn("ReceivedDate").AsDateTime().NotNullable()
            .WithColumn("IsDepleted").AsBoolean().NotNullable().WithDefaultValue(false)
            // AuditableEntity fields
            .WithColumn("CreatedBy").AsString(50).Nullable()
            .WithColumn("UpdatedBy").AsString(50).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime().Nullable()
            .WithColumn("DeletedBy").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        // 4. InventoryTransactions
        Create.Table("InventoryTransactions")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("BatchId").AsInt32().NotNullable().ForeignKey("Batches", "Id")
            .WithColumn("TransactionType").AsInt32().NotNullable() // Enum
            .WithColumn("QuantityChanged").AsDecimal().NotNullable()
            .WithColumn("Reason").AsString(250).Nullable()
            .WithColumn("ReferenceDocument").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        // 5. TemperatureLogs
        Create.Table("TemperatureLogs")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity() // Int64 ze względu na potencjalnie duże przyrosty
            .WithColumn("DeviceNameOrLocation").AsString(50).NotNullable()
            .WithColumn("RecordedTemperatureCelsius").AsDecimal().NotNullable()
            .WithColumn("RecordedAt").AsDateTime().NotNullable()
            .WithColumn("Remarks").AsString(255).Nullable()
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
        Delete.Table("TemperatureLogs");
        Delete.Table("InventoryTransactions");
        Delete.Table("Batches");
        Delete.Table("StockItems");
        Delete.Table("UnitsOfMeasure");
    }
}
