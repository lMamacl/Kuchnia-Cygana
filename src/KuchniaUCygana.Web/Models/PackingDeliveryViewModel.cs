using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Web.Models;

public sealed class PackingDeliveryViewModel
{
    public DateOnly SelectedDate { get; set; }

    public PackingRouteDto Route { get; set; } = null!;

    public PackingManifestDto? Manifest { get; set; }

    public ManifestControlDto? ManifestControl { get; set; }
}
