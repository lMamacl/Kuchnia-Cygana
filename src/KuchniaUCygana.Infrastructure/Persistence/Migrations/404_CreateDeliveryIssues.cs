using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(404, "Create driver delivery issues")]
public sealed class CreateDeliveryIssues : Migration
{
    public override void Up()
    {
        if (Schema.Table("DeliveryIssues").Exists())
        {
            return;
        }

        Create.Table("DeliveryIssues")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RouteStopId").AsInt32().NotNullable().ForeignKey("DeliveryRouteStops", "Id")
            .WithColumn("DriverId").AsInt32().NotNullable().ForeignKey("Drivers", "Id")
            .WithColumn("Reason").AsString(100).NotNullable()
            .WithColumn("Notes").AsString(500).Nullable()
            .WithColumn("ReportedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();

        Create.Index("IX_DeliveryIssues_RouteStop_ReportedAt")
            .OnTable("DeliveryIssues")
            .OnColumn("RouteStopId").Ascending()
            .OnColumn("ReportedAt").Descending();
    }

    public override void Down()
    {
        if (Schema.Table("DeliveryIssues").Exists())
        {
            Delete.Table("DeliveryIssues");
        }
    }
}
