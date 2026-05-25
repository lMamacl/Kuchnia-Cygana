using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(9)]
public sealed class ExtendPackingManifestsForRouteDelivery : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingManifests").Column("RouteId").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("RouteId").AsInt32().Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("RouteName").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("RouteName").AsString(150).Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("VehicleId").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("VehicleId").AsInt32().Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("VehicleRegistration").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("VehicleRegistration").AsString(50).Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("IsVerified").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("IsVerified").AsBoolean().NotNullable().WithDefaultValue(false);
        }

        if (!Schema.Table("PackingManifests").Column("VerifiedAt").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("VerifiedAt").AsDateTime().Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("VerifiedBy").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("VerifiedBy").AsString(100).Nullable();
        }

        if (!Schema.Table("PackingManifests").Index("IX_PackingManifests_DateRoute").Exists())
        {
            Create.Index("IX_PackingManifests_DateRoute")
                .OnTable("PackingManifests")
                .OnColumn("PackingDate").Descending()
                .OnColumn("RouteId").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingManifests").Index("IX_PackingManifests_DateRoute").Exists())
        {
            Delete.Index("IX_PackingManifests_DateRoute").OnTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("VerifiedBy").Exists())
        {
            Delete.Column("VerifiedBy").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("VerifiedAt").Exists())
        {
            Delete.Column("VerifiedAt").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("IsVerified").Exists())
        {
            Delete.Column("IsVerified").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("VehicleRegistration").Exists())
        {
            Delete.Column("VehicleRegistration").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("VehicleId").Exists())
        {
            Delete.Column("VehicleId").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("RouteName").Exists())
        {
            Delete.Column("RouteName").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("RouteId").Exists())
        {
            Delete.Column("RouteId").FromTable("PackingManifests");
        }
    }
}
