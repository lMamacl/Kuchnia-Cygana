namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class WarehouseDemandFilterDto
{
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int Days { get; set; } = 7;
    public string? Search { get; set; }
    public string? ResourceType { get; set; }
    public string? Risk { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public bool ExportAll { get; set; }
}

public sealed class WarehouseDemandDto
{
    public WarehouseDemandFilterDto Filter { get; set; } = new();
    public DateOnly StartDate { get; set; }
    public int RangeDays { get; set; }
    public int TotalOrderItems { get; set; }
    public int TotalRows { get; set; }
    public int FilteredRows { get; set; }
    public int ShortageRows { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling((double)FilteredRows / PageSize);
    public List<WarehouseDemandDayDto> Days { get; set; } = new();
    public List<WarehouseDemandRowDto> Rows { get; set; } = new();
}

public sealed class WarehouseDemandDayDto
{
    public DateOnly PlanDate { get; set; }
    public string Status { get; set; } = "Missing";
    public int? DietMenuPlanId { get; set; }
    public int SnapshotItemCount { get; set; }
    public int OrderItemCount { get; set; }
    public int MatchedOrderItemCount { get; set; }
    public int MissingSnapshotMatches { get; set; }
    public int AlertCount { get; set; }
    public bool HasPublishedSnapshot { get; set; }
}

public sealed class WarehouseDemandRowDto
{
    public string ResourceType { get; set; } = "Ingredient";
    public string ResourceName { get; set; } = string.Empty;
    public int? IngredientId { get; set; }
    public int? StockItemId { get; set; }
    public int? WarehouseCategoryId { get; set; }
    public string? WarehouseCategoryName { get; set; }
    public string SelectionMode { get; set; } = "StockItem";
    public decimal RequiredQuantity { get; set; }
    public string Unit { get; set; } = "g";
    public decimal AvailableQuantity { get; set; }
    public decimal ShortageQuantity { get; set; }
    public DateTimeOffset? EarliestExpiryDate { get; set; }
    public int? EarliestBatchId { get; set; }
    public string RiskLabel { get; set; } = "Ok";
    public List<string> SourceMeals { get; set; } = new();
    public List<int> SourceDietMenuPlanItemIds { get; set; } = new();
}
