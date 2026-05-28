using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migracja 015: Refaktor PackingSessions — dynamiczne trasowanie (ustalenia M3↔M4 z 28.05.2026).
/// 
/// Zmiana architektoniczna:
/// - DODAJE kolumnę DeliveryCalendarId (int?, nullable) — klucz integracyjny z M1/M4.
///   Pozwala na bezpośredni JOIN z DeliveryRouteStops (M4) w celu pobrania RouteId/StopNumber.
/// - USUWA kolumny RouteId i StopNumber — te dane pobierane są odtąd dynamicznie JOIN-em,
///   co zapobiega rozsynchronizowaniu przy zmianach tras przez dyspozytora M4.
/// - Kolumna OrderId POZOSTAJE — służy do danych klienta (nazwa, adres).
/// - PackingManifests NIE jest dotknięty — manifest pozostaje historycznym snapshotem.
/// 
/// Szczegóły decyzji: docs/intermodule_integration_qna.md
/// </summary>
[Migration(15)]
public sealed class RefactorPackingSessionsDynamicRouting : Migration
{
    public override void Up()
    {
        // 1. Dodaj DeliveryCalendarId (bridge do M1.DeliveryCalendar → M4.DeliveryRouteStops)
        if (!Schema.Table("PackingSessions").Column("DeliveryCalendarId").Exists())
        {
            Alter.Table("PackingSessions")
                .AddColumn("DeliveryCalendarId").AsInt32().Nullable();

            // Indeks na DeliveryCalendarId — częste JOIN-y z DeliveryRouteStops
            Create.Index("IX_PackingSessions_DeliveryCalendarId")
                .OnTable("PackingSessions")
                .OnColumn("DeliveryCalendarId")
                .Ascending();
        }

        // 2. Usuń RouteId (dane trasy pobierane dynamicznie z DeliveryRouteStops)
        if (Schema.Table("PackingSessions").Column("RouteId").Exists())
        {
            Delete.Column("RouteId").FromTable("PackingSessions");
        }

        // 3. Usuń StopNumber (dane stopu pobierane dynamicznie z DeliveryRouteStops)
        if (Schema.Table("PackingSessions").Column("StopNumber").Exists())
        {
            Delete.Column("StopNumber").FromTable("PackingSessions");
        }
    }

    public override void Down()
    {
        // Przywróć RouteId i StopNumber
        if (!Schema.Table("PackingSessions").Column("RouteId").Exists())
        {
            Alter.Table("PackingSessions")
                .AddColumn("RouteId").AsInt32().Nullable();
        }

        if (!Schema.Table("PackingSessions").Column("StopNumber").Exists())
        {
            Alter.Table("PackingSessions")
                .AddColumn("StopNumber").AsInt32().Nullable();
        }

        // Usuń DeliveryCalendarId
        if (Schema.Table("PackingSessions").Column("DeliveryCalendarId").Exists())
        {
            if (Schema.Table("PackingSessions").Index("IX_PackingSessions_DeliveryCalendarId").Exists())
            {
                Delete.Index("IX_PackingSessions_DeliveryCalendarId").OnTable("PackingSessions");
            }

            Delete.Column("DeliveryCalendarId").FromTable("PackingSessions");
        }
    }
}
