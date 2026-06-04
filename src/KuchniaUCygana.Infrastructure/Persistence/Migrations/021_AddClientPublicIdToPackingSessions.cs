using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(21)]
public sealed class AddClientPublicIdToPackingSessions : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingSessions").Column("ClientPublicId").Exists())
        {
            Alter.Table("PackingSessions")
                .AddColumn("ClientPublicId").AsString(50).Nullable();
        }

        if (Schema.Table("PackingLabels").Column("QrCode").Exists())
        {
            if (Schema.Table("PackingLabels").Index("IX_PackingLabels_QrCode").Exists())
            {
                Delete.Index("IX_PackingLabels_QrCode").OnTable("PackingLabels");
            }

            Alter.Column("QrCode")
                .OnTable("PackingLabels")
                .AsString(300)
                .NotNullable();

            Create.Index("IX_PackingLabels_QrCode")
                .OnTable("PackingLabels")
                .OnColumn("QrCode").Unique();
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingLabels").Column("QrCode").Exists())
        {
            if (Schema.Table("PackingLabels").Index("IX_PackingLabels_QrCode").Exists())
            {
                Delete.Index("IX_PackingLabels_QrCode").OnTable("PackingLabels");
            }

            Alter.Column("QrCode")
                .OnTable("PackingLabels")
                .AsString(100)
                .NotNullable();

            Create.Index("IX_PackingLabels_QrCode")
                .OnTable("PackingLabels")
                .OnColumn("QrCode").Unique();
        }

        if (Schema.Table("PackingSessions").Column("ClientPublicId").Exists())
        {
            Delete.Column("ClientPublicId").FromTable("PackingSessions");
        }
    }
}
