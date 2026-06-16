using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(508)]
public sealed class AddM2CatalogArchitecture : Migration
{
    public override void Up()
    {
        Alter.Table("Recipes")
            .AddColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false);

        CreateOrReplaceMealNutritionCostFunctionWithRecipeSoftDelete();

        Alter.Table("RecipeComponents")
            .AddColumn("CategoryId").AsInt32().Nullable()
            .AddColumn("ImageUrl").AsString(500).Nullable()
            .AddColumn("PreparationTimeMinutes").AsInt32().NotNullable().WithDefaultValue(0);

        Create.ForeignKey("FK_RecipeComponents_Categories")
            .FromTable("RecipeComponents").ForeignColumn("CategoryId")
            .ToTable("Categories").PrimaryColumn("Id");

        Alter.Table("RecipeComponentVersions")
            .AddColumn("NutritionSource").AsString(40).NotNullable().WithDefaultValue("Manual")
            .AddColumn("NutritionOverrideReason").AsString(500).Nullable()
            .AddColumn("AllergensApproved").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("AllergenOverrideReason").AsString(500).Nullable()
            .AddColumn("AllergensApprovedAt").AsDateTimeOffset().Nullable()
            .AddColumn("AllergensApprovedBy").AsString(100).Nullable();

        Create.Table("RecipeComponentInstructionSections")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RecipeComponentVersionId").AsInt32().NotNullable()
            .WithColumn("Title").AsString(200).Nullable()
            .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_RecipeComponentInstructionSections_ComponentVersions")
            .FromTable("RecipeComponentInstructionSections").ForeignColumn("RecipeComponentVersionId")
            .ToTable("RecipeComponentVersions").PrimaryColumn("Id");

        Create.Index("IX_RecipeComponentInstructionSections_Version_Sort")
            .OnTable("RecipeComponentInstructionSections")
            .OnColumn("RecipeComponentVersionId").Ascending()
            .OnColumn("SortOrder").Ascending();

        Create.Table("RecipeComponentInstructionSteps")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RecipeComponentInstructionSectionId").AsInt32().NotNullable()
            .WithColumn("StepText").AsCustom("nvarchar(max)").NotNullable()
            .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("RequiresControl").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ControlType").AsString(80).Nullable()
            .WithColumn("ExpectedValue").AsDecimal(10, 3).Nullable()
            .WithColumn("ExpectedUnit").AsString(40).Nullable()
            .WithColumn("IsCritical").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_RecipeComponentInstructionSteps_Sections")
            .FromTable("RecipeComponentInstructionSteps").ForeignColumn("RecipeComponentInstructionSectionId")
            .ToTable("RecipeComponentInstructionSections").PrimaryColumn("Id");

        Create.Index("IX_RecipeComponentInstructionSteps_Section_Sort")
            .OnTable("RecipeComponentInstructionSteps")
            .OnColumn("RecipeComponentInstructionSectionId").Ascending()
            .OnColumn("SortOrder").Ascending();

        Alter.Table("Ingredients")
            .AddColumn("ResourceType").AsString(40).NotNullable().WithDefaultValue("Food")
            .AddColumn("FoodCategoryId").AsInt32().Nullable()
            .AddColumn("Description").AsString(2000).Nullable()
            .AddColumn("ImageUrl").AsString(500).Nullable()
            .AddColumn("ProductComposition").AsString(2000).Nullable()
            .AddColumn("WarehouseCategoryFefoApproved").AsBoolean().NotNullable().WithDefaultValue(false);

        Create.ForeignKey("FK_Ingredients_FoodCategories")
            .FromTable("Ingredients").ForeignColumn("FoodCategoryId")
            .ToTable("Categories").PrimaryColumn("Id");

