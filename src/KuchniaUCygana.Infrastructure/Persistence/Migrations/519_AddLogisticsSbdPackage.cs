using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(519)]
public sealed class AddLogisticsSbdPackage : Migration
{
    public override void Up()
    {
        EnsurePackageSchema();
        CreateOrReplaceRouteLoadSummaryFunction();
        CreateOrReplaceDailyDispatchBoardProcedure();
        GrantRuntimePackagePermissions();
    }

    public override void Down()
    {
        Execute.Sql("DROP PROCEDURE IF EXISTS [logistics_pkg].[usp_GetDailyDispatchBoard];");
        Execute.Sql("DROP FUNCTION IF EXISTS [logistics_pkg].[fn_RouteLoadSummary];");
        Execute.Sql(
            """
            IF SCHEMA_ID(N'logistics_pkg') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.objects
                   WHERE [schema_id] = SCHEMA_ID(N'logistics_pkg'))
            BEGIN
                EXEC(N'DROP SCHEMA [logistics_pkg]');
            END;
            """);
    }

    private void EnsurePackageSchema()
    {
        Execute.Sql(
            """
            IF SCHEMA_ID(N'logistics_pkg') IS NULL
            BEGIN
                EXEC(N'CREATE SCHEMA [logistics_pkg]');
            END;
            """);
    }

