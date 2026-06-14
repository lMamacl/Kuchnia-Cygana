using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(520)]
public sealed class GrantExecuteOnArchiveSystemLogs : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            IF USER_ID(N'pracownik') IS NOT NULL
            BEGIN
                GRANT EXECUTE ON OBJECT::[dbo].[usp_ArchiveSystemLogs] TO [pracownik];
            END;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF USER_ID(N'pracownik') IS NOT NULL
            BEGIN
                REVOKE EXECUTE ON OBJECT::[dbo].[usp_ArchiveSystemLogs] FROM [pracownik];
            END;
            """);
    }
}
