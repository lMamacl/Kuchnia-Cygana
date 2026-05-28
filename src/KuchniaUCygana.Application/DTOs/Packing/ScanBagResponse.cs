namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class ScanBagResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public string VehicleRegistration { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public int OrderId { get; set; }

    public int StopNumber { get; set; }

    public bool AllBagsLoaded { get; set; }
}
