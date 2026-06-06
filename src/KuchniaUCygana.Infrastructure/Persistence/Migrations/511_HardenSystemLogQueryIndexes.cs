using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(511)]
public sealed class HardenSystemLogQueryIndexes : Migration
{
    public override void Up()
    {
        CreateSystemLogIndexes("SystemLogs", "SystemLogs");
        CreateSystemLogIndexes("SystemLogsArchive", "SystemLogsArchive");
    }

    public override void Down()
    {
        DeleteIndexIfExists("IX_SystemLogs_Target_Window", "SystemLogs");
        DeleteIndexIfExists("IX_SystemLogs_Action_Window", "SystemLogs");
        DeleteIndexIfExists("IX_SystemLogs_User_Window", "SystemLogs");
        DeleteIndexIfExists("IX_SystemLogs_Window", "SystemLogs");

        DeleteIndexIfExists("IX_SystemLogsArchive_Target_Window", "SystemLogsArchive");
        DeleteIndexIfExists("IX_SystemLogsArchive_Action_Window", "SystemLogsArchive");
        DeleteIndexIfExists("IX_SystemLogsArchive_User_Window", "SystemLogsArchive");
        DeleteIndexIfExists("IX_SystemLogsArchive_Window", "SystemLogsArchive");
    }

    private void CreateSystemLogIndexes(string tableName, string indexPrefix)
    {
        if (!Schema.Table(tableName).Exists())
        {
            return;
        }

        Execute.Sql($"""
            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_{indexPrefix}_Window'
                  AND [object_id] = OBJECT_ID(N'[dbo].[{tableName}]'))
            BEGIN
                CREATE INDEX [IX_{indexPrefix}_Window]
                ON [dbo].[{tableName}] ([Timestamp] DESC, [Id] DESC)
                INCLUDE ([UserId], [Action], [TargetEntity], [TargetId], [IPAddress]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_{indexPrefix}_User_Window'
                  AND [object_id] = OBJECT_ID(N'[dbo].[{tableName}]'))
            BEGIN
                CREATE INDEX [IX_{indexPrefix}_User_Window]
                ON [dbo].[{tableName}] ([UserId], [Timestamp] DESC, [Id] DESC)
                INCLUDE ([Action], [TargetEntity], [TargetId], [IPAddress]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_{indexPrefix}_Target_Window'
                  AND [object_id] = OBJECT_ID(N'[dbo].[{tableName}]'))
            BEGIN
                CREATE INDEX [IX_{indexPrefix}_Target_Window]
                ON [dbo].[{tableName}] ([TargetEntity], [TargetId], [Timestamp] DESC, [Id] DESC)
                INCLUDE ([UserId], [Action], [IPAddress]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_{indexPrefix}_Action_Window'
                  AND [object_id] = OBJECT_ID(N'[dbo].[{tableName}]'))
            BEGIN
                CREATE INDEX [IX_{indexPrefix}_Action_Window]
                ON [dbo].[{tableName}] ([Action], [Timestamp] DESC, [Id] DESC)
                INCLUDE ([UserId], [TargetEntity], [TargetId], [IPAddress]);
            END;
            """);
    }

    private void DeleteIndexIfExists(string indexName, string tableName)
    {
        if (!Schema.Table(tableName).Exists())
        {
            return;
        }

        Execute.Sql($"""
            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'{indexName}'
                  AND [object_id] = OBJECT_ID(N'[dbo].[{tableName}]'))
            BEGIN
                DROP INDEX [{indexName}] ON [dbo].[{tableName}];
            END;
            """);
    }
}
