using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(403, "Add logistics integrity indexes")]
public sealed class AddLogisticsIntegrityIndexes : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_Vehicles_Active_RegistrationNumber'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Vehicles]')
            )
                CREATE UNIQUE INDEX [UX_Vehicles_Active_RegistrationNumber]
                    ON [dbo].[Vehicles] ([RegistrationNumber])
                    WHERE [IsDeleted] = 0;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_Drivers_Active_LicenseNumber'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Drivers]')
            )
                CREATE UNIQUE INDEX [UX_Drivers_Active_LicenseNumber]
                    ON [dbo].[Drivers] ([LicenseNumber])
                    WHERE [IsDeleted] = 0;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_Drivers_Active_User'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Drivers]')
            )
                CREATE UNIQUE INDEX [UX_Drivers_Active_User]
                    ON [dbo].[Drivers] ([UserId])
                    WHERE [IsDeleted] = 0;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_DeliveryRouteStops_Active_Calendar'
                  AND [object_id] = OBJECT_ID(N'[dbo].[DeliveryRouteStops]')
            )
                CREATE UNIQUE INDEX [UX_DeliveryRouteStops_Active_Calendar]
                    ON [dbo].[DeliveryRouteStops] ([DeliveryCalendarId])
                    WHERE [IsDeleted] = 0;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_DeliveryRouteStops_Active_Calendar'
                  AND [object_id] = OBJECT_ID(N'[dbo].[DeliveryRouteStops]')
            )
                DROP INDEX [UX_DeliveryRouteStops_Active_Calendar] ON [dbo].[DeliveryRouteStops];

            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_Drivers_Active_User'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Drivers]')
            )
                DROP INDEX [UX_Drivers_Active_User] ON [dbo].[Drivers];

            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_Drivers_Active_LicenseNumber'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Drivers]')
            )
                DROP INDEX [UX_Drivers_Active_LicenseNumber] ON [dbo].[Drivers];

            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_Vehicles_Active_RegistrationNumber'
                  AND [object_id] = OBJECT_ID(N'[dbo].[Vehicles]')
            )
                DROP INDEX [UX_Vehicles_Active_RegistrationNumber] ON [dbo].[Vehicles];
            """);
    }
}
