using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class DeliveryRouteDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset RouteDate { get; set; }

    public double TotalDistanceKm { get; set; }

    public string Status { get; set; } = string.Empty;

    public int? DriverId { get; set; }

    public string? DriverName { get; set; } // Zamiast DriverId wysyłamy od razu Imię i Nazwisko

    public int? VehicleId { get; set; }

    public string? VehicleRegistration { get; set; }

    // Lista przystanków wewnątrz trasy
    public List<RouteStopDto> Stops { get; set; } = new();
}