    private void CreateOrReplaceRouteLoadSummaryFunction()
    {
        Execute.Sql(
            """
            CREATE OR ALTER FUNCTION [logistics_pkg].[fn_RouteLoadSummary]
            (
                @DeliveryDate date,
                @EstimatedDeliveryWeightKg decimal(10, 2)
            )
            RETURNS TABLE
            AS
            RETURN
            (
                SELECT
                    route.[Id] AS [RouteId],
                    CAST(route.[RouteDate] AS date) AS [RouteDate],
                    route.[Name] AS [RouteName],
                    route.[Status] AS [RouteStatus],
                    CASE route.[Status]
                        WHEN 0 THEN N'Created'
                        WHEN 1 THEN N'Assigned'
                        WHEN 2 THEN N'InProgress'
                        WHEN 3 THEN N'Completed'
                        WHEN 4 THEN N'Failed'
                        ELSE N'Unknown'
                    END AS [RouteStatusName],
                    route.[VehicleId],
                    COALESCE(route.[DriverId], activeAssignment.[DriverId]) AS [DriverId],
                    CAST(ISNULL(stopStats.[StopCount], 0) AS int) AS [StopCount],
                    CAST(ISNULL(stopStats.[CompletedStopCount], 0) AS int) AS [CompletedStopCount],
                    CAST(ISNULL(stopStats.[FailedStopCount], 0) AS int) AS [FailedStopCount],
                    CAST(ISNULL(stopStats.[CityCount], 0) AS int) AS [CityCount],
                    CAST(ISNULL(stopStats.[UnlinkedStopCount], 0) AS int) AS [UnlinkedStopCount],
                    CAST(ISNULL(stopStats.[MissingCoordinatesCount], 0) AS int) AS [MissingCoordinatesCount],
                    CAST(ISNULL(stopStats.[StopCount], 0) * COALESCE(@EstimatedDeliveryWeightKg, 1.20) AS decimal(18, 2)) AS [EstimatedLoadKg],
                    vehicle.[MaxLoadKg] AS [VehicleMaxLoadKg],
                    CAST(
                        CASE
                            WHEN vehicle.[MaxLoadKg] IS NULL OR vehicle.[MaxLoadKg] <= 0 THEN NULL
                            ELSE (ISNULL(stopStats.[StopCount], 0) * COALESCE(@EstimatedDeliveryWeightKg, 1.20) / vehicle.[MaxLoadKg]) * 100
                        END
                        AS decimal(9, 2)) AS [EstimatedLoadPercent],
                    CASE WHEN manifest.[Id] IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS [HasManifest],
                    manifest.[ManifestNumber],
                    CASE
                        WHEN manifest.[Id] IS NULL THEN N'Missing'
                        WHEN manifest.[RequiresRegeneration] = 1 THEN N'RequiresRegeneration'
                        WHEN manifest.[SentToLogisticsAt] IS NOT NULL THEN N'SentToLogistics'
                        WHEN manifest.[WorkerApprovedAt] IS NOT NULL OR manifest.[IsVerified] = 1 THEN N'Approved'
                        ELSE N'Created'
                    END AS [ManifestStatusName],
                    manifest.[SentToLogisticsAt],
                    manifest.[RequiresRegeneration]
                FROM [dbo].[DeliveryRoutes] route
                LEFT JOIN [dbo].[Vehicles] vehicle
                    ON vehicle.[Id] = route.[VehicleId]
                   AND vehicle.[IsDeleted] = 0
                OUTER APPLY
                (
                    SELECT TOP (1)
                        assignment.[DriverId]
                    FROM [dbo].[DriverVehicleAssignments] assignment
                    WHERE assignment.[VehicleId] = route.[VehicleId]
                      AND assignment.[UnassignedAt] IS NULL
                    ORDER BY assignment.[AssignedAt] DESC, assignment.[Id] DESC
                ) activeAssignment
                OUTER APPLY
                (
                    SELECT
                        COUNT(1) AS [StopCount],
                        SUM(CASE WHEN stop.[Status] = 3 THEN 1 ELSE 0 END) AS [CompletedStopCount],
                        SUM(CASE WHEN stop.[Status] = 4 THEN 1 ELSE 0 END) AS [FailedStopCount],
                        COUNT(DISTINCT address.[City]) AS [CityCount],
                        SUM(CASE WHEN calendar.[Id] IS NULL THEN 1 ELSE 0 END) AS [UnlinkedStopCount],
                        SUM(CASE WHEN address.[Latitude] IS NULL OR address.[Longitude] IS NULL THEN 1 ELSE 0 END) AS [MissingCoordinatesCount]
                    FROM [dbo].[DeliveryRouteStops] stop
                    LEFT JOIN [dbo].[DeliveryCalendar] calendar
                        ON calendar.[Id] = stop.[DeliveryCalendarId]
                       AND calendar.[IsDeleted] = 0
                    LEFT JOIN [dbo].[Addresses] address
                        ON address.[Id] = calendar.[AddressId]
                       AND address.[IsDeleted] = 0
                    WHERE stop.[RouteId] = route.[Id]
                      AND stop.[IsDeleted] = 0
                ) stopStats
                OUTER APPLY
                (
                    SELECT TOP (1)
                        packingManifest.[Id],
                        packingManifest.[ManifestNumber],
                        packingManifest.[IsVerified],
                        packingManifest.[WorkerApprovedAt],
                        packingManifest.[SentToLogisticsAt],
                        packingManifest.[RequiresRegeneration]
                    FROM [dbo].[PackingManifests] packingManifest
                    WHERE packingManifest.[RouteId] = route.[Id]
                      AND packingManifest.[PackingDate] = CAST(route.[RouteDate] AS date)
                    ORDER BY
                        COALESCE(
                            packingManifest.[SentToLogisticsAt],
                            packingManifest.[WorkerApprovedAt],
                            CONVERT(datetimeoffset, packingManifest.[VerifiedAt]),
                            CONVERT(datetimeoffset, packingManifest.[GeneratedAt])) DESC,
                        packingManifest.[Id] DESC
                ) manifest
                WHERE route.[IsDeleted] = 0
                  AND CAST(route.[RouteDate] AS date) = @DeliveryDate
            );
            """);
    }

