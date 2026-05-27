using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations; 

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Główny obiekt trasy. Agreguje przystanki,
/// przypisanego kierowcę i pojazd.
/// </summary>

public sealed class DeliveryRoute : AuditableEntity
{
    public DateTimeOffset RouteDate { get; set; }

    public string Name { get; set; } = string.Empty;

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

    [NotMapped]
    public List<DeliveryRouteStop> Stops { get; set; } = new();
}
