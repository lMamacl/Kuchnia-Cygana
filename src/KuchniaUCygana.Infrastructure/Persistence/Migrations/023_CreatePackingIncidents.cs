using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(23)]
public sealed class CreatePackingIncidents : Migration
{
    public override void Up()
    {
        if (Schema.Table("PackingIncidents").Exists())
        {
            return;
        }

        Create.Table("PackingIncidents")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Type").AsInt32().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ReasonFlags").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("PackingDate").AsDate().NotNullable()
            .WithColumn("PackingSessionId").AsInt32().NotNullable()
            .WithColumn("PackingItemId").AsInt32().Nullable()
            .WithColumn("PackingBagId").AsInt32().Nullable()
            .WithColumn("ReplacementPackingItemId").AsInt32().Nullable()
            .WithColumn("ReplacementPackingBagId").AsInt32().Nullable()
            .WithColumn("ClientPublicId").AsString(50).Nullable()
            .WithColumn("DeliveryCalendarId").AsInt32().Nullable()
            .WithColumn("MealId").AsInt32().Nullable()
            .WithColumn("MealName").AsString(200).Nullable()
            .WithColumn("BoxCode").AsString(100).Nullable()
            .WithColumn("BagCode").AsString(100).Nullable()
            .WithColumn("Description").AsString(1000).NotNullable().WithDefaultValue(string.Empty)
            .WithColumn("ReportedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("ReportedByUserId").AsInt32().Nullable()
            .WithColumn("AssignedToUserId").AsInt32().Nullable()
            .WithColumn("AdminNotes").AsString(2000).Nullable()
            .WithColumn("WarehouseWasteRegisteredAt").AsDateTimeOffset().Nullable()
            .WithColumn("WarehouseWasteError").AsString(1000).Nullable()
            .WithColumn("KitchenStartedAt").AsDateTimeOffset().Nullable()
            .WithColumn("KitchenPreparedAt").AsDateTimeOffset().Nullable()
            .WithColumn("ResolvedAt").AsDateTimeOffset().Nullable()
            .WithColumn("ResolvedByUserId").AsInt32().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.Index("IX_PackingIncidents_Date_Status")
            .OnTable("PackingIncidents")
            .OnColumn("PackingDate").Ascending()
            .OnColumn("Status").Ascending();

        Create.Index("IX_PackingIncidents_Session")
            .OnTable("PackingIncidents")
            .OnColumn("PackingSessionId").Ascending();

        Create.Index("IX_PackingIncidents_Item")
            .OnTable("PackingIncidents")
            .OnColumn("PackingItemId").Ascending();

        Create.Index("IX_PackingIncidents_Bag")
            .OnTable("PackingIncidents")
            .OnColumn("PackingBagId").Ascending();

        Create.Index("IX_PackingIncidents_DeliveryCalendar")
            .OnTable("PackingIncidents")
            .OnColumn("DeliveryCalendarId").Ascending();
    }

    public override void Down()
    {
        if (Schema.Table("PackingIncidents").Exists())
        {
            Delete.Table("PackingIncidents");
        }
    }
}
