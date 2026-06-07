namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class TicketOperationalContextDto
{
    public int TicketId { get; set; }

    public int? OrderId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public string? OrderNumber { get; set; }

    public string? OrderStatus { get; set; }

    public DateTime? OrderStartDate { get; set; }

    public DateTime? OrderEndDate { get; set; }

    public decimal? FinalPrice { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public string? DeliveryStatus { get; set; }

    public bool IsSkipped { get; set; }

    public string? SkipReason { get; set; }

    public DateTimeOffset? CutoffTime { get; set; }

    public string? DeliveryWindowName { get; set; }

    public string? DeliveryWindowRange { get; set; }

    public string? AddressFullLine { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public string? DeliveryNotes { get; set; }

    public string? ClientPublicId { get; set; }

    public IReadOnlyList<TicketOrderItemContextDto> OrderItems { get; set; } = [];

    public IReadOnlyList<TicketMealContextDto> Meals { get; set; } = [];

    public IReadOnlyList<TicketPackingBagContextDto> PackingBags { get; set; } = [];

    public IReadOnlyList<TicketPackingIncidentContextDto> PackingIncidents { get; set; } = [];

    public TicketRouteContextDto? Route { get; set; }

    public IReadOnlyList<string> AssessmentHints { get; set; } = [];
}

public sealed class TicketOrderItemContextDto
{
    public int DietId { get; set; }

    public int DietVariantId { get; set; }

    public string DietName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public int CaloriesPerDay { get; set; }

    public int TotalDays { get; set; }

    public int? MealId { get; set; }

    public int? MealVariantId { get; set; }

    public int? DietMenuPlanItemId { get; set; }

    public string? MealSlot { get; set; }
}

public sealed class TicketMealContextDto
{
    public string DietName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public string? MealSlot { get; set; }

    public int? MealId { get; set; }

    public int? DietMenuPlanItemId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public IReadOnlyList<string> Allergens { get; set; } = [];

    public IReadOnlyList<string> Ingredients { get; set; } = [];

    public string? NutritionSummary { get; set; }

    public IReadOnlyList<string> ValidationWarnings { get; set; } = [];
}

public sealed class TicketPackingBagContextDto
{
    public int PackingBagId { get; set; }

    public string BagCode { get; set; } = string.Empty;

    public int BagNumber { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public int TotalBoxes { get; set; }

    public int PackedBoxes { get; set; }

    public bool HasLabels { get; set; }

    public bool IsTransportLabelAttached { get; set; }

    public DateTimeOffset? TransportLabelAttachedAt { get; set; }
}

public sealed class TicketPackingIncidentContextDto
{
    public int Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ReasonSummary { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? MealName { get; set; }

    public string? BoxCode { get; set; }

    public string? BagCode { get; set; }

    public DateTimeOffset ReportedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class TicketRouteContextDto
{
    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public int StopId { get; set; }

    public int SequenceNumber { get; set; }

    public string StopStatus { get; set; } = string.Empty;

    public DateTimeOffset? PlannedArrivalTime { get; set; }

    public DateTimeOffset? ActualArrivalTime { get; set; }

    public string? DriverName { get; set; }

    public string? VehicleRegistration { get; set; }

    public string? VehicleModel { get; set; }

    public TicketDeliveryIssueContextDto? LatestIssue { get; set; }
}

public sealed class TicketDeliveryIssueContextDto
{
    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTimeOffset ReportedAt { get; set; }
}
