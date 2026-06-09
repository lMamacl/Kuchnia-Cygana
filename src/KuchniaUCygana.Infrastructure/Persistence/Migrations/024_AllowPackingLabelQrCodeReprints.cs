using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(24)]
public sealed class AllowPackingLabelQrCodeReprints : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingLabels").Exists() ||
            !Schema.Table("PackingLabels").Column("QrCode").Exists())
        {
            return;
        }

        if (Schema.Table("PackingLabels").Index("IX_PackingLabels_QrCode").Exists())
        {
            Delete.Index("IX_PackingLabels_QrCode").OnTable("PackingLabels");
        }

        Create.Index("IX_PackingLabels_QrCode")
            .OnTable("PackingLabels")
            .OnColumn("QrCode").Ascending();
    }

    public override void Down()
    {
        if (!Schema.Table("PackingLabels").Exists() ||
            !Schema.Table("PackingLabels").Column("QrCode").Exists())
        {
            return;
        }

        if (Schema.Table("PackingLabels").Index("IX_PackingLabels_QrCode").Exists())
        {
            Delete.Index("IX_PackingLabels_QrCode").OnTable("PackingLabels");
        }

        Create.Index("IX_PackingLabels_QrCode")
            .OnTable("PackingLabels")
            .OnColumn("QrCode").Unique();
    }
}
