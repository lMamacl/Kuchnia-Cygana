using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migracja 005: Tabele kompletacji dla Modułu 3.
/// Tabele: PackingSessions, PackingItems, PackingLabels
/// </summary>
[Migration(5)]
public class CreatePackingTables : Migration
{
    public override void Up()
    {
        // 1. PackingSessions (Torba)
        Create.Table("PackingSessions")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("PackingDate").AsDate().NotNullable()
            .WithColumn("OrderId").AsInt32().Nullable() // Bridge do Modułu 1
            .WithColumn("ClientName").AsString(150).Nullable()
            .WithColumn("PackedBy").AsString(50).Nullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("RouteId").AsInt32().Nullable() // Bridge do Modułu 4
            .WithColumn("StopNumber").AsInt32().Nullable()
            // AuditableEntity fields
            .WithColumn("CreatedBy").AsString(50).Nullable()
            .WithColumn("UpdatedBy").AsString(50).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime().Nullable()
            .WithColumn("DeletedBy").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        Create.Index("IX_PackingSessions_PackingDate")
            .OnTable("PackingSessions")
            .OnColumn("PackingDate")
            .Ascending();

        // 2. PackingItems (Pudełko)
        Create.Table("PackingItems")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("PackingSessionId").AsInt32().NotNullable().ForeignKey("PackingSessions", "Id")
            .WithColumn("MealId").AsInt32().NotNullable() // Bridge do Modułu 2
            .WithColumn("MealName").AsString(200).NotNullable().WithDefaultValue("")
            .WithColumn("DietVariantId").AsInt32().NotNullable() // Bridge do Modułu 2
            .WithColumn("BatchId").AsInt32().Nullable().ForeignKey("Batches", "Id") // HACCP traceability
            .WithColumn("ExpiryDate").AsDateTime().Nullable()
            .WithColumn("IsDamaged").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Remarks").AsString(250).Nullable()
            // AuditableEntity fields
            .WithColumn("CreatedBy").AsString(50).Nullable()
            .WithColumn("UpdatedBy").AsString(50).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime().Nullable()
            .WithColumn("DeletedBy").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        // 3. PackingLabels (Etykiety)
        Create.Table("PackingLabels")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("PackingItemId").AsInt32().Nullable() // Etykieta produktowa → pudełko
            .WithColumn("PackingSessionId").AsInt32().Nullable() // Etykieta wysyłkowa → sesja/torba
            .WithColumn("LabelType").AsInt32().NotNullable()
            .WithColumn("QrCode").AsString(100).NotNullable()
            .WithColumn("DishName").AsString(200).Nullable()
            .WithColumn("Allergens").AsString(500).Nullable()
            .WithColumn("Kcal").AsInt32().Nullable()
            .WithColumn("ClientName").AsString(150).Nullable()
            .WithColumn("RouteInfo").AsString(200).Nullable()
            .WithColumn("DeliveryWindow").AsString(50).Nullable()
            // BaseEntity fields
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        Create.Index("IX_PackingLabels_QrCode")
            .OnTable("PackingLabels")
            .OnColumn("QrCode")
            .Unique();
    }

    public override void Down()
    {
        Delete.Table("PackingLabels");
        Delete.Table("PackingItems");
        Delete.Table("PackingSessions");
    }
}