        Create.Table("MealVariants")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MealId").AsInt32().NotNullable()
            .WithColumn("Name").AsString(160).NotNullable()
            .WithColumn("VariantType").AsString(60).NotNullable().WithDefaultValue("Standard")
            .WithColumn("Status").AsString(30).NotNullable().WithDefaultValue("Draft")
            .WithColumn("Description").AsString(1000).Nullable()
            .WithColumn("IsDefault").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("RawWeightGrams").AsDecimal(10, 2).Nullable()
            .WithColumn("CookedWeightGrams").AsDecimal(10, 2).Nullable()
            .WithColumn("CaloriesPer100g").AsDecimal(10, 2).Nullable()
            .WithColumn("ProteinPer100g").AsDecimal(10, 2).Nullable()
            .WithColumn("CarbohydratesPer100g").AsDecimal(10, 2).Nullable()
            .WithColumn("FatPer100g").AsDecimal(10, 2).Nullable()
            .WithColumn("FiberPer100g").AsDecimal(10, 2).Nullable()
            .WithColumn("NutritionSource").AsString(40).NotNullable().WithDefaultValue("Aggregated")
            .WithColumn("NutritionOverrideReason").AsString(500).Nullable()
            .WithColumn("AllergensApproved").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("AllergenOverrideReason").AsString(500).Nullable()
            .WithColumn("PublishedAt").AsDateTimeOffset().Nullable()
            .WithColumn("PublishedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_MealVariants_Meals")
            .FromTable("MealVariants").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.Index("IX_MealVariants_Meal_Status")
            .OnTable("MealVariants")
            .OnColumn("MealId").Ascending()
            .OnColumn("Status").Ascending()
            .OnColumn("IsDeleted").Ascending();

        Create.Index("IX_MealVariants_Default")
            .OnTable("MealVariants")
            .OnColumn("MealId").Ascending()
            .OnColumn("IsDefault").Ascending()
            .OnColumn("IsDeleted").Ascending();

