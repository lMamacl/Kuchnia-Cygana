using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(305)]
public sealed class CreateVersionedRecipeComponents : Migration
{
    public override void Up()
    {
        Alter.Table("Meals")
            .AddColumn("ShelfLifeHours").AsInt32().Nullable()
            .AddColumn("UseEarliestIngredientExpiry").AsBoolean().NotNullable().WithDefaultValue(false);

        Create.Table("RecipeComponents")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.Index("IX_RecipeComponents_Name")
            .OnTable("RecipeComponents")
            .OnColumn("Name");

        Create.Table("RecipeComponentVersions")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RecipeComponentId").AsInt32().NotNullable()
            .WithColumn("VersionNumber").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("Status").AsString(30).NotNullable().WithDefaultValue("Draft")
            .WithColumn("Instructions").AsCustom("nvarchar(max)").Nullable()
            .WithColumn("YieldQuantity").AsDecimal(10, 3).NotNullable().WithDefaultValue(1.0m)
            .WithColumn("YieldUnit").AsString(40).NotNullable().WithDefaultValue("portion")
            .WithColumn("RawWeightGrams").AsDecimal(10, 2).Nullable()
            .WithColumn("CookedWeightGrams").AsDecimal(10, 2).Nullable()
            .WithColumn("ShelfLifeHours").AsInt32().Nullable()
            .WithColumn("UseEarliestIngredientExpiry").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ChangeSummary").AsString(500).Nullable()
            .WithColumn("IsTechnologyChange").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("NonTechnologyChangeReason").AsString(500).Nullable()
            .WithColumn("PublishedAt").AsDateTimeOffset().Nullable()
            .WithColumn("PublishedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_RecipeComponentVersions_RecipeComponents")
            .FromTable("RecipeComponentVersions").ForeignColumn("RecipeComponentId")
            .ToTable("RecipeComponents").PrimaryColumn("Id");

        Create.Index("UX_RecipeComponentVersions_Component_Version")
            .OnTable("RecipeComponentVersions")
            .OnColumn("RecipeComponentId").Ascending()
            .OnColumn("VersionNumber").Ascending()
            .WithOptions().Unique();

        Create.Table("RecipeComponentIngredients")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RecipeComponentVersionId").AsInt32().NotNullable()
            .WithColumn("IngredientId").AsInt32().NotNullable()
            .WithColumn("StockItemId").AsInt32().Nullable()
            .WithColumn("WarehouseCategoryId").AsInt32().Nullable()
            .WithColumn("WeightInGrams").AsDecimal(10, 2).NotNullable()
            .WithColumn("YieldFactor").AsDecimal(7, 4).NotNullable().WithDefaultValue(1.0m)
            .WithColumn("IsOptional").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Notes").AsString(500).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_RecipeComponentIngredients_ComponentVersions")
            .FromTable("RecipeComponentIngredients").ForeignColumn("RecipeComponentVersionId")
            .ToTable("RecipeComponentVersions").PrimaryColumn("Id");

        Create.ForeignKey("FK_RecipeComponentIngredients_Ingredients")
            .FromTable("RecipeComponentIngredients").ForeignColumn("IngredientId")
            .ToTable("Ingredients").PrimaryColumn("Id");

        Create.ForeignKey("FK_RecipeComponentIngredients_StockItems")
            .FromTable("RecipeComponentIngredients").ForeignColumn("StockItemId")
            .ToTable("StockItems").PrimaryColumn("Id");

        Create.ForeignKey("FK_RecipeComponentIngredients_WarehouseCategories")
            .FromTable("RecipeComponentIngredients").ForeignColumn("WarehouseCategoryId")
            .ToTable("WarehouseCategories").PrimaryColumn("Id");

        Create.Index("IX_RecipeComponentIngredients_Version")
            .OnTable("RecipeComponentIngredients")
            .OnColumn("RecipeComponentVersionId");

        Create.Table("MealRecipeComponents")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MealId").AsInt32().NotNullable()
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

        Create.ForeignKey("FK_MealRecipeComponents_Meals")
            .FromTable("MealRecipeComponents").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.ForeignKey("FK_MealRecipeComponents_ComponentVersions")
            .FromTable("MealRecipeComponents").ForeignColumn("RecipeComponentVersionId")
            .ToTable("RecipeComponentVersions").PrimaryColumn("Id");

