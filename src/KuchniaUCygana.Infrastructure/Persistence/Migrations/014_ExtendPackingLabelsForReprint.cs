using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

/// <summary>
/// Rozszerza PackingLabels o pola:
/// - MealsList: lista posiłków w torbie (denormalizacja do wydruku, max 2000 znaków)
/// - ReprintReason: powód ponownego druku etykiety (wymagany przy redruku)
/// </summary>
[Migration(14)]
public sealed class ExtendPackingLabelsForReprint : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingLabels").Column("MealsList").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("MealsList").AsString(2000).Nullable();
        }

        if (!Schema.Table("PackingLabels").Column("ReprintReason").Exists())
        {
            Alter.Table("PackingLabels")
                .AddColumn("ReprintReason").AsString(250).Nullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingLabels").Column("ReprintReason").Exists())
        {
            Delete.Column("ReprintReason").FromTable("PackingLabels");
        }

        if (Schema.Table("PackingLabels").Column("MealsList").Exists())
        {
            Delete.Column("MealsList").FromTable("PackingLabels");
        }
    }
}
