using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.AI;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Główny obiekt trasy. Agreguje przystanki,
/// przypisanego kierowcę i pojazd.
/// </summary>

public sealed class DeliveryRoute : AuditableEntity
{
    public DateTimeOffset RouteDate { get; set; }

    public double TotalDistanceKm { get; set; }

    public RouteStatus Status { get; set; } = RouteStatus.Created;

    public int? VehicleId { get; set; }

    public int? DriverId { get; set; }

    // nie ma właściwości nawigacyjnych typowych dla EF bo mamy ServiceStack.AI i nie potrzebujemy ich do mapowania relacji

    public void AssignDriver(int driverid)
    {
        this.DriverId = driverid;
    }

    public void AssignVehicle(int vehicleid)
    {
        this.VehicleId = vehicleid;
    }
}
