using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(517)]
public sealed class AddMenuPlanningQueryIndexes : Migration
{
    public override void Up()
    {
        CreateIndexIfMissing(
            "IX_DietMenuPlanItems_Plan_Variant_Active_Sort",
            "DietMenuPlanItems",
            "DietMenuPlanId",
            "DietVariantId",
            "IsDeleted",
            "IsActive",
            "SortOrder");
        CreateIndexIfMissing(
            "IX_MealRecipeComponents_Meal_Deleted_Sort",
            "MealRecipeComponents",
            "MealId",
            "IsDeleted",
            "SortOrder",
            "RecipeComponentVersionId");
        CreateIndexIfMissing(
            "IX_MealVariantComponents_Variant_Deleted_Sort",
            "MealVariantComponents",
            "MealVariantId",
            "IsDeleted",
            "SortOrder",
            "RecipeComponentVersionId");
        CreateIndexIfMissing(
            "IX_RecipeComponentIngredients_Version_Deleted",
            "RecipeComponentIngredients",
            "RecipeComponentVersionId",
            "IsDeleted");
        CreateIndexIfMissing(
            "IX_PackagingRequirements_Meal_Deleted",
            "PackagingRequirements",
            "MealId",
            "IsDeleted");
        CreateIndexIfMissing(
            "IX_PackagingRequirements_ComponentVersion_Deleted",
            "PackagingRequirements",
            "RecipeComponentVersionId",
            "IsDeleted");
        CreateIndexIfMissing(
            "IX_PackagingRequirements_MealVariant_Deleted",
            "PackagingRequirements",
            "MealVariantId",
            "IsDeleted");
        CreateIndexIfMissing(
            "IX_Recipes_Meal_Deleted",
            "Recipes",
            "MealId",
            "IsDeleted");
    }

    public override void Down()
    {
        DeleteIndexIfExists("IX_Recipes_Meal_Deleted", "Recipes");
        DeleteIndexIfExists("IX_PackagingRequirements_MealVariant_Deleted", "PackagingRequirements");
        DeleteIndexIfExists("IX_PackagingRequirements_ComponentVersion_Deleted", "PackagingRequirements");
        DeleteIndexIfExists("IX_PackagingRequirements_Meal_Deleted", "PackagingRequirements");
        DeleteIndexIfExists("IX_RecipeComponentIngredients_Version_Deleted", "RecipeComponentIngredients");
        DeleteIndexIfExists("IX_MealVariantComponents_Variant_Deleted_Sort", "MealVariantComponents");
        DeleteIndexIfExists("IX_MealRecipeComponents_Meal_Deleted_Sort", "MealRecipeComponents");
        DeleteIndexIfExists("IX_DietMenuPlanItems_Plan_Variant_Active_Sort", "DietMenuPlanItems");
    }

    private void CreateIndexIfMissing(string indexName, string tableName, params string[] columns)
    {
        if (Schema.Table(tableName).Index(indexName).Exists())
        {
            return;
        }

        var index = Create.Index(indexName).OnTable(tableName);
        foreach (var column in columns)
        {
            index.OnColumn(column).Ascending();
        }
    }

    private void DeleteIndexIfExists(string indexName, string tableName)
    {
        if (Schema.Table(tableName).Index(indexName).Exists())
        {
            Delete.Index(indexName).OnTable(tableName);
        }
    }
}