        Create.Table("MealVariantComponents")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MealVariantId").AsInt32().NotNullable()
            .WithColumn("RecipeComponentVersionId").AsInt32().NotNullable()
            .WithColumn("Role").AsString(80).Nullable()
            .WithColumn("QuantityPerServing").AsDecimal(10, 3).NotNullable().WithDefaultValue(1.0m)
            .WithColumn("Unit").AsString(40).NotNullable().WithDefaultValue("portion")
            .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsOptional").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_MealVariantComponents_MealVariants")
            .FromTable("MealVariantComponents").ForeignColumn("MealVariantId")
            .ToTable("MealVariants").PrimaryColumn("Id");

        Create.ForeignKey("FK_MealVariantComponents_ComponentVersions")
            .FromTable("MealVariantComponents").ForeignColumn("RecipeComponentVersionId")
            .ToTable("RecipeComponentVersions").PrimaryColumn("Id");

        Create.Index("IX_MealVariantComponents_Variant_Sort")
            .OnTable("MealVariantComponents")
            .OnColumn("MealVariantId").Ascending()
            .OnColumn("SortOrder").Ascending();

        Create.Table("MealVariantAllergens")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MealVariantId").AsInt32().NotNullable()
            .WithColumn("AllergenId").AsInt32().NotNullable()
            .WithColumn("IsTrace").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("SourceType").AsString(40).NotNullable().WithDefaultValue("Aggregated");

        Create.ForeignKey("FK_MealVariantAllergens_MealVariants")
            .FromTable("MealVariantAllergens").ForeignColumn("MealVariantId")
            .ToTable("MealVariants").PrimaryColumn("Id");

        Create.ForeignKey("FK_MealVariantAllergens_Allergens")
            .FromTable("MealVariantAllergens").ForeignColumn("AllergenId")
            .ToTable("Allergens").PrimaryColumn("Id");

        Create.Index("IX_MealVariantAllergens_Unique")
            .OnTable("MealVariantAllergens")
            .OnColumn("MealVariantId").Ascending()
            .OnColumn("AllergenId").Ascending()
            .WithOptions().Unique();

        Alter.Table("DietMenuPlanItems")
            .AddColumn("MealVariantId").AsInt32().Nullable();

        Create.ForeignKey("FK_DietMenuPlanItems_MealVariants")
            .FromTable("DietMenuPlanItems").ForeignColumn("MealVariantId")
            .ToTable("MealVariants").PrimaryColumn("Id");

        Create.Index("IX_DietMenuPlanItems_MealVariantId")
            .OnTable("DietMenuPlanItems")
            .OnColumn("MealVariantId");

        Create.Table("CookingSessions")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RecipeComponentVersionId").AsInt32().NotNullable()
            .WithColumn("ProductionDate").AsDate().NotNullable()
            .WithColumn("ProductionPlanItemId").AsInt32().Nullable()
            .WithColumn("Status").AsString(30).NotNullable().WithDefaultValue("Draft")
            .WithColumn("StartedAt").AsDateTimeOffset().Nullable()
            .WithColumn("StartedBy").AsString(100).Nullable()
            .WithColumn("CompletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CompletedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_CookingSessions_ComponentVersions")
            .FromTable("CookingSessions").ForeignColumn("RecipeComponentVersionId")
            .ToTable("RecipeComponentVersions").PrimaryColumn("Id");

        Create.ForeignKey("FK_CookingSessions_ProductionPlanItems")
            .FromTable("CookingSessions").ForeignColumn("ProductionPlanItemId")
            .ToTable("ProductionPlanItems").PrimaryColumn("Id");

        Create.Index("IX_CookingSessions_Date_Component")
            .OnTable("CookingSessions")
            .OnColumn("ProductionDate").Ascending()
            .OnColumn("RecipeComponentVersionId").Ascending()
            .OnColumn("IsDeleted").Ascending();

        Create.Table("CookingSessionStepChecks")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CookingSessionId").AsInt32().NotNullable()
            .WithColumn("RecipeComponentInstructionStepId").AsInt32().NotNullable()
            .WithColumn("Status").AsString(30).NotNullable().WithDefaultValue("Pending")
            .WithColumn("CheckedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CheckedBy").AsString(100).Nullable()
            .WithColumn("ActualValue").AsDecimal(10, 3).Nullable()
            .WithColumn("ActualUnit").AsString(40).Nullable()
            .WithColumn("Notes").AsString(500).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_CookingSessionStepChecks_CookingSessions")
            .FromTable("CookingSessionStepChecks").ForeignColumn("CookingSessionId")
            .ToTable("CookingSessions").PrimaryColumn("Id");

        Create.ForeignKey("FK_CookingSessionStepChecks_InstructionSteps")
            .FromTable("CookingSessionStepChecks").ForeignColumn("RecipeComponentInstructionStepId")
            .ToTable("RecipeComponentInstructionSteps").PrimaryColumn("Id");

        Create.Index("IX_CookingSessionStepChecks_Session_Step")
            .OnTable("CookingSessionStepChecks")
            .OnColumn("CookingSessionId").Ascending()
            .OnColumn("RecipeComponentInstructionStepId").Ascending();

        CreateSearchIndexes();
        BackfillDefaultMealVariants();
    }

    public override void Down()
    {
        Delete.Index("IX_Meals_Category_Status_Name").OnTable("Meals");
        Delete.Index("IX_RecipeComponentVersions_Status_Component").OnTable("RecipeComponentVersions");
        Delete.Index("IX_RecipeComponents_Category_Active_Name").OnTable("RecipeComponents");
        Delete.Index("IX_Ingredients_Resource_Category_Name").OnTable("Ingredients");
        Delete.Index("IX_CookingSessionStepChecks_Session_Step").OnTable("CookingSessionStepChecks");
        Delete.Table("CookingSessionStepChecks");
        Delete.Index("IX_CookingSessions_Date_Component").OnTable("CookingSessions");
        Delete.Table("CookingSessions");
        Delete.Index("IX_DietMenuPlanItems_MealVariantId").OnTable("DietMenuPlanItems");
        Delete.ForeignKey("FK_DietMenuPlanItems_MealVariants").OnTable("DietMenuPlanItems");
        Delete.Column("MealVariantId").FromTable("DietMenuPlanItems");
        Delete.Index("IX_MealVariantAllergens_Unique").OnTable("MealVariantAllergens");
        Delete.Table("MealVariantAllergens");
        Delete.Index("IX_MealVariantComponents_Variant_Sort").OnTable("MealVariantComponents");
        Delete.Table("MealVariantComponents");
        Delete.Index("IX_MealVariants_Default").OnTable("MealVariants");
        Delete.Index("IX_MealVariants_Meal_Status").OnTable("MealVariants");
        Delete.Table("MealVariants");
        Delete.ForeignKey("FK_Ingredients_FoodCategories").OnTable("Ingredients");
        Delete.Column("WarehouseCategoryFefoApproved").FromTable("Ingredients");
        Delete.Column("ProductComposition").FromTable("Ingredients");
        Delete.Column("ImageUrl").FromTable("Ingredients");
        Delete.Column("Description").FromTable("Ingredients");
        Delete.Column("FoodCategoryId").FromTable("Ingredients");
        Delete.Column("ResourceType").FromTable("Ingredients");
        Delete.Index("IX_RecipeComponentInstructionSteps_Section_Sort").OnTable("RecipeComponentInstructionSteps");
        Delete.Table("RecipeComponentInstructionSteps");
        Delete.Index("IX_RecipeComponentInstructionSections_Version_Sort").OnTable("RecipeComponentInstructionSections");
        Delete.Table("RecipeComponentInstructionSections");
        Delete.Column("AllergensApprovedBy").FromTable("RecipeComponentVersions");
        Delete.Column("AllergensApprovedAt").FromTable("RecipeComponentVersions");
        Delete.Column("AllergenOverrideReason").FromTable("RecipeComponentVersions");
        Delete.Column("AllergensApproved").FromTable("RecipeComponentVersions");
        Delete.Column("NutritionOverrideReason").FromTable("RecipeComponentVersions");
        Delete.Column("NutritionSource").FromTable("RecipeComponentVersions");
        Delete.ForeignKey("FK_RecipeComponents_Categories").OnTable("RecipeComponents");
        Delete.Column("PreparationTimeMinutes").FromTable("RecipeComponents");
        Delete.Column("ImageUrl").FromTable("RecipeComponents");
        Delete.Column("CategoryId").FromTable("RecipeComponents");
        CreateOrReplaceMealNutritionCostFunctionWithoutRecipeSoftDelete();
        Delete.Column("IsDeleted").FromTable("Recipes");
    }

    private void CreateOrReplaceMealNutritionCostFunctionWithRecipeSoftDelete()
    {
        Execute.Sql(
            """
            CREATE OR ALTER FUNCTION [dbo].[fn_MealNutritionCost] (@MealId int)
            RETURNS TABLE
            AS
            RETURN
            (
                SELECT
                    @MealId AS [MealId],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * i.[CostPerUnit]), 0) AS decimal(18, 4)) AS [EstimatedCost],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[CaloriesPer100g]), 0) AS decimal(18, 2)) AS [Calories],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[ProteinPer100g]), 0) AS decimal(18, 2)) AS [Protein],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[CarbohydratesPer100g]), 0) AS decimal(18, 2)) AS [Carbohydrates],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[FatPer100g]), 0) AS decimal(18, 2)) AS [Fat],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[FiberPer100g]), 0) AS decimal(18, 2)) AS [Fiber]
                FROM [dbo].[Recipes] r
                INNER JOIN [dbo].[Ingredients] i ON i.[Id] = r.[IngredientId]
                LEFT JOIN [dbo].[NutritionFacts] nf ON nf.[IngredientId] = r.[IngredientId]
                WHERE r.[MealId] = @MealId
                  AND r.[IsDeleted] = 0
                  AND i.[IsDeleted] = 0
                  AND i.[IsActive] = 1
            );
            """);
    }

    private void CreateOrReplaceMealNutritionCostFunctionWithoutRecipeSoftDelete()
    {
        Execute.Sql(
            """
            CREATE OR ALTER FUNCTION [dbo].[fn_MealNutritionCost] (@MealId int)
            RETURNS TABLE
            AS
            RETURN
            (
                SELECT
                    @MealId AS [MealId],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * i.[CostPerUnit]), 0) AS decimal(18, 4)) AS [EstimatedCost],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[CaloriesPer100g]), 0) AS decimal(18, 2)) AS [Calories],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[ProteinPer100g]), 0) AS decimal(18, 2)) AS [Protein],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[CarbohydratesPer100g]), 0) AS decimal(18, 2)) AS [Carbohydrates],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[FatPer100g]), 0) AS decimal(18, 2)) AS [Fat],
                    CAST(COALESCE(SUM((r.[WeightInGrams] / 100.0) * nf.[FiberPer100g]), 0) AS decimal(18, 2)) AS [Fiber]
                FROM [dbo].[Recipes] r
                INNER JOIN [dbo].[Ingredients] i ON i.[Id] = r.[IngredientId]
                LEFT JOIN [dbo].[NutritionFacts] nf ON nf.[IngredientId] = r.[IngredientId]
                WHERE r.[MealId] = @MealId
                  AND i.[IsDeleted] = 0
                  AND i.[IsActive] = 1
            );
            """);
    }

    private void CreateSearchIndexes()
    {
        Create.Index("IX_Ingredients_Resource_Category_Name")
            .OnTable("Ingredients")
            .OnColumn("ResourceType").Ascending()
            .OnColumn("FoodCategoryId").Ascending()
            .OnColumn("IsActive").Ascending()
            .OnColumn("Name").Ascending();

        Create.Index("IX_RecipeComponents_Category_Active_Name")
            .OnTable("RecipeComponents")
            .OnColumn("CategoryId").Ascending()
            .OnColumn("IsActive").Ascending()
            .OnColumn("Name").Ascending();

        Create.Index("IX_RecipeComponentVersions_Status_Component")
            .OnTable("RecipeComponentVersions")
            .OnColumn("Status").Ascending()
            .OnColumn("RecipeComponentId").Ascending()
            .OnColumn("VersionNumber").Ascending();

        Create.Index("IX_Meals_Category_Status_Name")
            .OnTable("Meals")
            .OnColumn("CategoryId").Ascending()
            .OnColumn("Status").Ascending()
            .OnColumn("IsActive").Ascending()
            .OnColumn("Name").Ascending();
    }

    private void BackfillDefaultMealVariants()
    {
        Execute.Sql(
            """
            INSERT INTO [MealVariants]
                ([MealId], [Name], [VariantType], [Status], [Description], [IsDefault],
                 [RawWeightGrams], [CookedWeightGrams], [CaloriesPer100g], [ProteinPer100g],
                 [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [NutritionSource],
                 [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                m.[Id],
                N'Standard',
                N'Standard',
                CASE WHEN m.[Status] IN (N'Published', N'Active') THEN N'Published' ELSE N'Draft' END,
                N'Wariant domyslny utworzony z istniejacego posilku.',
                1,
                m.[RawWeightGrams],
                m.[CookedWeightGrams],
                nf.[CaloriesPer100g],
                nf.[ProteinPer100g],
                nf.[CarbohydratesPer100g],
                nf.[FatPer100g],
                nf.[FiberPer100g],
                CASE WHEN nf.[MealId] IS NULL THEN N'Manual' ELSE N'Aggregated' END,
                CASE WHEN m.[Status] IN (N'Published', N'Active') THEN SYSDATETIMEOFFSET() ELSE NULL END,
                CASE WHEN m.[Status] IN (N'Published', N'Active') THEN N'Migration508' ELSE NULL END,
                SYSDATETIMEOFFSET(),
                N'Migration508',
                0
            FROM [Meals] m
            LEFT JOIN [NutritionFacts] nf ON nf.[MealId] = m.[Id]
            WHERE m.[IsDeleted] = 0
              AND NOT EXISTS (
                    SELECT 1
                    FROM [MealVariants] existing
                    WHERE existing.[MealId] = m.[Id]
                      AND existing.[IsDeleted] = 0
              );

            INSERT INTO [MealVariantComponents]
                ([MealVariantId], [RecipeComponentVersionId], [Role], [QuantityPerServing], [Unit],
                 [SortOrder], [IsOptional], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                mv.[Id],
                mrc.[RecipeComponentVersionId],
                mrc.[Role],
                mrc.[QuantityPerServing],
                mrc.[Unit],
                mrc.[SortOrder],
                mrc.[IsOptional],
                SYSDATETIMEOFFSET(),
                N'Migration508',
                0
            FROM [MealVariants] mv
            INNER JOIN [MealRecipeComponents] mrc ON mrc.[MealId] = mv.[MealId]
            WHERE mv.[IsDefault] = 1
              AND mv.[IsDeleted] = 0
              AND mrc.[IsDeleted] = 0
              AND NOT EXISTS (
                    SELECT 1
                    FROM [MealVariantComponents] existing
                    WHERE existing.[MealVariantId] = mv.[Id]
                      AND existing.[RecipeComponentVersionId] = mrc.[RecipeComponentVersionId]
                      AND existing.[IsDeleted] = 0
              );

            INSERT INTO [MealVariantAllergens] ([MealVariantId], [AllergenId], [IsTrace], [SourceType])
            SELECT DISTINCT
                mv.[Id],
                allergens.[AllergenId],
                allergens.[IsTrace],
                N'Aggregated'
            FROM [MealVariants] mv
            INNER JOIN (
                SELECT ma.[MealId], ma.[AllergenId], ma.[IsTrace]
                FROM [MealAllergens] ma
                UNION
                SELECT mrc.[MealId], ia.[AllergenId], CAST(ia.[TraceAmount] AS bit)
                FROM [MealRecipeComponents] mrc
                INNER JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = mrc.[RecipeComponentVersionId]
                INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = rci.[IngredientId]
                WHERE mrc.[IsDeleted] = 0
                  AND rci.[IsDeleted] = 0
            ) allergens ON allergens.[MealId] = mv.[MealId]
            WHERE mv.[IsDefault] = 1
              AND mv.[IsDeleted] = 0;

            UPDATE i
            SET [MealVariantId] = mv.[Id]
            FROM [DietMenuPlanItems] i
            INNER JOIN [MealVariants] mv ON mv.[MealId] = i.[MealId]
                AND mv.[IsDefault] = 1
                AND mv.[IsDeleted] = 0
            WHERE i.[MealVariantId] IS NULL;
            """);
    }
}
