using System;
using System.Collections.Generic;

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class ManifestPreviewDto
{
    public DateOnly PackingDate { get; set; }

    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public string VehicleRegistration { get; set; } = string.Empty;

    public int TotalBags { get; set; }

    public int TotalBoxes { get; set; }

    public List<ManifestBagDto> Bags { get; set; } = new();

    public string GeneratedBy { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; set; }
}

public sealed class ManifestBagDto
{
    public int PackingSessionId { get; set; }

    public int OrderId { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public int StopNumber { get; set; }

    public int BoxCount { get; set; }

    public string Status { get; set; } = string.Empty;
}
