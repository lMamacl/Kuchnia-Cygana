using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(105)]
public sealed class CreateDeliveryCalendarTable : Migration
{
    public override void Up()
    {
        Create.Table("DeliveryCalendar")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OrderId").AsInt32().NotNullable().ForeignKey("Orders", "Id")
            .WithColumn("AddressId").AsInt32().NotNullable().ForeignKey("Addresses", "Id")
            .WithColumn("DeliveryWindowId").AsInt32().Nullable()
                .ForeignKey("DeliveryWindows", "Id")
            .WithColumn("DeliveryDate").AsDateTime().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsSkipped").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("SkipReason").AsString(500).Nullable()
            .WithColumn("CutoffTime").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();

        Create.Index("IX_DeliveryCalendar_DeliveryDate")
            .OnTable("DeliveryCalendar")
            .OnColumn("DeliveryDate");
    }

    public override void Down() => Delete.Table("DeliveryCalendar");
}
