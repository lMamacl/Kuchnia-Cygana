namespace KuchniaUCygana.Application.DTOs.Production;

public sealed class PackingBoardDto
{
    public DateOnly PackingDate { get; set; }

    public int TotalBags { get; set; }

    public int PackedBags { get; set; }

    public int LoadedBags { get; set; }

    public List<PackingRouteDto> Routes { get; set; } = new();
}

public sealed class PackingRouteDto
{
    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public string VehicleRegistration { get; set; } = string.Empty;

    public int TotalBags { get; set; }

    public int PackedBags { get; set; }

    public int LoadedBags { get; set; }

    public int DispatchedBags { get; set; }

    public bool AllBagsPacked { get; set; }

    public bool AllBagsLoaded { get; set; }

    public bool HasManifest { get; set; }

    public bool IsManifestVerified { get; set; }

    public int? ManifestId { get; set; }

    public string? ManifestNumber { get; set; }

    public DateTimeOffset? ManifestGeneratedAt { get; set; }

    public DateTimeOffset? ManifestVerifiedAt { get; set; }

    public bool CanGenerateManifest { get; set; }

    public bool CanVerifyManifest { get; set; }

    public bool CanLoadBags { get; set; }

    public bool CanDispatchDelivery { get; set; }

    public List<PackingBagDto> Bags { get; set; } = new();
}

public sealed class PackingBagDto
{
    public int PackingSessionId { get; set; }

    public int OrderId { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public string DietType { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public string VehicleRegistration { get; set; } = string.Empty;

    public int StopNumber { get; set; }

    public string DeliveryWindow { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string StatusColor { get; set; } = string.Empty;

    public int TotalBoxes { get; set; }

    public int PackedBoxes { get; set; }

    public bool HasLabels { get; set; }

    public bool CanPackBag { get; set; }

    public bool CanLoad { get; set; }
}
