using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(302)]
public sealed class CreateRelationshipTables : Migration
{
    public override void Up()
    {
        Create.Table("IngredientAllergens")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("IngredientId").AsInt32().NotNullable()
            .WithColumn("AllergenId").AsInt32().NotNullable()
            .WithColumn("TraceAmount").AsBoolean().NotNullable().WithDefaultValue(false);

        Create.ForeignKey("FK_IngredientAllergens_Ingredients")
            .FromTable("IngredientAllergens").ForeignColumn("IngredientId")
            .ToTable("Ingredients").PrimaryColumn("Id");

        Create.ForeignKey("FK_IngredientAllergens_Allergens")
            .FromTable("IngredientAllergens").ForeignColumn("AllergenId")
            .ToTable("Allergens").PrimaryColumn("Id");

        Create.Index("IX_IngredientAllergens_Unique")
            .OnTable("IngredientAllergens")
            .OnColumn("IngredientId").Ascending()
            .OnColumn("AllergenId").Ascending()
            .WithOptions().Unique();

        Create.Table("Recipes")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MealId").AsInt32().NotNullable()
            .WithColumn("IngredientId").AsInt32().NotNullable()
            .WithColumn("WeightInGrams").AsDecimal(8, 2).NotNullable()
            .WithColumn("IsOptional").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Notes").AsString(500).Nullable();

        Create.ForeignKey("FK_Recipes_Meals")
            .FromTable("Recipes").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.ForeignKey("FK_Recipes_Ingredients")
            .FromTable("Recipes").ForeignColumn("IngredientId")
            .ToTable("Ingredients").PrimaryColumn("Id");

        Create.Index("IX_Recipes_MealId")
            .OnTable("Recipes")
            .OnColumn("MealId").Ascending();

        Create.Table("MealAllergens")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MealId").AsInt32().NotNullable()
            .WithColumn("AllergenId").AsInt32().NotNullable()
            .WithColumn("IsTrace").AsBoolean().NotNullable().WithDefaultValue(false);

        Create.ForeignKey("FK_MealAllergens_Meals")
            .FromTable("MealAllergens").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.ForeignKey("FK_MealAllergens_Allergens")
            .FromTable("MealAllergens").ForeignColumn("AllergenId")
            .ToTable("Allergens").PrimaryColumn("Id");

        Create.Index("IX_MealAllergens_MealId_AllergenId")
            .OnTable("MealAllergens")
            .OnColumn("MealId").Ascending()
            .OnColumn("AllergenId").Ascending()
            .WithOptions().Unique();

        Create.Table("DietVariantMeals")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("DietVariantId").AsInt32().NotNullable()
            .WithColumn("MealId").AsInt32().NotNullable()
            .WithColumn("ServingSizeMultiplier").AsDecimal(5, 2).NotNullable().WithDefaultValue(1.0)
            .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0);

        Create.ForeignKey("FK_DietVariantMeals_DietVariants")
            .FromTable("DietVariantMeals").ForeignColumn("DietVariantId")
            .ToTable("DietVariants").PrimaryColumn("Id");

        Create.ForeignKey("FK_DietVariantMeals_Meals")
            .FromTable("DietVariantMeals").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.Index("IX_DietVariantMeals_Variant_Meal")
            .OnTable("DietVariantMeals")
            .OnColumn("DietVariantId").Ascending()
            .OnColumn("MealId").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("DietVariantMeals");
        Delete.Table("MealAllergens");
        Delete.Table("Recipes");
        Delete.Table("IngredientAllergens");
    }
}
