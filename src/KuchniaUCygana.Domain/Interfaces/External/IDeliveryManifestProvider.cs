using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KuchniaUCygana.Domain.Interfaces.External;

/// <summary>
/// Dane trasy dostaw z Modułu 4.
/// </summary>
public sealed class RouteEntry
{
    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public string VehicleRegistration { get; set; } = string.Empty;

    public List<RouteStopEntry> Stops { get; set; } = new();
}

/// <summary>
/// Dane stopu na trasie.
/// </summary>
public sealed class RouteStopEntry
{
    public int StopId { get; set; }

    public int StopOrder { get; set; }

    public string DeliveryWindowFrom { get; set; } = string.Empty;

    public string DeliveryWindowTo { get; set; } = string.Empty;

    public int OrderId { get; set; }
}

/// <summary>
/// Dostawca danych logistycznych z Modułu 4.
/// Mock w fazie rozwoju M3 → adapter do prawdziwego M4 w fazie integracji.
/// </summary>
public interface IDeliveryManifestProvider
{
    /// <summary>
    /// Pobiera trasy zaplanowane na dany dzień.
    /// </summary>
    Task<IEnumerable<RouteEntry>> GetRoutesForDateAsync(DateOnly date);

    /// <summary>
    /// Pobiera dane pojazdu.
    /// </summary>
    Task<RouteEntry?> GetRouteByIdAsync(int routeId);
}
