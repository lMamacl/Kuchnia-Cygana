using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(18)]
public sealed class AddWarehouseCategoriesHaccpLocationsAndNotifications : Migration
{
    public override void Up()
    {
        Create.Table("WarehouseCategories")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Code").AsString(40).NotNullable()
            .WithColumn("Name").AsString(80).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("DisplayOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Execute.Sql(
            """
            SET IDENTITY_INSERT [WarehouseCategories] ON;
            INSERT INTO [WarehouseCategories] ([Id], [Code], [Name], [IsActive], [DisplayOrder], [CreatedAt], [UpdatedAt])
            VALUES
                (1, N'MEAT_FISH', N'Mięso/Ryby', 1, 10, SYSDATETIMEOFFSET(), NULL),
                (2, N'DAIRY', N'Nabiał', 1, 20, SYSDATETIMEOFFSET(), NULL),
                (3, N'VEG_FRUIT', N'Warzywa i owoce', 1, 30, SYSDATETIMEOFFSET(), NULL),
                (4, N'DRY', N'Suche', 1, 40, SYSDATETIMEOFFSET(), NULL),
                (5, N'PACKAGING', N'Opakowania', 1, 50, SYSDATETIMEOFFSET(), NULL);
            SET IDENTITY_INSERT [WarehouseCategories] OFF;
            """);

        Alter.Table("StockItems")
            .AddColumn("WarehouseCategoryId").AsInt32().Nullable();

        Execute.Sql(
            """
            UPDATE [StockItems]
            SET [WarehouseCategoryId] = CASE
                WHEN LOWER([Name]) LIKE N'%pude%' OR LOWER([Name]) LIKE N'%torba%' OR LOWER([Name]) LIKE N'%opakow%' THEN 5
                WHEN LOWER([Name]) LIKE N'%kurczak%' OR LOWER([Name]) LIKE N'%oso%' OR LOWER([Name]) LIKE N'%mięs%' OR LOWER([Name]) LIKE N'%mies%' OR LOWER([Name]) LIKE N'%ryb%' OR LOWER([Name]) LIKE N'%indyka%' THEN 1
                WHEN LOWER([Name]) LIKE N'%mietanka%' OR LOWER([Name]) LIKE N'%śmietanka%' OR LOWER([Name]) LIKE N'%mas%' OR LOWER([Name]) LIKE N'%ser %' OR LOWER([Name]) LIKE N'%gouda%' OR LOWER([Name]) LIKE N'%jogurt%' OR LOWER([Name]) LIKE N'%mleko%' THEN 2
                WHEN LOWER([Name]) LIKE N'%broku%' OR LOWER([Name]) LIKE N'%dynia%' OR LOWER([Name]) LIKE N'%batat%' OR LOWER([Name]) LIKE N'%jagod%' OR LOWER([Name]) LIKE N'%ziemniak%' OR (LOWER([Name]) LIKE N'%pomidor%' AND LOWER([Name]) NOT LIKE N'%puszka%') THEN 3
                ELSE 4
            END
            WHERE [WarehouseCategoryId] IS NULL;
            """);

        Alter.Column("WarehouseCategoryId")
            .OnTable("StockItems")
            .AsInt32()
            .NotNullable()
            .WithDefaultValue(4);

        Create.ForeignKey("FK_StockItems_WarehouseCategories")
            .FromTable("StockItems").ForeignColumn("WarehouseCategoryId")
            .ToTable("WarehouseCategories").PrimaryColumn("Id");

        Create.Table("HaccpLocations")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Code").AsString(40).NotNullable()
            .WithColumn("Name").AsString(120).NotNullable()
            .WithColumn("MinTemperatureCelsius").AsDecimal(9, 4).NotNullable()
            .WithColumn("MaxTemperatureCelsius").AsDecimal(9, 4).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("DisplayOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Notes").AsString(255).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Execute.Sql(
            """
            SET IDENTITY_INSERT [HaccpLocations] ON;
            INSERT INTO [HaccpLocations]
                ([Id], [Code], [Name], [MinTemperatureCelsius], [MaxTemperatureCelsius], [IsActive], [DisplayOrder], [Notes], [CreatedAt], [UpdatedAt])
            VALUES
                (1, N'FRIDGE_DAIRY_1', N'Lodówka #1 - Nabiał', 2.0, 6.0, 1, 10, NULL, SYSDATETIMEOFFSET(), NULL),
                (2, N'COLD_MEAT_2', N'Chłodnia #2 - Mięso', 0.0, 4.0, 1, 20, NULL, SYSDATETIMEOFFSET(), NULL),
                (3, N'FREEZER_FISH_3', N'Zamrażarka #3 - Ryby', -22.0, -18.0, 1, 30, NULL, SYSDATETIMEOFFSET(), NULL),
                (4, N'FRIDGE_VEG_4', N'Lodówka #4 - Warzywa', 4.0, 8.0, 1, 40, NULL, SYSDATETIMEOFFSET(), NULL),
                (5, N'SERVICE_DISPLAY', N'Witryna Wydawcza', 2.0, 8.0, 1, 50, NULL, SYSDATETIMEOFFSET(), NULL),
                (6, N'DRY_STORAGE', N'Magazyn Suchy', 15.0, 25.0, 1, 60, NULL, SYSDATETIMEOFFSET(), NULL);
            SET IDENTITY_INSERT [HaccpLocations] OFF;
            """);

        Create.Table("HaccpLocationCategories")
            .WithColumn("HaccpLocationId").AsInt32().NotNullable()
            .WithColumn("WarehouseCategoryId").AsInt32().NotNullable();

        Create.PrimaryKey("PK_HaccpLocationCategories")
            .OnTable("HaccpLocationCategories")
            .Columns("HaccpLocationId", "WarehouseCategoryId");

        Create.ForeignKey("FK_HaccpLocationCategories_HaccpLocations")
            .FromTable("HaccpLocationCategories").ForeignColumn("HaccpLocationId")
            .ToTable("HaccpLocations").PrimaryColumn("Id");

        Create.ForeignKey("FK_HaccpLocationCategories_WarehouseCategories")
            .FromTable("HaccpLocationCategories").ForeignColumn("WarehouseCategoryId")
            .ToTable("WarehouseCategories").PrimaryColumn("Id");

        Execute.Sql(
            """
            INSERT INTO [HaccpLocationCategories] ([HaccpLocationId], [WarehouseCategoryId])
            VALUES
                (1, 2),
                (2, 1),
                (3, 1),
                (4, 3),
                (5, 1),
                (5, 2),
                (5, 3),
                (6, 4),
                (6, 5);
            """);

        Alter.Table("TemperatureLogs")
            .AddColumn("HaccpLocationId").AsInt32().Nullable();

        Alter.Column("DeviceNameOrLocation")
            .OnTable("TemperatureLogs")
            .AsString(120)
            .NotNullable();

        Create.ForeignKey("FK_TemperatureLogs_HaccpLocations")
            .FromTable("TemperatureLogs").ForeignColumn("HaccpLocationId")
            .ToTable("HaccpLocations").PrimaryColumn("Id");

        Execute.Sql(
            """
            UPDATE [TemperatureLogs]
            SET [HaccpLocationId] = CASE
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%nabia%' THEN 1
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%mięs%' OR LOWER([DeviceNameOrLocation]) LIKE N'%mies%' OR LOWER([DeviceNameOrLocation]) LIKE N'%chłodnia a%' OR LOWER([DeviceNameOrLocation]) LIKE N'%chlodnia a%' THEN 2
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%zamra%' OR LOWER([DeviceNameOrLocation]) LIKE N'%freezer%' THEN 3
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%warzyw%' THEN 4
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%witryna%' THEN 5
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%suchy%' THEN 6
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%chłodnia b%' OR LOWER([DeviceNameOrLocation]) LIKE N'%chlodnia b%' THEN 2
                ELSE NULL
            END
            WHERE [HaccpLocationId] IS NULL;
            """);

        Create.Table("HaccpTemperatureAlerts")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("HaccpLocationId").AsInt32().NotNullable()
            .WithColumn("TemperatureLogId").AsInt64().NotNullable()
            .WithColumn("Status").AsString(20).NotNullable()
            .WithColumn("TriggeredTemperatureCelsius").AsDecimal(9, 4).NotNullable()
            .WithColumn("LastTemperatureCelsius").AsDecimal(9, 4).NotNullable()
            .WithColumn("MinTemperatureCelsius").AsDecimal(9, 4).NotNullable()
            .WithColumn("MaxTemperatureCelsius").AsDecimal(9, 4).NotNullable()
            .WithColumn("OpenedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("LastObservedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("ClosedAt").AsDateTimeOffset().Nullable()
            .WithColumn("Message").AsString(500).NotNullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.ForeignKey("FK_HaccpTemperatureAlerts_HaccpLocations")
            .FromTable("HaccpTemperatureAlerts").ForeignColumn("HaccpLocationId")
            .ToTable("HaccpLocations").PrimaryColumn("Id");

        Create.ForeignKey("FK_HaccpTemperatureAlerts_TemperatureLogs")
            .FromTable("HaccpTemperatureAlerts").ForeignColumn("TemperatureLogId")
            .ToTable("TemperatureLogs").PrimaryColumn("Id");

        Create.Table("Notifications")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("Type").AsString(50).NotNullable()
            .WithColumn("Severity").AsString(20).NotNullable()
            .WithColumn("Title").AsString(160).NotNullable()
            .WithColumn("Message").AsString(1000).NotNullable()
            .WithColumn("LinkUrl").AsString(300).Nullable()
            .WithColumn("DeduplicationKey").AsString(160).Nullable()
            .WithColumn("SourceType").AsString(50).Nullable()
            .WithColumn("SourceId").AsInt64().Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.Table("UserNotifications")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("NotificationId").AsInt64().NotNullable()
            .WithColumn("UserId").AsInt32().NotNullable()
            .WithColumn("IsRead").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeliveredAt").AsDateTimeOffset().NotNullable()
            .WithColumn("ReadAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.ForeignKey("FK_UserNotifications_Notifications")
            .FromTable("UserNotifications").ForeignColumn("NotificationId")
            .ToTable("Notifications").PrimaryColumn("Id");

        Create.ForeignKey("FK_UserNotifications_Users")
            .FromTable("UserNotifications").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");

        Execute.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WarehouseCategories_Code' AND object_id = OBJECT_ID(N'[dbo].[WarehouseCategories]'))
                CREATE UNIQUE INDEX [UX_WarehouseCategories_Code] ON [dbo].[WarehouseCategories] ([Code]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseCategories_IsActive_DisplayOrder' AND object_id = OBJECT_ID(N'[dbo].[WarehouseCategories]'))
                CREATE INDEX [IX_WarehouseCategories_IsActive_DisplayOrder] ON [dbo].[WarehouseCategories] ([IsActive], [DisplayOrder]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_Category_IsDeleted_Name' AND object_id = OBJECT_ID(N'[dbo].[StockItems]'))
                CREATE INDEX [IX_StockItems_Category_IsDeleted_Name] ON [dbo].[StockItems] ([WarehouseCategoryId], [IsDeleted], [Name]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_HaccpLocations_Code' AND object_id = OBJECT_ID(N'[dbo].[HaccpLocations]'))
                CREATE UNIQUE INDEX [UX_HaccpLocations_Code] ON [dbo].[HaccpLocations] ([Code]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HaccpLocations_IsActive_DisplayOrder' AND object_id = OBJECT_ID(N'[dbo].[HaccpLocations]'))
                CREATE INDEX [IX_HaccpLocations_IsActive_DisplayOrder] ON [dbo].[HaccpLocations] ([IsActive], [DisplayOrder]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HaccpLocationCategories_WarehouseCategoryId' AND object_id = OBJECT_ID(N'[dbo].[HaccpLocationCategories]'))
                CREATE INDEX [IX_HaccpLocationCategories_WarehouseCategoryId] ON [dbo].[HaccpLocationCategories] ([WarehouseCategoryId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemperatureLogs_HaccpLocation_IsDeleted_RecordedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[TemperatureLogs]'))
                CREATE INDEX [IX_TemperatureLogs_HaccpLocation_IsDeleted_RecordedAt_Id]
                ON [dbo].[TemperatureLogs] ([HaccpLocationId], [IsDeleted], [RecordedAt] DESC, [Id] DESC)
                INCLUDE ([RecordedTemperatureCelsius], [DeviceNameOrLocation]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HaccpTemperatureAlerts_Location_Status_OpenedAt' AND object_id = OBJECT_ID(N'[dbo].[HaccpTemperatureAlerts]'))
                CREATE INDEX [IX_HaccpTemperatureAlerts_Location_Status_OpenedAt]
                ON [dbo].[HaccpTemperatureAlerts] ([HaccpLocationId], [Status], [OpenedAt] DESC);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Notifications_DeduplicationKey' AND object_id = OBJECT_ID(N'[dbo].[Notifications]'))
                CREATE UNIQUE INDEX [UX_Notifications_DeduplicationKey]
                ON [dbo].[Notifications] ([DeduplicationKey])
                WHERE [DeduplicationKey] IS NOT NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserNotifications_User_IsRead_DeliveredAt' AND object_id = OBJECT_ID(N'[dbo].[UserNotifications]'))
                CREATE INDEX [IX_UserNotifications_User_IsRead_DeliveredAt]
                ON [dbo].[UserNotifications] ([UserId], [IsRead], [DeliveredAt] DESC)
                INCLUDE ([NotificationId], [ReadAt]);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserNotifications_User_IsRead_DeliveredAt' AND object_id = OBJECT_ID(N'[dbo].[UserNotifications]'))
                DROP INDEX [IX_UserNotifications_User_IsRead_DeliveredAt] ON [dbo].[UserNotifications];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Notifications_DeduplicationKey' AND object_id = OBJECT_ID(N'[dbo].[Notifications]'))
                DROP INDEX [UX_Notifications_DeduplicationKey] ON [dbo].[Notifications];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HaccpTemperatureAlerts_Location_Status_OpenedAt' AND object_id = OBJECT_ID(N'[dbo].[HaccpTemperatureAlerts]'))
                DROP INDEX [IX_HaccpTemperatureAlerts_Location_Status_OpenedAt] ON [dbo].[HaccpTemperatureAlerts];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemperatureLogs_HaccpLocation_IsDeleted_RecordedAt_Id' AND object_id = OBJECT_ID(N'[dbo].[TemperatureLogs]'))
                DROP INDEX [IX_TemperatureLogs_HaccpLocation_IsDeleted_RecordedAt_Id] ON [dbo].[TemperatureLogs];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HaccpLocationCategories_WarehouseCategoryId' AND object_id = OBJECT_ID(N'[dbo].[HaccpLocationCategories]'))
                DROP INDEX [IX_HaccpLocationCategories_WarehouseCategoryId] ON [dbo].[HaccpLocationCategories];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HaccpLocations_IsActive_DisplayOrder' AND object_id = OBJECT_ID(N'[dbo].[HaccpLocations]'))
                DROP INDEX [IX_HaccpLocations_IsActive_DisplayOrder] ON [dbo].[HaccpLocations];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_HaccpLocations_Code' AND object_id = OBJECT_ID(N'[dbo].[HaccpLocations]'))
                DROP INDEX [UX_HaccpLocations_Code] ON [dbo].[HaccpLocations];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_Category_IsDeleted_Name' AND object_id = OBJECT_ID(N'[dbo].[StockItems]'))
                DROP INDEX [IX_StockItems_Category_IsDeleted_Name] ON [dbo].[StockItems];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseCategories_IsActive_DisplayOrder' AND object_id = OBJECT_ID(N'[dbo].[WarehouseCategories]'))
                DROP INDEX [IX_WarehouseCategories_IsActive_DisplayOrder] ON [dbo].[WarehouseCategories];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WarehouseCategories_Code' AND object_id = OBJECT_ID(N'[dbo].[WarehouseCategories]'))
                DROP INDEX [UX_WarehouseCategories_Code] ON [dbo].[WarehouseCategories];
            """);

        Delete.ForeignKey("FK_UserNotifications_Users").OnTable("UserNotifications");
        Delete.ForeignKey("FK_UserNotifications_Notifications").OnTable("UserNotifications");
        Delete.Table("UserNotifications");
        Delete.Table("Notifications");

        Delete.ForeignKey("FK_HaccpTemperatureAlerts_TemperatureLogs").OnTable("HaccpTemperatureAlerts");
        Delete.ForeignKey("FK_HaccpTemperatureAlerts_HaccpLocations").OnTable("HaccpTemperatureAlerts");
        Delete.Table("HaccpTemperatureAlerts");

        Delete.ForeignKey("FK_TemperatureLogs_HaccpLocations").OnTable("TemperatureLogs");
        Delete.Column("HaccpLocationId").FromTable("TemperatureLogs");
        Alter.Column("DeviceNameOrLocation")
            .OnTable("TemperatureLogs")
            .AsString(50)
            .NotNullable();

        Delete.ForeignKey("FK_HaccpLocationCategories_WarehouseCategories").OnTable("HaccpLocationCategories");
        Delete.ForeignKey("FK_HaccpLocationCategories_HaccpLocations").OnTable("HaccpLocationCategories");
        Delete.Table("HaccpLocationCategories");
        Delete.Table("HaccpLocations");

        Delete.ForeignKey("FK_StockItems_WarehouseCategories").OnTable("StockItems");
        Delete.Column("WarehouseCategoryId").FromTable("StockItems");
        Delete.Table("WarehouseCategories");
    }
}
