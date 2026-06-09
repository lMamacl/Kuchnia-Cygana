using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(8)]
public sealed class CreatePackingManifests : Migration
{
    public override void Up()
    {
        if (Schema.Table("PackingManifests").Exists())
        {
            return;
        }

        Create.Table("PackingManifests")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("PackingDate").AsDate().NotNullable()
            .WithColumn("ManifestNumber").AsString(50).NotNullable()
            .WithColumn("RouteCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("BagCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("GeneratedAt").AsDateTime().NotNullable()
            .WithColumn("GeneratedBy").AsString(100).Nullable()
            .WithColumn("PayloadJson").AsCustom("NVARCHAR(MAX)").NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        Create.Index("IX_PackingManifests_PackingDate")
            .OnTable("PackingManifests")
            .OnColumn("PackingDate")
            .Descending();
    }

    public override void Down()
    {
        if (Schema.Table("PackingManifests").Exists())
        {
            Delete.Table("PackingManifests");
        }
    }
}
