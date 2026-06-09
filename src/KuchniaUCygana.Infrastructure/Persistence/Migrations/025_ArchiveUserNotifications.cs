using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(25)]
public sealed class ArchiveUserNotifications : Migration
{
    public override void Up()
    {
        if (!Schema.Table("UserNotifications").Column("ArchivedAt").Exists())
        {
            Alter.Table("UserNotifications")
                .AddColumn("ArchivedAt").AsDateTimeOffset().Nullable();
        }

        if (!Schema.Table("UserNotifications").Column("ArchivedByUserId").Exists())
        {
            Alter.Table("UserNotifications")
                .AddColumn("ArchivedByUserId").AsInt32().Nullable();
        }

        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserNotifications_User_IsRead_DeliveredAt' AND object_id = OBJECT_ID(N'[dbo].[UserNotifications]'))
                DROP INDEX [IX_UserNotifications_User_IsRead_DeliveredAt] ON [dbo].[UserNotifications];

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserNotifications_User_Archived_Read_DeliveredAt' AND object_id = OBJECT_ID(N'[dbo].[UserNotifications]'))
                CREATE INDEX [IX_UserNotifications_User_Archived_Read_DeliveredAt]
                ON [dbo].[UserNotifications] ([UserId], [ArchivedAt], [IsRead], [DeliveredAt] DESC)
                INCLUDE ([NotificationId], [ReadAt]);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserNotifications_User_Archived_Read_DeliveredAt' AND object_id = OBJECT_ID(N'[dbo].[UserNotifications]'))
                DROP INDEX [IX_UserNotifications_User_Archived_Read_DeliveredAt] ON [dbo].[UserNotifications];

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserNotifications_User_IsRead_DeliveredAt' AND object_id = OBJECT_ID(N'[dbo].[UserNotifications]'))
                CREATE INDEX [IX_UserNotifications_User_IsRead_DeliveredAt]
                ON [dbo].[UserNotifications] ([UserId], [IsRead], [DeliveredAt] DESC)
                INCLUDE ([NotificationId], [ReadAt]);
            """);

        if (Schema.Table("UserNotifications").Column("ArchivedByUserId").Exists())
        {
            Delete.Column("ArchivedByUserId").FromTable("UserNotifications");
        }

        if (Schema.Table("UserNotifications").Column("ArchivedAt").Exists())
        {
            Delete.Column("ArchivedAt").FromTable("UserNotifications");
        }
    }
}
