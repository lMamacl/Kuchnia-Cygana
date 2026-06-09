using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(306)]
public sealed class AddNutritionToRecipeComponentVersions : Migration
{
    public override void Up()
    {
        Alter.Table("RecipeComponentVersions")
            .AddColumn("CaloriesPer100g").AsDecimal(10, 2).Nullable()
            .AddColumn("ProteinPer100g").AsDecimal(10, 2).Nullable()
            .AddColumn("CarbohydratesPer100g").AsDecimal(10, 2).Nullable()
            .AddColumn("FatPer100g").AsDecimal(10, 2).Nullable()
            .AddColumn("FiberPer100g").AsDecimal(10, 2).Nullable();
    }

    public override void Down()
    {
        Delete.Column("FiberPer100g").FromTable("RecipeComponentVersions");
        Delete.Column("FatPer100g").FromTable("RecipeComponentVersions");
        Delete.Column("CarbohydratesPer100g").FromTable("RecipeComponentVersions");
        Delete.Column("ProteinPer100g").FromTable("RecipeComponentVersions");
        Delete.Column("CaloriesPer100g").FromTable("RecipeComponentVersions");
    }
}