    private void CreateOrReplaceDailyDispatchBoardProcedure()
    {
        Execute.Sql(
            """
            CREATE OR ALTER PROCEDURE [logistics_pkg].[usp_GetDailyDispatchBoard]
                @DeliveryDate date,
                @EstimatedDeliveryWeightKg decimal(10, 2) = 1.20
            AS
            BEGIN
                SET NOCOUNT ON;

                IF @DeliveryDate IS NULL
                    THROW 51901, '@DeliveryDate is required.', 1;

                IF @EstimatedDeliveryWeightKg IS NULL OR @EstimatedDeliveryWeightKg <= 0
                    THROW 51902, '@EstimatedDeliveryWeightKg must be greater than zero.', 1;

                SELECT
                    summary.[RouteDate],
                    summary.[RouteId],
                    summary.[RouteName],
                    summary.[RouteStatus],
                    summary.[RouteStatusName],
                    vehicle.[RegistrationNumber] AS [VehicleRegistrationNumber],
                    vehicle.[Model] AS [VehicleModel],
                    summary.[VehicleMaxLoadKg],
                    summary.[DriverId],
                    COALESCE(
                        NULLIF(LTRIM(RTRIM(CONCAT(driverUser.[FirstName], N' ', driverUser.[LastName]))), N''),
                        driverUser.[Email],
                        N'(brak)') AS [DriverDisplayName],
                    summary.[StopCount],
                    summary.[CompletedStopCount],
                    summary.[FailedStopCount],
                    summary.[CityCount],
                    summary.[UnlinkedStopCount],
                    summary.[MissingCoordinatesCount],
                    summary.[EstimatedLoadKg],
                    summary.[EstimatedLoadPercent],
                    summary.[HasManifest],
                    summary.[ManifestNumber],
                    summary.[ManifestStatusName],
                    summary.[SentToLogisticsAt],
                    summary.[RequiresRegeneration]
                FROM [logistics_pkg].[fn_RouteLoadSummary](@DeliveryDate, @EstimatedDeliveryWeightKg) summary
                LEFT JOIN [dbo].[Vehicles] vehicle
                    ON vehicle.[Id] = summary.[VehicleId]
                LEFT JOIN [dbo].[Drivers] driver
                    ON driver.[Id] = summary.[DriverId]
                   AND driver.[IsDeleted] = 0
                LEFT JOIN [dbo].[Users] driverUser
                    ON driverUser.[Id] = driver.[UserId]
                ORDER BY
                    summary.[RouteDate],
                    summary.[RouteName],
                    summary.[RouteId];

                SELECT
                    @DeliveryDate AS [DeliveryDate],
                    COUNT(1) AS [RouteCount],
                    ISNULL(SUM(summary.[StopCount]), 0) AS [StopCount],
                    ISNULL(SUM(summary.[CompletedStopCount]), 0) AS [CompletedStopCount],
                    ISNULL(SUM(summary.[FailedStopCount]), 0) AS [FailedStopCount],
                    ISNULL(SUM(summary.[UnlinkedStopCount]), 0) AS [UnlinkedStopCount],
                    ISNULL(SUM(summary.[MissingCoordinatesCount]), 0) AS [MissingCoordinatesCount],
                    CAST(ISNULL(SUM(summary.[EstimatedLoadKg]), 0) AS decimal(18, 2)) AS [EstimatedLoadKg],
                    ISNULL(SUM(CASE WHEN summary.[HasManifest] = 1 THEN 1 ELSE 0 END), 0) AS [RoutesWithManifest],
                    ISNULL(SUM(CASE WHEN summary.[ManifestStatusName] = N'SentToLogistics' THEN 1 ELSE 0 END), 0) AS [RoutesSentToLogistics],
                    ISNULL(SUM(CASE WHEN summary.[RequiresRegeneration] = 1 THEN 1 ELSE 0 END), 0) AS [RoutesRequiringRegeneration]
                FROM [logistics_pkg].[fn_RouteLoadSummary](@DeliveryDate, @EstimatedDeliveryWeightKg) summary;
            END;
            """);
    }

    private void GrantRuntimePackagePermissions()
    {
        Execute.Sql(
            """
            IF USER_ID(N'pracownik') IS NOT NULL
            BEGIN
                GRANT EXECUTE ON SCHEMA::[logistics_pkg] TO [pracownik];
                GRANT SELECT ON SCHEMA::[logistics_pkg] TO [pracownik];
            END;
            """);
    }
}
