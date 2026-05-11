using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(301)]
public sealed class CreateMenuTables : Migration
{
    public override void Up()
    {
        Create.Table("Categories")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Description").AsString(500).Nullable()
            .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.Table("Allergens")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Code").AsString(20).NotNullable().Unique()
            .WithColumn("IconUrl").AsString(500).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.Table("Ingredients")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Unit").AsString(20).NotNullable()
            .WithColumn("CostPerUnit").AsDecimal(10, 4).NotNullable()
            .WithColumn("Notes").AsString(500).Nullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.Index("IX_Ingredients_Name").OnTable("Ingredients").OnColumn("Name");

        Create.Table("Diets")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("MarketingDescription").AsString(5000).Nullable()
            .WithColumn("Status").AsString(30).NotNullable().WithDefaultValue("Draft")
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("ThumbnailUrl").AsString(500).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.Table("DietVariants")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("DietId").AsInt32().NotNullable()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("TargetCalories").AsInt32().NotNullable()
            .WithColumn("PriceMultiplier").AsDecimal(5, 2).NotNullable().WithDefaultValue(1.0)
            .WithColumn("IsDefault").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_DietVariants_Diets")
            .FromTable("DietVariants").ForeignColumn("DietId")
            .ToTable("Diets").PrimaryColumn("Id");

        Create.Table("Meals")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CategoryId").AsInt32().NotNullable()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("MarketingDescription").AsString(5000).Nullable()
            .WithColumn("Status").AsString(30).NotNullable().WithDefaultValue("Draft")
            .WithColumn("PreparationTimeMinutes").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable();

        Create.ForeignKey("FK_Meals_Categories")
            .FromTable("Meals").ForeignColumn("CategoryId")
            .ToTable("Categories").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("Meals");
        Delete.Table("DietVariants");
        Delete.Table("Diets");
        Delete.Table("Ingredients");
        Delete.Table("Allergens");
        Delete.Table("Categories");
    }
}
