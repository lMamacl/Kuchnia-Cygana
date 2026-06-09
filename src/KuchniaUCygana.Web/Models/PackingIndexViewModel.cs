using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Web.Models;

public sealed class PackingIndexViewModel
{
    public DateOnly SelectedDate { get; set; }

    public PackingBoardDto Board { get; set; } = new();

    public IReadOnlyList<PackingRouteDto> AllRoutes { get; set; } = Array.Empty<PackingRouteDto>();

    public string Mode { get; set; } = "packing";

    public string? Search { get; set; }

    public int? RouteId { get; set; }

    public string LabelStatus { get; set; } = "all";

    public string BagStatus { get; set; } = "all";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public int TotalBags { get; set; }

    public int TotalPages => TotalBags == 0 ? 1 : (int)Math.Ceiling(TotalBags / (double)PageSize);

    public int FirstItem => TotalBags == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => Math.Min(Page * PageSize, TotalBags);
}
