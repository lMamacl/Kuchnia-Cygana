using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(108)]
public sealed class AddCustomerProfilePublicId : Migration
{
    public override void Up()
    {
        if (!Schema.Table("CustomerProfiles").Column("PublicId").Exists())
        {
            Alter.Table("CustomerProfiles")
                .AddColumn("PublicId").AsGuid().Nullable();

            Execute.Sql("UPDATE [CustomerProfiles] SET [PublicId] = NEWID() WHERE [PublicId] IS NULL;");

            Alter.Column("PublicId")
                .OnTable("CustomerProfiles")
                .AsGuid()
                .NotNullable()
                .WithDefault(SystemMethods.NewGuid);

            Create.Index("UX_CustomerProfiles_PublicId")
                .OnTable("CustomerProfiles")
                .OnColumn("PublicId").Unique();
        }
    }

    public override void Down()
    {
        if (Schema.Table("CustomerProfiles").Index("UX_CustomerProfiles_PublicId").Exists())
        {
            Delete.Index("UX_CustomerProfiles_PublicId").OnTable("CustomerProfiles");
        }

        if (Schema.Table("CustomerProfiles").Column("PublicId").Exists())
        {
            Execute.Sql(
                """
                DECLARE @constraintName nvarchar(128);
                SELECT @constraintName = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                INNER JOIN sys.tables t ON t.object_id = c.object_id
                WHERE t.name = 'CustomerProfiles' AND c.name = 'PublicId';

                IF @constraintName IS NOT NULL
                    EXEC('ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @constraintName + ']');
                """);

            Delete.Column("PublicId").FromTable("CustomerProfiles");
        }
    }
}