        Create.Index("IX_MealRecipeComponents_Meal")
            .OnTable("MealRecipeComponents")
            .OnColumn("MealId").Ascending()
            .OnColumn("SortOrder").Ascending();

        Create.Table("PackagingRequirements")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OwnerType").AsString(40).NotNullable()
            .WithColumn("MealId").AsInt32().Nullable()
            .WithColumn("RecipeComponentVersionId").AsInt32().Nullable()
            .WithColumn("StockItemId").AsInt32().Nullable()
            .WithColumn("WarehouseCategoryId").AsInt32().Nullable()
            .WithColumn("ResourceName").AsString(200).NotNullable()
            .WithColumn("Quantity").AsDecimal(10, 3).NotNullable().WithDefaultValue(1.0m)
            .WithColumn("Unit").AsString(20).NotNullable().WithDefaultValue("pcs")
            .WithColumn("ContainerRole").AsString(80).Nullable()
            .WithColumn("IsCustomerFacing").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_PackagingRequirements_Meals")
            .FromTable("PackagingRequirements").ForeignColumn("MealId")
            .ToTable("Meals").PrimaryColumn("Id");

        Create.ForeignKey("FK_PackagingRequirements_ComponentVersions")
            .FromTable("PackagingRequirements").ForeignColumn("RecipeComponentVersionId")
            .ToTable("RecipeComponentVersions").PrimaryColumn("Id");

        Create.ForeignKey("FK_PackagingRequirements_StockItems")
            .FromTable("PackagingRequirements").ForeignColumn("StockItemId")
            .ToTable("StockItems").PrimaryColumn("Id");

        Create.ForeignKey("FK_PackagingRequirements_WarehouseCategories")
            .FromTable("PackagingRequirements").ForeignColumn("WarehouseCategoryId")
            .ToTable("WarehouseCategories").PrimaryColumn("Id");

        Create.Index("IX_PackagingRequirements_Owner")
            .OnTable("PackagingRequirements")
            .OnColumn("OwnerType").Ascending()
            .OnColumn("MealId").Ascending()
            .OnColumn("RecipeComponentVersionId").Ascending();

        Create.Table("PlanChangeAlerts")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("PlanDate").AsDate().NotNullable()
            .WithColumn("DietMenuPlanId").AsInt32().Nullable()
            .WithColumn("DietMenuPlanItemId").AsInt32().Nullable()
            .WithColumn("MealId").AsInt32().Nullable()
            .WithColumn("RecipeComponentVersionId").AsInt32().Nullable()
            .WithColumn("AlertType").AsString(60).NotNullable()
            .WithColumn("Severity").AsString(30).NotNullable().WithDefaultValue("Info")
            .WithColumn("Message").AsString(1000).NotNullable()
            .WithColumn("Reason").AsString(500).Nullable()
            .WithColumn("RequiresAcknowledgement").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("AcknowledgedAt").AsDateTimeOffset().Nullable()
            .WithColumn("AcknowledgedBy").AsString(100).Nullable();

        Create.Index("IX_PlanChangeAlerts_Date_Ack")
            .OnTable("PlanChangeAlerts")
            .OnColumn("PlanDate").Ascending()
            .OnColumn("AcknowledgedAt").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_PlanChangeAlerts_Date_Ack").OnTable("PlanChangeAlerts");
        Delete.Table("PlanChangeAlerts");

        Delete.Index("IX_PackagingRequirements_Owner").OnTable("PackagingRequirements");
        Delete.Table("PackagingRequirements");

        Delete.Index("IX_MealRecipeComponents_Meal").OnTable("MealRecipeComponents");
        Delete.Table("MealRecipeComponents");

        Delete.Index("IX_RecipeComponentIngredients_Version").OnTable("RecipeComponentIngredients");
        Delete.Table("RecipeComponentIngredients");

        Delete.Index("UX_RecipeComponentVersions_Component_Version").OnTable("RecipeComponentVersions");
        Delete.Table("RecipeComponentVersions");

        Delete.Index("IX_RecipeComponents_Name").OnTable("RecipeComponents");
        Delete.Table("RecipeComponents");

        Delete.Column("UseEarliestIngredientExpiry").FromTable("Meals");
        Delete.Column("ShelfLifeHours").FromTable("Meals");
    }
}
