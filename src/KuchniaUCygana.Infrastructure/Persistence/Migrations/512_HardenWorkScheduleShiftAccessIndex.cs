using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(512)]
public sealed class HardenWorkScheduleShiftAccessIndex : Migration
{
    public override void Up()
    {
        if (!Schema.Table("WorkSchedules").Exists())
        {
            return;
        }

        Execute.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_WorkSchedules_User_Date_Active'
                  AND [object_id] = OBJECT_ID(N'[dbo].[WorkSchedules]'))
            BEGIN
                CREATE INDEX [IX_WorkSchedules_User_Date_Active]
                ON [dbo].[WorkSchedules] ([UserId], [ShiftDate], [IsDeleted])
                INCLUDE ([Shift], [RoleAtShift]);
            END;
            """);
    }

    public override void Down()
    {
        if (!Schema.Table("WorkSchedules").Exists())
        {
            return;
        }

        Execute.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_WorkSchedules_User_Date_Active'
                  AND [object_id] = OBJECT_ID(N'[dbo].[WorkSchedules]'))
            BEGIN
                DROP INDEX [IX_WorkSchedules_User_Date_Active] ON [dbo].[WorkSchedules];
            END;
            """);
    }
}
