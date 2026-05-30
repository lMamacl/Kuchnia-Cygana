using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(304)]
public sealed class CreateDietMenuPlans : Migration
{
    public override void Up()
    {
        Alter.Table("Ingredients")
            .AddColumn("StockItemId").AsInt32().Nullable()
            .AddColumn("WarehouseCategoryId").AsInt32().Nullable()
            .AddColumn("YieldFactor").AsDecimal(7, 4).NotNullable().WithDefaultValue(1.0m)
            .AddColumn("RequiresCoreTemperatureCheck").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("MinimumCoreTemperatureCelsius").AsDecimal(5, 2).Nullable();

        Create.ForeignKey("FK_Ingredients_StockItems")
            .FromTable("Ingredients").ForeignColumn("StockItemId")
            .ToTable("StockItems").PrimaryColumn("Id");

        Create.ForeignKey("FK_Ingredients_WarehouseCategories")
            .FromTable("Ingredients").ForeignColumn("WarehouseCategoryId")
            .ToTable("WarehouseCategories").PrimaryColumn("Id");

        Execute.Sql(
            """
            UPDATE i
            SET
                i.StockItemId = si.Id,
                i.WarehouseCategoryId = si.WarehouseCategoryId
            FROM [Ingredients] i
            INNER JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id]
            WHERE i.[StockItemId] IS NULL;
            """);

        Create.Index("IX_Ingredients_StockItemId")
            .OnTable("Ingredients")
            .OnColumn("StockItemId");

        Create.Index("IX_Ingredients_WarehouseCategoryId")
            .OnTable("Ingredients")
            .OnColumn("WarehouseCategoryId");

        Alter.Table("Meals")
            .AddColumn("PreparationInstructions").AsCustom("nvarchar(max)").Nullable()
            .AddColumn("RawWeightGrams").AsDecimal(10, 2).Nullable()
            .AddColumn("CookedWeightGrams").AsDecimal(10, 2).Nullable()
            .AddColumn("RequiresCoreTemperatureCheck").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("MinimumCoreTemperatureCelsius").AsDecimal(5, 2).Nullable();

        Execute.Sql(
            """
            UPDATE [Meals]
            SET [Status] = N'Published'
            WHERE [Status] = N'Active';

            UPDATE [Meals]
            SET
                [PreparationInstructions] = COALESCE([PreparationInstructions], [Description]),
                [RawWeightGrams] = COALESCE([RawWeightGrams], 400),
                [CookedWeightGrams] = COALESCE([CookedWeightGrams], 350),
                [RequiresCoreTemperatureCheck] = CASE
                    WHEN LOWER([Name]) LIKE N'%kurczak%'
                      OR LOWER([Name]) LIKE N'%losos%'
                      OR LOWER([Name]) LIKE N'%loso%'
                      OR LOWER([Name]) LIKE N'%wolow%'
                      OR LOWER([Name]) LIKE N'%wołow%'
                      OR LOWER([Name]) LIKE N'%ryb%'
                    THEN 1 ELSE [RequiresCoreTemperatureCheck] END,
                [MinimumCoreTemperatureCelsius] = CASE
                    WHEN [MinimumCoreTemperatureCelsius] IS NOT NULL THEN [MinimumCoreTemperatureCelsius]
                    WHEN LOWER([Name]) LIKE N'%kurczak%' THEN 75
                    WHEN LOWER([Name]) LIKE N'%losos%' OR LOWER([Name]) LIKE N'%loso%' OR LOWER([Name]) LIKE N'%ryb%' THEN 63
                    WHEN LOWER([Name]) LIKE N'%wolow%' OR LOWER([Name]) LIKE N'%wołow%' THEN 70
                    ELSE NULL END
            WHERE [PreparationInstructions] IS NULL
               OR [RawWeightGrams] IS NULL
               OR [CookedWeightGrams] IS NULL
               OR [MinimumCoreTemperatureCelsius] IS NULL;
            """);

        Create.Table("DietMenuPlans")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("PlanDate").AsDate().NotNullable()
            .WithColumn("Status").AsString(30).NotNullable().WithDefaultValue("Draft")
            .WithColumn("Notes").AsString(500).Nullable()
            .WithColumn("PublishedAt").AsDateTimeOffset().Nullable()
            .WithColumn("PublishedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.Index("UX_DietMenuPlans_PlanDate")
            .OnTable("DietMenuPlans")
            .OnColumn("PlanDate").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_DietMenuPlans_Status_PlanDate")
            .OnTable("DietMenuPlans")
            .OnColumn("Status").Ascending()
            .OnColumn("PlanDate").Ascending();

        Create.Table("DietMenuPlanItems")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("DietMenuPlanId").AsInt32().NotNullable()
            .WithColumn("DietVariantId").AsInt32().NotNullable()
            .WithColumn("MealId").AsInt32().NotNullable()
            .WithColumn("MealSlot").AsString(40).NotNullable()
            .WithColumn("ServingSizeMultiplier").AsDecimal(5, 2).NotNullable().WithDefaultValue(1.0m)
            .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_DietMenuPlanItems_DietMenuPlans")
            .FromTable("DietMenuPlanItems").ForeignColumn("DietMenuPlanId")
            .ToTable("DietMenuPlans").PrimaryColumn("Id");

        Create.ForeignKey("FK_DietMenuPlanItems_DietVariants")
            .FromTable("DietMenuPlanItems").ForeignColumn("DietVariantId")
            .ToTable("DietVariants").PrimaryColumn("Id");

        Create.ForeignKey("FK_DietMenuPlanItems_Meals")
            .FromTable("DietMenuPlanItems").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.Index("IX_DietMenuPlanItems_Plan_Variant_Sort")
            .OnTable("DietMenuPlanItems")
            .OnColumn("DietMenuPlanId").Ascending()
            .OnColumn("DietVariantId").Ascending()
            .OnColumn("SortOrder").Ascending();

        Create.Index("IX_DietMenuPlanItems_MealId")
            .OnTable("DietMenuPlanItems")
            .OnColumn("MealId");

        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM [DietVariantMeals])
               AND NOT EXISTS (SELECT 1 FROM [DietMenuPlans])
            BEGIN
                DECLARE @offset int = 0;
                DECLARE @planId int;
                DECLARE @planDate date;

                WHILE @offset < 7
                BEGIN
                    SET @planDate = DATEADD(day, @offset, CONVERT(date, SYSUTCDATETIME()));

                    INSERT INTO [DietMenuPlans]
                        ([PlanDate], [Status], [Notes], [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
                    VALUES
                        (@planDate, N'Published', N'Backfill z DietVariantMeals', SYSDATETIMEOFFSET(), N'Migration304', SYSDATETIMEOFFSET(), N'Migration304', 0);

                    SET @planId = CAST(SCOPE_IDENTITY() AS int);

                    INSERT INTO [DietMenuPlanItems]
                        ([DietMenuPlanId], [DietVariantId], [MealId], [MealSlot], [ServingSizeMultiplier], [SortOrder], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
                    SELECT
                        @planId,
                        dvm.[DietVariantId],
                        dvm.[MealId],
                        CASE dvm.[SortOrder]
                            WHEN 1 THEN N'Breakfast'
                            WHEN 2 THEN N'Snack1'
                            WHEN 3 THEN N'Lunch'
                            WHEN 4 THEN N'Snack2'
                            WHEN 5 THEN N'Dinner'
                            ELSE CONCAT(N'Meal', dvm.[SortOrder])
                        END,
                        dvm.[ServingSizeMultiplier],
                        dvm.[SortOrder],
                        1,
                        SYSDATETIMEOFFSET(),
                        N'Migration304',
                        0
                    FROM [DietVariantMeals] dvm;

                    SET @offset = @offset + 1;
                END
            END
            """);
    }

    public override void Down()
    {
        Delete.Index("IX_DietMenuPlanItems_MealId").OnTable("DietMenuPlanItems");
        Delete.Index("IX_DietMenuPlanItems_Plan_Variant_Sort").OnTable("DietMenuPlanItems");
        Delete.ForeignKey("FK_DietMenuPlanItems_Meals").OnTable("DietMenuPlanItems");
        Delete.ForeignKey("FK_DietMenuPlanItems_DietVariants").OnTable("DietMenuPlanItems");
        Delete.ForeignKey("FK_DietMenuPlanItems_DietMenuPlans").OnTable("DietMenuPlanItems");
        Delete.Table("DietMenuPlanItems");

        Delete.Index("IX_DietMenuPlans_Status_PlanDate").OnTable("DietMenuPlans");
        Delete.Index("UX_DietMenuPlans_PlanDate").OnTable("DietMenuPlans");
        Delete.Table("DietMenuPlans");

        Delete.Column("MinimumCoreTemperatureCelsius").FromTable("Meals");
        Delete.Column("RequiresCoreTemperatureCheck").FromTable("Meals");
        Delete.Column("CookedWeightGrams").FromTable("Meals");
        Delete.Column("RawWeightGrams").FromTable("Meals");
        Delete.Column("PreparationInstructions").FromTable("Meals");

        Delete.Index("IX_Ingredients_WarehouseCategoryId").OnTable("Ingredients");
        Delete.Index("IX_Ingredients_StockItemId").OnTable("Ingredients");
        Delete.ForeignKey("FK_Ingredients_WarehouseCategories").OnTable("Ingredients");
        Delete.ForeignKey("FK_Ingredients_StockItems").OnTable("Ingredients");
        Delete.Column("MinimumCoreTemperatureCelsius").FromTable("Ingredients");
        Delete.Column("RequiresCoreTemperatureCheck").FromTable("Ingredients");
        Delete.Column("YieldFactor").FromTable("Ingredients");
        Delete.Column("WarehouseCategoryId").FromTable("Ingredients");
        Delete.Column("StockItemId").FromTable("Ingredients");
    }
}
