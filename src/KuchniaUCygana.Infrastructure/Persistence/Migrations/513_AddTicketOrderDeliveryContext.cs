using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(513)]
public sealed class AddTicketOrderDeliveryContext : Migration
{
    public override void Up()
    {
        if (!Schema.Table("Tickets").Exists())
        {
            return;
        }

        Execute.Sql(
            """
            IF COL_LENGTH(N'dbo.Tickets', N'OrderId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Tickets] ADD [OrderId] int NULL;
            END;

            IF COL_LENGTH(N'dbo.Tickets', N'DeliveryCalendarId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Tickets] ADD [DeliveryCalendarId] int NULL;
            END;

            IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] = N'FK_Tickets_Orders')
            BEGIN
                ALTER TABLE [dbo].[Tickets] WITH CHECK
                ADD CONSTRAINT [FK_Tickets_Orders]
                FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);
            END;

            IF OBJECT_ID(N'dbo.DeliveryCalendar', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] = N'FK_Tickets_DeliveryCalendar')
            BEGIN
                ALTER TABLE [dbo].[Tickets] WITH CHECK
                ADD CONSTRAINT [FK_Tickets_DeliveryCalendar]
                FOREIGN KEY ([DeliveryCalendarId]) REFERENCES [dbo].[DeliveryCalendar] ([Id]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_Tickets_OrderDelivery_Active'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Tickets]'))
            BEGIN
                CREATE INDEX [IX_Tickets_OrderDelivery_Active]
                ON [dbo].[Tickets] ([OrderId], [DeliveryCalendarId], [IsDeleted])
                INCLUDE ([ClientUserId], [Status], [Priority], [CreatedAt]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_Tickets_Client_Status_Created'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Tickets]'))
            BEGIN
                CREATE INDEX [IX_Tickets_Client_Status_Created]
                ON [dbo].[Tickets] ([ClientUserId], [Status], [CreatedAt] DESC)
                INCLUDE ([OrderId], [DeliveryCalendarId], [Priority]);
            END;
            """);
    }

    public override void Down()
    {
        if (!Schema.Table("Tickets").Exists())
        {
            return;
        }

        Execute.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_Tickets_Client_Status_Created'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Tickets]'))
            BEGIN
                DROP INDEX [IX_Tickets_Client_Status_Created] ON [dbo].[Tickets];
            END;

            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_Tickets_OrderDelivery_Active'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Tickets]'))
            BEGIN
                DROP INDEX [IX_Tickets_OrderDelivery_Active] ON [dbo].[Tickets];
            END;

            IF EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [name] = N'FK_Tickets_DeliveryCalendar')
            BEGIN
                ALTER TABLE [dbo].[Tickets] DROP CONSTRAINT [FK_Tickets_DeliveryCalendar];
            END;

            IF EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [name] = N'FK_Tickets_Orders')
            BEGIN
                ALTER TABLE [dbo].[Tickets] DROP CONSTRAINT [FK_Tickets_Orders];
            END;

            IF COL_LENGTH(N'dbo.Tickets', N'DeliveryCalendarId') IS NOT NULL
            BEGIN
                ALTER TABLE [dbo].[Tickets] DROP COLUMN [DeliveryCalendarId];
            END;

            IF COL_LENGTH(N'dbo.Tickets', N'OrderId') IS NOT NULL
            BEGIN
                ALTER TABLE [dbo].[Tickets] DROP COLUMN [OrderId];
            END;
            """);
    }
}
