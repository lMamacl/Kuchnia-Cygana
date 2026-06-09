using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(22)]
public sealed class AddPackingIssueAndBagDamageAudit : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingItems").Column("ReplacementForPackingItemId").Exists())
        {
            Alter.Table("PackingItems")
                .AddColumn("ReplacementForPackingItemId").AsInt32().Nullable()
                .AddColumn("IssueReportedAt").AsDateTimeOffset().Nullable()
                .AddColumn("IssueReportedByUserId").AsInt32().Nullable();

            Create.Index("IX_PackingItems_ReplacementForPackingItemId")
                .OnTable("PackingItems")
                .OnColumn("ReplacementForPackingItemId").Ascending();
        }

        if (!Schema.Table("PackingBags").Column("DamageReason").Exists())
        {
            Alter.Table("PackingBags")
                .AddColumn("DamageReason").AsString(500).Nullable()
                .AddColumn("DamagedAt").AsDateTimeOffset().Nullable()
                .AddColumn("DamagedByUserId").AsInt32().Nullable()
                .AddColumn("ReplacementPackingBagId").AsInt32().Nullable();

            Create.Index("IX_PackingBags_ReplacementPackingBagId")
                .OnTable("PackingBags")
                .OnColumn("ReplacementPackingBagId").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingBags").Column("DamageReason").Exists())
        {
            if (Schema.Table("PackingBags").Index("IX_PackingBags_ReplacementPackingBagId").Exists())
            {
                Delete.Index("IX_PackingBags_ReplacementPackingBagId").OnTable("PackingBags");
            }

            Delete.Column("ReplacementPackingBagId").FromTable("PackingBags");
            Delete.Column("DamagedByUserId").FromTable("PackingBags");
            Delete.Column("DamagedAt").FromTable("PackingBags");
            Delete.Column("DamageReason").FromTable("PackingBags");
        }

        if (Schema.Table("PackingItems").Column("ReplacementForPackingItemId").Exists())
        {
            if (Schema.Table("PackingItems").Index("IX_PackingItems_ReplacementForPackingItemId").Exists())
            {
                Delete.Index("IX_PackingItems_ReplacementForPackingItemId").OnTable("PackingItems");
            }

            Delete.Column("IssueReportedByUserId").FromTable("PackingItems");
            Delete.Column("IssueReportedAt").FromTable("PackingItems");
            Delete.Column("ReplacementForPackingItemId").FromTable("PackingItems");
        }
    }
}
