using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class PackingIncidentDto
{
    public int Id { get; set; }

    public PackingIncidentType Type { get; set; }

    public PackingIncidentStatus Status { get; set; }

    public PackingIncidentReasonFlag ReasonFlags { get; set; }

    public string ReasonSummary { get; set; } = string.Empty;

    public DateOnly PackingDate { get; set; }

    public int PackingSessionId { get; set; }

    public int? PackingItemId { get; set; }

    public int? PackingBagId { get; set; }

    public int? ReplacementPackingItemId { get; set; }

    public int? ReplacementPackingBagId { get; set; }

    public string? ClientPublicId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public int? MealId { get; set; }

    public string? MealName { get; set; }

    public string? BoxCode { get; set; }

    public string? BagCode { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset ReportedAt { get; set; }

    public int? ReportedByUserId { get; set; }

    public int? AssignedToUserId { get; set; }

    public string? AdminNotes { get; set; }

    public DateTimeOffset? WarehouseWasteRegisteredAt { get; set; }

    public string? WarehouseWasteError { get; set; }

    public DateTimeOffset? KitchenStartedAt { get; set; }

    public DateTimeOffset? KitchenPreparedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
