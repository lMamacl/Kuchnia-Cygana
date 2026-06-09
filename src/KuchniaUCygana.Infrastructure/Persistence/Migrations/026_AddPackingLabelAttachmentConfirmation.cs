using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(26)]
public sealed class AddPackingLabelAttachmentConfirmation : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingLabels").Column("AttachedAt").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("AttachedAt").AsDateTimeOffset().Nullable();
        }

        if (!Schema.Table("PackingLabels").Column("AttachedByUserId").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("AttachedByUserId").AsInt32().Nullable();
        }

        if (!Schema.Table("PackingLabels").Column("AttachedBy").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("AttachedBy").AsString(100).Nullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingLabels").Column("AttachedBy").Exists())
        {
            Delete.Column("AttachedBy").FromTable("PackingLabels");
        }

        if (Schema.Table("PackingLabels").Column("AttachedByUserId").Exists())
        {
            Delete.Column("AttachedByUserId").FromTable("PackingLabels");
        }

        if (Schema.Table("PackingLabels").Column("AttachedAt").Exists())
        {
            Delete.Column("AttachedAt").FromTable("PackingLabels");
        }
    }
}
