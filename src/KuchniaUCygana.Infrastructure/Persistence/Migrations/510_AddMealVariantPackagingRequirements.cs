using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(510)]
public sealed class AddMealVariantPackagingRequirements : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackagingRequirements").Column("MealVariantId").Exists())
        {
            Alter.Table("PackagingRequirements")
                .AddColumn("MealVariantId").AsInt32().Nullable();
        }

        Execute.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE name = N'FK_PackagingRequirements_MealVariants'
                  AND parent_object_id = OBJECT_ID(N'[dbo].[PackagingRequirements]')
            )
            BEGIN
                ALTER TABLE [dbo].[PackagingRequirements]
                WITH CHECK ADD CONSTRAINT [FK_PackagingRequirements_MealVariants]
                FOREIGN KEY ([MealVariantId]) REFERENCES [dbo].[MealVariants] ([Id]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_PackagingRequirements_MealVariantId'
                  AND object_id = OBJECT_ID(N'[dbo].[PackagingRequirements]')
            )
            BEGIN
                CREATE INDEX [IX_PackagingRequirements_MealVariantId]
                ON [dbo].[PackagingRequirements] ([MealVariantId]);
            END;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_PackagingRequirements_MealVariantId'
                  AND object_id = OBJECT_ID(N'[dbo].[PackagingRequirements]')
            )
            BEGIN
                DROP INDEX [IX_PackagingRequirements_MealVariantId]
                ON [dbo].[PackagingRequirements];
            END;

            IF EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE name = N'FK_PackagingRequirements_MealVariants'
                  AND parent_object_id = OBJECT_ID(N'[dbo].[PackagingRequirements]')
            )
            BEGIN
                ALTER TABLE [dbo].[PackagingRequirements]
                DROP CONSTRAINT [FK_PackagingRequirements_MealVariants];
            END;
            """);

        if (Schema.Table("PackagingRequirements").Column("MealVariantId").Exists())
        {
            Delete.Column("MealVariantId").FromTable("PackagingRequirements");
        }
    }
}
