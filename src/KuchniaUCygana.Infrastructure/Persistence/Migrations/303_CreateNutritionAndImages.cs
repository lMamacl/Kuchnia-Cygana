using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(303)]
public sealed class CreateNutritionAndImages : Migration
{
    public override void Up()
    {
        Create.Table("NutritionFacts")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MealId").AsInt32().Nullable()
            .WithColumn("IngredientId").AsInt32().Nullable()
            .WithColumn("CaloriesPer100g").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("ProteinPer100g").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("CarbohydratesPer100g").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("FatPer100g").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("FiberPer100g").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.ForeignKey("FK_NutritionFacts_Meals")
            .FromTable("NutritionFacts").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.ForeignKey("FK_NutritionFacts_Ingredients")
            .FromTable("NutritionFacts").ForeignColumn("IngredientId")
            .ToTable("Ingredients").PrimaryColumn("Id");

        Create.Index("IX_NutritionFacts_MealId")
            .OnTable("NutritionFacts")
            .OnColumn("MealId");

        Create.Index("IX_NutritionFacts_IngredientId")
            .OnTable("NutritionFacts")
            .OnColumn("IngredientId");

        Create.Table("MealImages")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MealId").AsInt32().NotNullable()
            .WithColumn("Url").AsString(1000).NotNullable()
            .WithColumn("FileName").AsString(500).NotNullable()
            .WithColumn("IsMain").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("FileSizeBytes").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.ForeignKey("FK_MealImages_Meals")
            .FromTable("MealImages").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.Index("IX_MealImages_MealId").OnTable("MealImages").OnColumn("MealId");
    }

    public override void Down()
    {
        Delete.Table("MealImages");
        Delete.Table("NutritionFacts");
    }
}
