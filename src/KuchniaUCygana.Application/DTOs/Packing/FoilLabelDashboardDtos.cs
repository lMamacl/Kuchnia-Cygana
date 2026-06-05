using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class FoilLabelFilterDto
{
    public DateOnly Date { get; set; }

    public string? Search { get; set; }

    public string? Status { get; set; }

    public string? LabelState { get; set; }

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

public sealed class FoilLabelDashboardDto
{
    public FoilLabelFilterDto Filter { get; set; } = new();

    public PagedResultDto<FoilLabelItemDto> Items { get; set; } = new();

    public int TotalBoxes { get; set; }

    public int PendingCount { get; set; }

    public int PrintedCount { get; set; }

    public int ReprintCount { get; set; }

    public int BlockedCount { get; set; }

    public int PackedCount { get; set; }
}

public sealed class FoilLabelPreparationResultDto
{
    public int CreatedBoxes { get; set; }

    public int SkippedSessions { get; set; }

    public List<string> Errors { get; set; } = new();
}

public sealed class FoilLabelItemDto
{
    public int Id { get; set; }

    public int PackingSessionId { get; set; }

    public int? PackingBagId { get; set; }

    public int? ProductionPlanItemId { get; set; }

    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public string BoxCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? FoilPrintedAt { get; set; }

    public DateTimeOffset? PackedAt { get; set; }

    public bool IsDamaged { get; set; }

    public string? Remarks { get; set; }

    public int? OrderId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public string? ClientName { get; set; }

    public string ClientPublicIdDisplay { get; set; } = string.Empty;

    public int? RouteId { get; set; }

    public int? StopNumber { get; set; }

    public int ProductLabelPrintCount { get; set; }

    public DateTimeOffset? LatestProductLabelPrintedAt { get; set; }

    public string? ProductionStatus { get; set; }

    public DateTimeOffset? PackagingDeductedAt { get; set; }

    public string? FoilBlockReason { get; set; }

    public bool IsReadyForFoil => string.IsNullOrWhiteSpace(FoilBlockReason);

    public bool CanPrint => Status is "Pending" && IsReadyForFoil;

    public bool CanReprint => (Status is "FoilPrinted" or "Packed") && ProductLabelPrintCount > 0 && IsReadyForFoil;

    public bool IsBlocked => Status is "Damaged" or "Missing" || IsDamaged || !IsReadyForFoil;
}
