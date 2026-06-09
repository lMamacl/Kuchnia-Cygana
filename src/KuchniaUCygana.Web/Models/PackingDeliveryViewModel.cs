using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Web.Models;

public sealed class PackingDeliveryViewModel
{
    public DateOnly SelectedDate { get; set; }

    public PackingRouteDto Route { get; set; } = null!;

    public PackingManifestDto? Manifest { get; set; }

    public ManifestControlDto? ManifestControl { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    public int TotalBags { get; set; }

    public int TotalPages => TotalBags == 0 ? 1 : (int)Math.Ceiling(TotalBags / (double)PageSize);

    public int FirstItem => TotalBags == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => Math.Min(Page * PageSize, TotalBags);
}
