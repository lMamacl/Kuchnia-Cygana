using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

/// <summary>
/// Rozszerza pudelka pakowania o etap folii, statusu i operatora.
/// </summary>
[Migration(7)]
public sealed class ExtendPackingItemsForOrderBags : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingItems").Column("BoxCode").Exists())
        {
            Alter.Table("PackingItems")
                .AddColumn("BoxCode").AsString(100).Nullable();
        }

        if (!Schema.Table("PackingItems").Column("Status").Exists())
        {
            Alter.Table("PackingItems")
                .AddColumn("Status").AsInt32().NotNullable().WithDefaultValue(0);
        }

        if (!Schema.Table("PackingItems").Column("FoilPrintedAt").Exists())
        {
            Alter.Table("PackingItems")
                .AddColumn("FoilPrintedAt").AsDateTime().Nullable();
        }

        if (!Schema.Table("PackingItems").Column("PackedAt").Exists())
        {
            Alter.Table("PackingItems")
                .AddColumn("PackedAt").AsDateTime().Nullable();
        }

        if (!Schema.Table("PackingItems").Column("PackedBy").Exists())
        {
            Alter.Table("PackingItems")
                .AddColumn("PackedBy").AsString(50).Nullable();
        }

        if (!Schema.Table("PackingItems").Index("IX_PackingItems_BoxCode").Exists())
        {
            Create.Index("IX_PackingItems_BoxCode")
                .OnTable("PackingItems")
                .OnColumn("BoxCode")
                .Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingItems").Index("IX_PackingItems_BoxCode").Exists())
        {
            Delete.Index("IX_PackingItems_BoxCode").OnTable("PackingItems");
        }

        if (Schema.Table("PackingItems").Column("PackedBy").Exists())
        {
            Delete.Column("PackedBy").FromTable("PackingItems");
        }

        if (Schema.Table("PackingItems").Column("PackedAt").Exists())
        {
            Delete.Column("PackedAt").FromTable("PackingItems");
        }

        if (Schema.Table("PackingItems").Column("FoilPrintedAt").Exists())
        {
            Delete.Column("FoilPrintedAt").FromTable("PackingItems");
        }

        if (Schema.Table("PackingItems").Column("Status").Exists())
        {
            Delete.Column("Status").FromTable("PackingItems");
        }

        if (Schema.Table("PackingItems").Column("BoxCode").Exists())
        {
            Delete.Column("BoxCode").FromTable("PackingItems");
        }
    }
}
